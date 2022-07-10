using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CsvHelper;
using System.Globalization;
using System.Diagnostics;
namespace S3
{
    class Program
    {
        async static Task Main(string[] args)
        {
            var config =  new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile($"appsettings.json").Build();

            IServiceCollection services = new ServiceCollection();
            var serviceProvider = services.BuildServiceProvider();

            var region=config["region"]??"us-east-1";
            var sourceBucket=config["source_bucket"];
            var targetBucket=config["target_bucket"];

            using var loggerFactory = LoggerFactory.Create(loggingBuilder => loggingBuilder
                .SetMinimumLevel(LogLevel.Trace)
                .AddConsole());

            ILogger logger = loggerFactory.CreateLogger<Program>();
            
            if (string.IsNullOrEmpty(sourceBucket))
            {
                logger.LogError("Cannot continue. Source bucket key is not present in AppSettings ") ;
                return;
            }
            if (string.IsNullOrEmpty(targetBucket))
            {
                logger.LogError("Cannot continue. Target bucket key is not present in AppSettings ") ;
                return;
            }
            if (region != "us-east-1")
            {
                logger.LogError("Cannot continue. The only supported AWS Region ID is " +
                "'us-east-1'.");
                return;
            }

            try
            {
                logger.LogInformation("Region: {0}, Source bucket: {1}, Target Bucket: {2}", region,sourceBucket,targetBucket);
                var bucketRegion=RegionEndpoint.USEast1;
                var s3Client=new AmazonS3Client(bucketRegion);
                MultiPartHelper mpuHelper=new MultiPartHelper(s3Client,config,loggerFactory);
                var filePaths=GetInventory("./inventory.csv");
                var totalCount=filePaths.Count;
                var counter=0;
                logger.LogDebug("*********Copying Started**********");
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();
                foreach(var filePath in filePaths)
                {
                    ++counter;
                    var objectPath=filePath.Path;
                    logger.LogDebug("Copying {0} -- file {1}/{2}",objectPath,counter,totalCount);

                    await mpuHelper.MPUCopyObjectAsync(sourceBucket,objectPath,targetBucket,objectPath);
                }
                stopWatch.Stop();
                // Get the elapsed time as a TimeSpan value.
                TimeSpan ts = stopWatch.Elapsed;

                // Format and display the TimeSpan value.
                string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                    ts.Hours, ts.Minutes, ts.Seconds,
                    ts.Milliseconds / 10);
                logger.LogDebug("*********Copying Complete**********");
                logger.LogDebug("Elaspsed time -- {0}",elapsedTime);
                
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    
        private static List<InventoryItem> GetInventory(string filePath)
        {
            var filePaths=new List<InventoryItem>();
            using (var reader = new StreamReader(filePath))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                 filePaths = csv.GetRecords<InventoryItem>().ToList();
            }
            return filePaths;
        }
    }
}