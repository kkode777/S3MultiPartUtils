/*
    The material embodied in this software is provided to you "as-is" and without warranty of any kind, express, implied or otherwise, including without limitation, any warranty of fitness for a particular purpose.
*/

using Amazon;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CsvHelper;
using System.Globalization;
using System.Diagnostics;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
namespace S3
{
    class Program
    {
        async static Task Main(string[] args)
        {
            var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile($"appSettings.json").Build();

            IServiceCollection services = new ServiceCollection();
            var serviceProvider = services.BuildServiceProvider();

            var region = config["region"] ?? "us-east-1";
            // var sourceBucket = config["source_bucket"];
            // var targetBucket = config["target_bucket"];
            var logToDynamo=false;
            bool.TryParse(config["log_to_dynamoDB"],out logToDynamo);
            var dynamoDBTable=config["dynamoDB_table"];

            using var loggerFactory = LoggerFactory.Create(loggingBuilder => loggingBuilder
                .SetMinimumLevel(LogLevel.Debug)
                .AddConsole());

            ILogger logger = loggerFactory.CreateLogger<Program>();

            // if (string.IsNullOrEmpty(sourceBucket))
            // {
            //     logger.LogError("Cannot continue. Source bucket key is not present in AppSettings ");
            //     return;
            // }
            // if (string.IsNullOrEmpty(targetBucket))
            // {
            //     logger.LogError("Cannot continue. Target bucket key is not present in AppSettings ");
            //     return;
            // }
            if (region != "us-east-1")
            {
                logger.LogError("Cannot continue. The only supported AWS Region ID is " +
                "'us-east-1'.");
                return;
            }
            if (logToDynamo && String.IsNullOrEmpty(dynamoDBTable))
            {
                logger.LogError("Cannot continue. DynamoDB table name not present in APPSettings ");
                return;
            }
            try
            {
                logger.LogInformation("Region: {0}", region);
                var bucketRegion = RegionEndpoint.USEast1;
                var s3Client = new AmazonS3Client(bucketRegion);
            
                var dynmoDBTableCreated=false;
                var batchId = (Guid.NewGuid()).ToString();
                if(logToDynamo)
                {
                    dynmoDBTableCreated=await CreateDynamboDBTable(dynamoDBTable,logger);      
                }

                MultiPartHelper mpuHelper = new MultiPartHelper(s3Client, config, loggerFactory);
                var filePaths = GetInventory("./inventory.csv");
                var totalCount = filePaths.Count;
                logger.LogDebug($"Object key count - {totalCount}");
                logger.LogDebug("*********Copying Started**********");
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();

                //foreach(var filePath in filePaths)
                await Parallel.ForEachAsync(filePaths, async (filePath, CancellationToken) =>
                {
                    // ++counter;
                    var objectPath = filePath.ObjectName;
                    var sourceBucket=filePath.SourceBucketName;
                    var targetBucket=filePath.TargetBucketName;
                    var permissionsDict=new Dictionary<string,string>(){
                        {"file-owner",filePath.FileOwner},
                        {"file-permissions" ,filePath.FilePermissions},
                        {"file-group" ,filePath.FileGroup},
                        {"file-acl" ,filePath.FileAcl} 
                    };
                    
                    // logger.LogDebug("Copying {0} -- file {1}/{2}",objectPath,counter,totalCount);
                    logger.LogDebug("Copying {0}", objectPath);

                    var copyResponse=await mpuHelper.MPUCopyObjectAsync(sourceBucket, objectPath, targetBucket, objectPath,permissionsDict);
                    if(logToDynamo && dynmoDBTableCreated)
                    {
                        await LogItemStatusToDynamoDB(batchId,dynamoDBTable,copyResponse,logger);
                    }
                });
                stopWatch.Stop();
                // Get the elapsed time as a TimeSpan value.
                TimeSpan ts = stopWatch.Elapsed;

                // Format and display the TimeSpan value.
                string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                    ts.Hours, ts.Minutes, ts.Seconds,
                    ts.Milliseconds / 10);
                logger.LogDebug("*********Copying Complete**********");
                logger.LogDebug("Elaspsed time -- {0}", elapsedTime);
                logger.LogDebug("Batch Id: {0}. Use this for querying this batch data in DynaboDB", batchId);

            }
            catch (Exception ex)
            {
                logger.LogError(ex.Message);
            }
        }

        private static List<InventoryItem> GetInventory(string filePath)
        {
            var filePaths = new List<InventoryItem>();
            using (var reader = new StreamReader(filePath))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                csv.Context.RegisterClassMap<InventoryItemMap>();
                filePaths = csv.GetRecords<InventoryItem>().ToList();
            }
            return filePaths;
        }

        private static async Task<bool> CreateDynamboDBTable(string tableName, ILogger logger)
        {
            bool dynmoDBTableCreated=true;
            try
            {

                var client = new AmazonDynamoDBClient();
                var currentTables = await client.ListTablesAsync();

                if (!currentTables.TableNames.Contains(tableName))
                {
                    var request = new CreateTableRequest
                    {
                        TableName = tableName,
                        BillingMode= "PAY_PER_REQUEST",
                        AttributeDefinitions = new List<AttributeDefinition>
                        {
                            new AttributeDefinition
                            {
                                AttributeName = "BatchId",
                                AttributeType = "S"
                            },
                            new AttributeDefinition
                            {
                                AttributeName = "SourceKey",
                                AttributeType = "S"
                            }
                        },

                        KeySchema = new List<KeySchemaElement>
                        {
                            new KeySchemaElement
                            {
                                AttributeName = "BatchId",
                                // "HASH" = hash key, "RANGE" = range key.
                                KeyType = "HASH"
                            },
                            new KeySchemaElement
                            {
                                AttributeName = "SourceKey",
                                KeyType = "RANGE"
                            },
                        }
                        // ,
                        // ProvisionedThroughput = new ProvisionedThroughput
                        // {
                        //     ReadCapacityUnits = 10,
                        //     WriteCapacityUnits = 5
                        // },
                    };

                    var response = await client.CreateTableAsync(request);
                    logger.LogInformation("DynamoDB table successfully created");
                }
                else
                {
                    logger.LogInformation("DynamoDB table already exists.");
                }
            }
            catch (Exception ex)
            {
                logger.LogError("Error creating DynamoDB table. DynmoDB logging will be skipped. Error: {0}",ex.Message);
                dynmoDBTableCreated=false;
            }
            return dynmoDBTableCreated;
        }

        private static async Task<bool> LogItemStatusToDynamoDB(string batchId,string table,MPUCopyObjectResponse copyResponse,ILogger logger)
        {
            var success=true;
            logger.LogDebug("Table-{0}",table);
            try{
                var client = new AmazonDynamoDBClient();

                var request = new PutItemRequest
                {
                    TableName = table,
                    Item = new Dictionary<string, AttributeValue>
                    {
                        { "BatchId", new AttributeValue { S = batchId }},
                        { "SourceBucket", new AttributeValue { S = copyResponse.SourceBucket }},
                        { "SourceKey", new AttributeValue { S = copyResponse.SourceKey }},
                        { "TargetBucket", new AttributeValue { S = copyResponse.TargetBucket }},
                        { "TargetKey", new AttributeValue { S = copyResponse.TargetKey }},
                        { "Status", new AttributeValue { S = copyResponse.CopiedSuccessfully.ToString() }},
                        { "Message", new AttributeValue { S = copyResponse.Message }}
                    }
                };
                await client.PutItemAsync(request);
            }
            catch(AmazonDynamoDBException ex)
            {
                logger.LogError("Error adding item to DynamoDB table. Error: {0}",ex.Message);
                success=false;
            }
            catch(Exception ex)
            {
                logger.LogError("Error adding item to DynamoDB table. Error: {0}",ex.Message);
                success=false;
            }
            return success;
        }
   }
}
