# AZure Bulk CoPy Utility
[AZure Bulk CoPy Utility](https://github.com/JocaPC/AzureBCP/tree/master/dist/) is a command-line tool that enables you to load a set of files from Azure Blob Storage into Azure SQL Database or SQL Server 2017.
It uses Server-side load with [BULK INSERT T-SQL command](https://docs.microsoft.com/en-us/sql/t-sql/statements/bulk-insert-transact-sql) to load the files. Content of files is not downloaded to the computer where
you are running the tool - SQL Database directly reads data from Azure Blob Storage).
 
The syntax of the command line is:
```
azbcp <destination table> [IN] <source files> <options>...
```
First parameter of azbcp command is a destination table where data will be loaded, followed by optional **IN** token. Then you need to specify source file name(s) that will be loaded. You can use * wildcard to specify
more than one file, or you can put a list of file names separated with comma ",". You can also add one or many options to customize bulk insert process using the command line options similar to the well known
 [BCP](https://docs.microsoft.com/en-us/sql/tools/bcp-utility) command-line utility.

> Note: **IN** keyword is optional and kept for backward-compatibility with standard bcp utility.
> AzureBCP do not supports OUT option, so IN don't need to be added in command line.
 
An example of usage is:

```
azbcp Sales.SalesOrders IN *.csv -s dest.database.windows.net -d WideWorldImporters -u bulkuser -p bulkpassword -ENCRYPT -WORKERTHREADS 50 -ACCOUNT sourceaccount -CONTAINER srccontainer -SAS "sv=2017-04-17&ss=b&srt=sco&sp=rl&st=2017-11-22T11%3A31%3A00Z&se=2017-12-25T11%3A31%3A00Z&sig=rRJ%2BUbAWYIB2EllDZWhlM5bHSE%2BRNhQCw%2Fm446Gn1Bs%3D"
```

This command will load the data into Sales.SalesOrders table from the files that match pattern *.csv. There are some additional parameters that you can use in **azbcp** that don't exist in standard bcp utility:
 - ENCRYPT - Sql connection should be encrypted.
 - ACCOUNT - name of the Azure Blob Storage account where the source files are placed.
 - CONTAINER - container in Azure Blob Storage account where the source files are placed.
 - SAS - SAS Token that will be used to read files from the blob storage.
 - DATASOURCE - EXTERNAL DATA SOURCE defined in target SQL Database that will. If it is not specified, **AzBCP** will dynamically create a data source using -ACCOUNT, -CONTAINER and -SAS parameters, and drop it when the load finishes.
 - WORKERTHREADS - Number of parallel workers threads that will load the data from the files in Azure Blob Storage.
 - CSV - source files are CSV files formatted based on [RFC4180](https://tools.ietf.org/html/rfc4180) specification.

You can put the Sql connection and storage account information as command-line parameters, or you can prepare configuration parameters is **Config.json** file.
**Config.json** contains the parameters required to run the load such as the connection to the target database, number of working threads that will load data,
connection parameters required to connect to Azure Blob Storage account, etc.

An example of a configuration file is shown in the following sample:

```javascript
{
  "ConnectionString": "Server=<SERVERNAME>.database.windows.net;Database=<DATABASENAME>;User Id=<USERID>;Password=<PASSWORD>;Encrypt=True;",
  "WorkerThreads": 50,
  "Account": "STORAGE_ACCOUNT_NAME",
  "Container": "CONTAINER_NAME",
  "Sas": "sv=2017-04-17&ss=b&srt=sco&sp=rl&st=2017-11-22T11%3A31%3A00Z&se=2017-12-25T11%3A31%3A00lM5bHSE%2BRNhQCw%2Fm446Gn1Bs%3D"
}
```

# Examples
If you have placed connection information in the configuration file, you can load the files from Azure Blob Storage using the following command line: 

```
azbcp Sales.SalesOrders orders/*.csv
```
This command will import files that match orders/*.csv pattern on blob storage into the Sales.SalesOrder table.

Since database connection information and credentials required to access Azure Blob Storage are not provided in the command line, in this example it is assumed that you have placed them in **Config.json** file. 

If you have not placed the connection string in **Config.json**, you can put connection parameters in command-line like in [BCP](https://docs.microsoft.com/en-us/sql/tools/bcp-utility) command utility:

```
azbcp Sales.Orders orders/*.cvs -s .\\SQLEXPRESS -d WideWorldImporters -T -ACCOUNT myblobstorage -CONTAINER myfiles -SAS "sv=2017-04-17&ss=b&srt=sco&sp=rl&st=2017-11-22T11%3A31%3A00Z&se=2017-12-25T11%3A31%3A00lM5bHSE%2BRNhQCw%2Fm446Gn1Bs%3D"
```

You can specify additional parameters in command line to customize import command (such as first row, field/row terminator, etc.) Parameters match [BCP](https://docs.microsoft.com/en-us/sql/tools/bcp-utility) command utility.
```
azbcp Sales.Orders orders/*.cvs -s -F 2 -t , -r 0x0a -h "TABLOCK"
```

All possible command line options are shown in the following example:
```
azbcp Sales.SalesOrder orders/*.csv -s "destination.database.windows.net" -d WWI -U "bulk-user" -P P4ssword -ENCRYPT -ACCOUNT sourceblob -CONTAINER mycontainer -SAS "sv=2017-04-17&ss=b&srt=sco&sp=rl&st=2017-11-22T11%3A31%3A00Z&se=2017-12-25T11%3A31%3A00Z&sig=rRJ%2BUbAWYIB2E%3D" -DOP 50 -QUERYLOG failed-queries.json -b 100000 -F 4 -r ; -t "|" -csv -FIELDQUOTE ' -C 86001 -c -m 100 -h "TABLOCK"
```

# Download

You can download the AzureBCP utility as [zip](https://github.com/JocaPC/AzureBCP/blob/dist/master/azbcp.zip),
[tar.gz](https://github.com/JocaPC/AzureBCP/blob/dist/master/azbcp.tar.gz), or [7z](https://github.com/JocaPC/AzureBCP/blob/dist/master/azbcp.7z) archive.
There is no installer - just extract the files and load the data.

# Build the code

You can download the code from this gitHub repository and build it locally. You will need Visual Studio 2017 and .Net Framework.
Note that default post-build action will archive executable files into **.zip**, **.tar.gz**, and **.7z** archives, and put them in the [dist](dist) folder.

```
7z a -tzip azbcp.zip licence.txt *.exe *.dll *.config *.xml
7z a  azbcp.7z licence.txt *.exe *.dll *.config *.xml
7z a -ttar azbcp.tar licence.txt *.exe *.dll *.config *.xml
7z a -tgzip azbcp.tar.gz azbcp.tar
del azbcp.tar /Q
copy azbcp.*z* "../../../../dist/"
```
You will need to [download 7zip](http://www.7-zip.org/download.html) and put it in the **path**
environment variable to run this step. Otherwise, you can just delete this post-build action.