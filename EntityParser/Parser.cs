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

        public List<SFEntityDescribe> PropertiesFromDescribeFile { get; private set; }
        public List<MSAEntityDescribe> PropertiesFromSample { get; private set; }

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

            // Read the property name from describe file and sample file.
            this.PropertiesFromDescribeFile = this.GetAllPropertiesFromItsDescribeFile();
            this.ValidateFields(this.PropertiesFromDescribeFile);
            var propertyNamesFromSample = this.GetAllPropertyNamesFromSample();
            var refinedPropertyNamesFromSample = propertyNamesFromSample.Select(name => Utility.RefineEntityName(name)).ToList();

            // Utility.TurnOffRefineEntityName = true;
            // You can comment out this if block and turn on the above line.
            if (refinedPropertyNamesFromSample.Count != refinedPropertyNamesFromSample.Distinct().Count())
            {
                var duplicates = refinedPropertyNamesFromSample
                    .GroupBy(i => i)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key);
                throw new Exception($"There are duplicated property names :{string.Join("","", duplicates)}");
            }
            else
            {
                this.PropertiesFromSample = new List<MSAEntityDescribe>();
                foreach (var column in propertyNamesFromSample)
                {
                    var property = this.PropertiesFromDescribeFile.Find(item => item.Name.Equals(column));
                    if (property != null)
                    {
                        this.PropertiesFromSample.Add(new MSAEntityDescribe(property));
                    }
                    else
                    {
                        if (!column.Equals("attributes"))
                        {
                            throw new Exception($"Cannot find column {column} in the describe file");
                        }
                    }
                }

                this.ValidateFields(this.PropertiesFromSample);
            }

            var unKnownProperties = this.PropertiesFromSample.FindAll(property => property.Type == "unknown").ToList();
            if (unKnownProperties.Count > 0)
            {
                throw new Exception($"Unknown data fields: {string.Join(",", unKnownProperties.Select(field => field.Type).ToList())}");
            }

            this.GenerateFSEntityFile();
            this.GenerateAdsEntityFile();
            this.GenerateQueryBuilder();
            this.GenerateConverter();
            this.GenerateAdsDataService();

            return true;
        }

        private List<SFEntityDescribe> GetAllPropertiesFromItsDescribeFile()
        {
            string content = File.ReadAllText(this.describeFilePath);
            var dynamicObject = JsonConvert.DeserializeObject<JObject>(content);
            JArray fields = dynamicObject.Value<JArray>("fields");
            return fields.Select(field =>
                new SFEntityDescribe((string)(field["name"]), (string)(field["soapType"]), (bool)(field["nillable"]))).ToList();
        }

        private List<string> GetAllPropertyNamesFromSample()
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
            foreach (var field in this.PropertiesFromSample)
            {
                fileContent = string.Concat(fileContent, $"\r\n        [JsonProperty(\"{field.Name}\", NullValueHandling = NullValueHandling.Ignore)]\r\n        public {field.Type} {field.Name} {{ get; set; }}\r\n");
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
            foreach (var field in this.PropertiesFromSample)
            {
                string typeWithNullableMarker = Utility.AddNullableMarker(field.Type, field.IsNullable);
                fileContent = string.Concat(fileContent, $"\r\n        public {typeWithNullableMarker} {field.Name} {{ get; set; }}\r\n");
            }

            fileContent = string.Concat(fileContent, adsEntityClassEnd);
            File.WriteAllText(Path.Combine(this.outputFolderPath, $"Ad{this.entityName}.cs"), fileContent);
        }

        private void GenerateQueryBuilder()
        {
            string defaultQuery = string.Concat("SELECT +", string.Join(",", this.PropertiesFromSample.Select(field => field.Name).ToList()), $"+FROM+{this.entityName}");
            string builderClassStart = @"
using Microsoft.Advertising.XandrSFDataService.Interface;
using System;

namespace Microsoft.Advertising.XandrSFDataService.QueryBuilder
{
" + $"internal class {this.entityName}QueryBuilder : SFEntityQueryBuilderBase, ISFEntityQueryBuilder, IAdsEntityQueryBuilder\r\n" + @"
{
" +
        $"private readonly string EntityNameC = \"{this.entityName}\";\r\n" +
        $"private readonly string TableNameC = \"{this.entityName}\";\r\n" +
        $"private readonly string DefaultQuery = @\"{defaultQuery}\";\r\n" +

        @"
        public string EntityName { get { return this.EntityNameC; } }
        public string TableName { get { return this.TableNameC; } }

        public string BuildEntityReadQuery(DateTime? lastUpdateTimeStamp)
        {
            return string.Concat(GetBaseQueryUrl(), DefaultQuery);
        }

        public string BuildTableCreationQuery()
        {
            return $""CREATE TABLE [{this.TableName}]"" + @""(";

            string tableColumns = "";
            int maxColumnLength = this.PropertiesFromSample.Select(field => field.Name.Length).Max();
            int formattedColumnLength = maxColumnLength + 4;
            foreach (var field in this.PropertiesFromSample)
            {
                tableColumns = string.Concat(tableColumns, $"\r\n    {field.Name}{new string(' ', formattedColumnLength - field.Name.Length)}{Utility.GetSQLType(field.Type)} NULL,");
            }
            string builderClassEnd = @"
);"";
        }
    }
}";

            File.WriteAllText(Path.Combine(this.outputFolderPath, $"{this.entityName}QueryBuilder.cs"), string.Concat(builderClassStart, tableColumns, builderClassEnd));
        }

        private void GenerateAdsDataService()
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
            foreach (var field in this.PropertiesFromSample)
            {
                columns = string.Concat(columns, $"\r\n                        .AddColumn(x => x.{field.Name}, \"{field.Name}\")");
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
            foreach (var field in this.PropertiesFromSample)
            {
                fileContent = string.Concat(fileContent, $"\r\n                {field.Name} = aSFEntity.{field.Name},");
            }

            fileContent = string.Concat(fileContent, adsEntityConverterClassEnd);

            File.WriteAllText(Path.Combine(this.outputFolderPath, $"{this.entityName}Converter.cs"), fileContent);
        }

        private void ValidateFields(IEnumerable<EntityDescribe> fields)
        {
            var names = fields.Select(f => f.Name).ToList();
            if (names.Count != names.Distinct().Count())
            {
                var duplicates = names
                    .GroupBy(i => i)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key);
                throw new Exception($"duplicated fields: {string.Join(",", duplicates)}");
            }
        }
    }
}
