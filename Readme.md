** The material embodied in this software is provided to you "as-is" and without warranty of any kind, express, implied or otherwise, including without limitation, any warranty of fitness for a particular purpose.**

Install .NET

On Cloud9
https://docs.aws.amazon.com/cloud9/latest/user-guide/sample-dotnetcore.html


Clone Repository
git clone https://github.com/kkode777/S3MultiPartUtils.git

Update settings 
cd S3MultiPartUtils
Open appSettings.json file and update the settings

Restore, Build, and Run
Run the following commands

dotnet restore    #This will download all the project dependencies

dotnet build      #This will build the project

dotnet run        #This will run the project

#Make sure to note down the batchid that will logged to the console when the program completes the copy. 
#This batchid is the partition key and can be used to query the log data in the DynamoDB table 