using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using System;
using System.Threading.Tasks;
     
namespace S3
{
  class Program
  {
   async static Task Main(string[] args)
   {
        if (args.Length < 3) 
        {
          Console.WriteLine("Usage:  <the AWS Region to use> <source bucket name> <target bucket name>");
          Console.WriteLine("Example: us-east-1 my-source-bucket my-target-bucket");
          return;
        }
     
        if (args[0] != "us-east-1") 
        {
          Console.WriteLine("Cannot continue. The only supported AWS Region ID is " +
          "'us-east-1'.");
           return;
        }
         
        var bucketRegion = RegionEndpoint.USEast1;
        var source_bucket_name = args[1];
        var target_bucket_name = args[2];
     
        using (var s3Client = new AmazonS3Client(bucketRegion)) 
        {
        
            
        }
     
    }
  }
}