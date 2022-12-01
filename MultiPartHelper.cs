/*
    The material embodied in this software is provided to you "as-is" and without warranty of any kind, express, implied or otherwise, including without limitation, any warranty of fitness for a particular purpose.
*/
using Amazon.S3;
using Amazon.S3.Transfer;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace S3
{
    public class MultiPartHelper
    {
        private IAmazonS3 s3Client;//=null;
        private IConfigurationRoot config;//=null;
        private ILogger logger;//=null;
        public MultiPartHelper(IAmazonS3 s3Client, IConfigurationRoot config,ILoggerFactory loggerFactory)
        {
                this.s3Client=s3Client;
                this.config=config;
                this.logger=loggerFactory.CreateLogger<MultiPartHelper>();
        }
        
        public async Task MPUUploadFileAsync(string bucketName, string keyName, string filePath, Dictionary<string,string> metadataDict)
        {
            try
            {
                logger.LogInformation("Inside Upload");
                var fileTransferUtility =
                    new TransferUtility(s3Client);

                long partSize=6291456;
                long.TryParse(config["multipart_chunk_size"],out partSize);
               //Multi-Part Upload
                var fileTransferUtilityRequest = new TransferUtilityUploadRequest()
                {
                    BucketName = bucketName,
                    FilePath = filePath,
                    StorageClass = S3StorageClass.Standard,
                    PartSize = partSize, 
                    Key = keyName
                };
                
                foreach(var item in metadataDict)
                {
                   fileTransferUtilityRequest.Metadata.Add(item.Key, item.Value); 
                }
                fileTransferUtilityRequest.UploadProgressEvent +=
                    new EventHandler<UploadProgressArgs>
                        (uploadRequest_UploadPartProgressEvent);

                logger.LogInformation("Upload Started");

                await fileTransferUtility.UploadAsync(fileTransferUtilityRequest);
                logger.LogInformation("Upload completed");
            }
            catch (AmazonS3Exception e)
            {
                logger.LogInformation("Error encountered on server. Message:'{0}' when writing an object", e.Message);
            }
            catch (Exception e)
            {
                logger.LogInformation("Unknown encountered on server. Message:'{0}' when writing an object", e.Message);
            }

        }
        private void uploadRequest_UploadPartProgressEvent(object sender, UploadProgressArgs e)
        {
            // Process event.
            logger.LogInformation("{0}/{1}", e.TransferredBytes, e.TotalBytes);
        }
        public  async Task<MPUCopyObjectResponse> MPUCopyObjectAsync(string sourceBucket,string sourceObjectKey, string targetBucket, string targetObjectKey,Dictionary<string,string> permissionsDict)
        {
        
            //bool copyIfStrippedMetadaisNotPresent;
            //bool.TryParse(config["copy_if_stripped_metadata_notpresent"],out copyIfStrippedMetadaisNotPresent);
            //long partSize = 5 * (long)Math.Pow(2, 20); // Part size is 5 MB.
            long partSize=6291456;
            long.TryParse(config["multipart_chunk_size"],out partSize);
            var copyResponse=new MPUCopyObjectResponse(sourceBucket,sourceObjectKey,targetBucket,targetObjectKey);

            // Create a list to store the upload part responses.
            List<UploadPartResponse> uploadResponses = new List<UploadPartResponse>();
            List<CopyPartResponse> copyResponses = new List<CopyPartResponse>();

            GetObjectMetadataRequest metadataRequest = new GetObjectMetadataRequest
                {
                    BucketName = sourceBucket,
                    Key = sourceObjectKey
                };

            var newMetadata= await GetObjectMetadata(metadataRequest,permissionsDict);
            
            //if(newMetadata.ContentLength==0 || (!newMetadata.StrippedKeysPresent && !copyIfStrippedMetadaisNotPresent))
            if(newMetadata.ContentLength==0)
            {
                logger.LogInformation("Metadata get error. Not copying object {0}/{1}",sourceBucket,sourceObjectKey);
                copyResponse.Message=$"Unable to retrieve metadata. Error: {newMetadata.MetadataError}";
                return copyResponse ;
            }
            if(newMetadata.ContentLength<=partSize)
            {
                return await CopyObjectAsync(sourceBucket,sourceObjectKey,targetBucket,targetObjectKey,newMetadata,newMetadata.ContentLength);
            }
            // Setup information required to initiate the multipart upload.
            InitiateMultipartUploadRequest initiateRequest =
                new InitiateMultipartUploadRequest
                {
                    BucketName = targetBucket,
                    Key = targetObjectKey,
                };
            foreach(var item in newMetadata.MetadataCollection)
            {
                initiateRequest.Metadata.Add(item.Key,item.Value);
            }
            
            logger.LogInformation("Initiating multi-part upload for {0}/{1}",sourceBucket,sourceObjectKey);
            // Initiate the upload.
            InitiateMultipartUploadResponse initResponse =
                await s3Client.InitiateMultipartUploadAsync(initiateRequest);

            // Save the upload ID.
            String uploadId = initResponse.UploadId;
            try
            {
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();
                long objectSize = newMetadata.ContentLength; // Length in bytes.
                copyResponse.ObjectSize=objectSize;
                // Copy the parts.
               
                long bytePosition = 0;
                for (int i = 1; bytePosition < objectSize; i++)
                {
                    CopyPartRequest copyRequest = new CopyPartRequest
                    {
                        DestinationBucket = targetBucket,
                        DestinationKey = targetObjectKey,
                        SourceBucket = sourceBucket,
                        SourceKey = sourceObjectKey,
                        UploadId = uploadId,
                        FirstByte = bytePosition,
                        LastByte = bytePosition + partSize - 1 >= objectSize ? objectSize - 1 : bytePosition + partSize - 1,
                        PartNumber = i
                    };
                    copyResponses.Add(await s3Client.CopyPartAsync(copyRequest));
                    bytePosition += partSize;
                    MPUCopyProgress(sourceObjectKey,bytePosition,objectSize);
                }
                // Set up to complete the copy.
                CompleteMultipartUploadRequest completeRequest =
                new CompleteMultipartUploadRequest
                {
                    BucketName = targetBucket,
                    Key = targetObjectKey,
                    UploadId = initResponse.UploadId
                };
                completeRequest.AddPartETags(copyResponses);
                // Complete the copy.
                CompleteMultipartUploadResponse completeUploadResponse = 
                    await s3Client.CompleteMultipartUploadAsync(completeRequest);
                copyResponse.CopiedSuccessfully=true;
                logger.LogInformation("Multi-part upload complete for {0}/{1}",sourceBucket,sourceObjectKey);
                stopWatch.Stop();
                var elapsedTimeInMinutes=stopWatch.Elapsed.TotalMinutes;
                copyResponse.ElapsedTimeInMinutes=elapsedTimeInMinutes;
                return copyResponse;
            }
            catch (AmazonS3Exception e)
            {
                logger.LogError("Error encountered on server. Message:'{0}' when copying object {1}/{2}", e.Message,sourceBucket,sourceObjectKey);
                copyResponse.Message=e.Message;
            }
            catch (Exception e)
            {
                logger.LogError("Unknown encountered on server. Message:'{0}' when copying object {1}/{2}", e.Message,sourceBucket,sourceObjectKey);
                copyResponse.Message=e.Message;
            }


            return copyResponse;
        }
        public  async Task<MPUCopyObjectResponse> CopyObjectAsync(string sourceBucket,string sourceObjectKey, string targetBucket, string targetObjectKey,Metadata metadata,long ContentLength)
        {
            var copyResponse=new MPUCopyObjectResponse(sourceBucket,sourceObjectKey,targetBucket,targetObjectKey);
            try
            {
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();
                CopyObjectRequest request = new CopyObjectRequest
                {
                    SourceBucket = sourceBucket,
                    SourceKey = sourceObjectKey,
                    DestinationBucket = targetBucket,
                    DestinationKey = targetObjectKey,
                    MetadataDirective=S3MetadataDirective.REPLACE,
                };
                foreach(var item in metadata.MetadataCollection)
                {
                    request.Metadata.Add(item.Key,item.Value);
                }
                logger.LogInformation("Object less than multi-part threshold. Copying object {0}/{1} using regular copy.",sourceBucket,sourceObjectKey);
                CopyObjectResponse response = await s3Client.CopyObjectAsync(request);
                copyResponse.CopiedSuccessfully=true;
                logger.LogInformation("Copied object - {0}/{1}",sourceBucket,sourceObjectKey);
                
                stopWatch.Stop();
                var elapsedTimeInMinutes=stopWatch.Elapsed.TotalMinutes;
                copyResponse.ElapsedTimeInMinutes=elapsedTimeInMinutes;
                copyResponse.ObjectSize=ContentLength;
            }
            catch (AmazonS3Exception e)
            {
                logger.LogError("Error encountered on server. Message:'{0}' when writing an object", e.Message);
                copyResponse.Message=e.Message;
            }
            catch (Exception e)
            {
                logger.LogError("Unknown encountered on server. Message:'{0}' when writing an object", e.Message);
                copyResponse.Message=e.Message;
            }
            return copyResponse;
        }
        private void MPUCopyProgress(string objectKey, long copiedBytes, long totalBytes)
        {
             int percentCopied=Convert.ToInt32(copiedBytes*100/totalBytes);
             logger.LogDebug("Copying {0} - Copied Percent {1}% - {2}/{3} Bytes", objectKey,percentCopied,copiedBytes, totalBytes);
        }
        private async Task<Metadata> GetObjectMetadata(GetObjectMetadataRequest objectMetadataRequest,Dictionary<string,string> permissionsDict)
        {
            var strippedKeys=config["stripped_keys"]??""; 
            var newMetadataDictionary=new Dictionary<string,string>();
            var newMetadata= new Metadata();
            try
            {
                var strippedKeyList=strippedKeys.Split(',');
                if(strippedKeyList.Length>0)
                {
                    strippedKeyList=strippedKeyList.Select(k=>{k="x-amz-meta-"+k;return k;}).ToArray();
                }
                
                logger.LogInformation("Getting metadata for object - {0}/{1} ",objectMetadataRequest.BucketName,objectMetadataRequest.Key);

                GetObjectMetadataResponse metadataResponse =
                    await s3Client.GetObjectMetadataAsync(objectMetadataRequest);
                newMetadata.ContentLength=metadataResponse.ContentLength;
                var metadataCollection=metadataResponse.Metadata;

                if(metadataCollection?.Count>0)
                {
                    var currentKeyList=metadataCollection.Keys;
                    if(currentKeyList.Any(item=>strippedKeyList.Contains(item)))
                    {
                        newMetadata.StrippedKeysPresent=true;
                    }

                    var newKeyList=currentKeyList.Except(strippedKeyList); //Remove NFS metadata and retain other metadata
                    
                    foreach(var key in newKeyList)
                    {
                        if(!String.IsNullOrEmpty(metadataCollection[key]))
                        {
                            newMetadataDictionary.Add(key,metadataCollection[key]);
                        }
                    }
                   // newMetadata.MetadataCollection=newMetadataDictionary;
                }
                //Add the SMB permissions
                foreach(var perm in permissionsDict)
                    {
                        if(!String.IsNullOrEmpty(perm.Value))
                        {
                            newMetadataDictionary.Add(perm.Key,perm.Value);
                        }
                }

                newMetadata.MetadataCollection=newMetadataDictionary;
            }
            catch(Exception ex)
            {
                 logger.LogError("Error getting meta data for Object:'{0}'. Error: {1}",objectMetadataRequest.Key ,ex.Message);
                 newMetadata.MetadataError=ex.Message;
            }
            return newMetadata;
        }

    }
}
