This tool is to generate three files:
- Entity file: this is the data class to deserialize objects from Salesforce. It contains all the properites/fields an object can have from its sample file.
- Parquet Writer file: this is the class to write all entities into a parquet file format and the columns are the entity's fields.
- Query builder file: this is the file to generate the SOQL to query entities from Salesforce.

  
Run `EntityParser.exe arg0 arg1 arg2 arg3`
- arg0: The entity describe file path.
- arg1: The entity sample file path.
- arg2: The output folder path.
- arg3: The entity name you want to use.

For example `EntityParser.exe "C:\Users\yingfand\OneDrive - Microsoft\Temp\UCM\Xandr\Response\account-describe.json" "C:\Users\yingfand\OneDrive - Microsoft\Temp\UCM\Xandr\Response\account-sample.json" "C:\Users\yingfand\Download\Ouput"  "Account"`
It will generate:
- Account.cs: entity file.
- AccountParquetWriter.cs: Parquet Writer file.
- AccountQueryBuilder.cs: Query builder.

After getting these generated files:
- Add these files into the corresponding folder of Xandr.Salesforce.Data.Pull project.
- You need to format these documents properly, using "Edit\Advanced\Format Document" command in visual studio.
- Build the project, you should not get any errors. If get errors:
- Check your `EntityParquetWriter.cs` implementation, they are all properly implemented.
   - ResetCache()
   - WriteItem()
   - FlushCache()
   - HasCache
   - ShouldFlushCache
- Add the creation in `EntitySyncProcessorManager.CreateSyncProcessor` to create a `EntitySyncProcessor` for the new entity.

  
