using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace EntityParser
{
    internal class Parser
    {
        private readonly string describeFilePath;
        private readonly string sampleEntityPath;
        private readonly string outputFolderPath;
        private readonly string entityName;

        public List<Entity> FieldsFromDescribeFile { get; private set; }
        public List<Entity> FieldsFromSample { get; private set; }

        public Parser(string describeFilePath, string sampleEntityPath, string outputFolderPath, string entityName)
        {
            this.describeFilePath = describeFilePath;
            this.sampleEntityPath = sampleEntityPath;
            this.outputFolderPath = outputFolderPath;
            this.entityName = entityName;
        }

        public bool Process()
        {
            if (!Directory.Exists(this.outputFolderPath))
            {
                Directory.CreateDirectory(this.outputFolderPath);
            }
            this.FieldsFromDescribeFile = this.GetAllFieldsFromItsDescribeFile();
            var fieldNamesFromSample = this.GetAllFieldNamesFromSample();
            this.FieldsFromSample = new List<Entity>();
            foreach (var column in fieldNamesFromSample)
            {
                var field = this.FieldsFromDescribeFile.Find(item => item.Name.Equals(column));
                if (field != null)
                {
                    this.FieldsFromSample.Add(new Entity(field.Name, field.Type, field.IsNullable));
                }
                else
                {
                    Console.Error.WriteLine($"Cannot find column {column} in the describe file");
                }
            }

            var unKnownFields = this.FieldsFromSample.FindAll(field => field.CSharpType == "unknown").ToList();
            if (unKnownFields.Count > 0)
            {
                Console.Error.WriteLine($"Unkwn data fields", string.Join(",", unKnownFields.Select(field => field.Type).ToList()));
            }

            this.GenerateFSEntityFile();
            this.GenerateAdsEntityFile();
            this.GenerateQueryBuilder();
            this.GenerateConverter();
            this.GenerateDataService();

            return true;
        }

        private List<Entity> GetAllFieldsFromItsDescribeFile()
        {
            string content = File.ReadAllText(this.describeFilePath);
            var dynamicObject = JsonConvert.DeserializeObject<JObject>(content);
            JArray fields = dynamicObject.Value<JArray>("fields");
            return fields.Select(field =>
                new Entity((string)(field["name"]), (string)(field["soapType"]), (bool)(field["nillable"]))).ToList();
        }

        private List<string> GetAllFieldNamesFromSample()
        {
            string content = File.ReadAllText(this.sampleEntityPath);
            var dynamicObject = JsonConvert.DeserializeObject<JObject>(content);
            return dynamicObject.Properties().Select(property => property.Name).ToList();
        }

        private void GenerateFSEntityFile()
        {
            string sfEntityClassStart = @"
using Newtonsoft.Json;

namespace Microsoft.Advertising.XandrSFDataService.SFEntity
{
" +
        $"    internal class {this.entityName}" + @"{
        [JsonProperty(""attributes"", NullValueHandling = NullValueHandling.Ignore)]
        public Attributes Attributes { get; set; }
";
            string sfEntityClassEnd = @"
    }
}";
            string fileContent = sfEntityClassStart;
            foreach (var field in this.FieldsFromSample)
            {
                fileContent = string.Concat(fileContent, $"\r        [JsonProperty(\"{field.Name}\", NullValueHandling = NullValueHandling.Ignore)]\r        public {field.CSharpType} {field.RefineName} {{ get; set; }}\r");
            }

            fileContent = string.Concat(fileContent, sfEntityClassEnd);
            File.WriteAllText(Path.Combine(this.outputFolderPath, $"SF{this.entityName}.cs"), fileContent);
        }

        private void GenerateAdsEntityFile()
        {
            string adsEntityClassStart = @"
using System;

namespace Microsoft.Advertising.XandrSFDataService.AdsEntity
{
    [Serializable]
    " + $"internal class {this.entityName}" + @"{
";
            string adsEntityClassEnd = @"
    }
}";
            string fileContent = adsEntityClassStart;
            foreach (var field in this.FieldsFromSample)
            {
                string typeWithNullableMarker = Utility.AddNullableMarker(field.CSharpType, field.IsNullable);
                fileContent = string.Concat(fileContent, $"\r        public {typeWithNullableMarker} {field.RefineName} {{ get; set; }}\r");
            }

            fileContent = string.Concat(fileContent, adsEntityClassEnd);
            File.WriteAllText(Path.Combine(this.outputFolderPath, $"Ad{this.entityName}.cs"), fileContent);
        }

        private void GenerateQueryBuilder()
        {
            string defaultQuery = string.Concat("SELECT +", string.Join(",", this.FieldsFromSample.Select(field => field.Name).ToList()), $"+FROM+{this.entityName}");
            string builderClassStart = @"
using Microsoft.Advertising.XandrSFDataService.Interface;
using System;

namespace Microsoft.Advertising.XandrSFDataService.QueryBuilder
{
" + $"internal class {this.entityName}QueryBuilder : SFEntityQueryBuilderBase, ISFEntityQueryBuilder, IAdsEntityQueryBuilder\r" + @"
{
" +
        $"private readonly string EntityNameC = \"{this.entityName}\";\r" +
        $"private readonly string TableNameC = \"{this.entityName}\";\r" +
        $"private readonly string DefaultQuery = @\"{defaultQuery}\";\r" +

        @"
        public string EntityName { get { return this.EntityNameC; } }
        public string TableName { get { return this.TableNameC; } }

        public string BuildEntityReadQuery(DateTime? lastUpdateTimeStamp)
        {
            return string.Concat(GetBaseQueryUrl(), DefaultQuery);
        }

        public string BuildTableExistQuery()
        {
            return $""SELECT COUNT(1) as Count FROM sys.tables  where name = {this.TableName}"";
        }

        public string BuildTableCreationQuery()
        {
            return $""CREATE TABLE [{this.TableName}] (";

            string columns = "";
            int maxColumnLength = this.FieldsFromSample.Select(field => field.RefineName.Length).Max();
            int formattedColumnLength = maxColumnLength + 4;
            foreach (var field in this.FieldsFromSample)
            {
               columns = string.Concat(columns, $"\r    {field.RefineName}{new string(' ', formattedColumnLength - field.RefineName.Length)}{Utility.GetSQLType(field.CSharpType)} NULL,");
            }
            string builderClassEnd = @"
);"";
        }
    }
}";

            File.WriteAllText(Path.Combine(this.outputFolderPath, $"{this.entityName}QueryBuilder.cs"), string.Concat(builderClassStart, columns, builderClassEnd));
        }

        private void GenerateDataService()
        {
            string builderClassStart = @"
using Microsoft.Advertising.XandrSFDataService.AdsEntity;
using Microsoft.Advertising.XandrSFDataService.Interface;
using SqlBulkTools;
using System.Collections.Generic;

namespace Microsoft.Advertising.XandrSFDataService.QueryBuilder
{
" + $"internal class {this.entityName}DataService : AdsDataServiceBase<{this.entityName}>" + @"
{
" +
        $"public {this.entityName}DataService(IAdsEntityQueryBuilder queryBuilder) : base(queryBuilder)" + @"
{
}

" +
        $"protected override BulkInsertOrUpdate<{this.entityName}> BuildBulkEditOperation(List<{this.entityName}> items)" +
        @"
{
        return new BulkOperations()
" + $"                       .Setup<{this.entityName}>()" + @"
                        .ForCollection(items)
                        .WithTable(TableName)";
            string columns = "";
            foreach (var field in this.FieldsFromSample)
            {
                columns = string.Concat(columns, $"\r                        .AddColumn(x => x.{field.RefineName}, \"{field.RefineName}\")");
            }
            string builderClassEnd = @"
                        .BulkInsertOrUpdate()
                        .SetIdentityColumn(x => x.Id)
                        .MatchTargetOn(x => x.Id);
        }
    }
}";
            File.WriteAllText(Path.Combine(this.outputFolderPath, $"{this.entityName}DataService.cs"), string.Concat(builderClassStart, columns, builderClassEnd));
        }

        private void GenerateConverter()
        {
            string adsEntityConverterClassStart = @"

namespace Microsoft.Advertising.XandrSFDataService.Converter
{
    " + $"internal static class {this.entityName}Converter" + @"
    {
        " + $"public static  AdsEntity.{this.entityName} Converter(SFEntity.{this.entityName} aSFEntity)" + @"
        {
" + $"            return new AdsEntity.{this.entityName}()" + @"
{";
            string adsEntityConverterClassEnd = @"
            };
        }
    }
}";
            string fileContent = adsEntityConverterClassStart;
            foreach (var field in this.FieldsFromSample)
            {
                fileContent = string.Concat(fileContent, $"\r{field.RefineName} = aSFEntity.{field.RefineName},");
            }

            fileContent = string.Concat(fileContent, adsEntityConverterClassEnd);

            File.WriteAllText(Path.Combine(this.outputFolderPath, $"{this.entityName}Converter.cs"), fileContent);
        }
    }
}
