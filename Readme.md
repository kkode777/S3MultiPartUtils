# Introduction
The purpose of this project is to do a parallel copy of objects larger than 5 GB between two S3 buckets. Using .NET Task Parallel Library, the object list is partitioned so that multiple threads can operate on different segments concurrently. The code also has provision to remove existing metadata and add new metadata to the objects in the target bucket. The status of each object is logged to a DynamoDB table which is created when the program starts. 

S3 Batch operations with Lambda can be used for copying and modifying metadata for objects smaller than 5 GB but there is no native solution to copy objects larger than 5 GB in bulk. 


# Instructions

### Install .NET
https://docs.aws.amazon.com/cloud9/latest/user-guide/sample-dotnetcore.html
https://learn.microsoft.com/en-us/dotnet/core/install/

### Update Configuration Settings
Open appSettings.json file and update relevant settings

### Update the inventory.csv 
Add the objects to copy to this file. 


### Restore, Build, and Run

#### **This will download all the project dependencies** 
dotnet restore    

#### **This will build the project**
dotnet build     

#### **This will run the project**
dotnet run        

**Make sure to note down the batchid that will logged to the console when the program completes the copy. 
  This batchid is the partition key and can be used to query the log data in the DynamoDB table**