namespace EntityParser
{
    using Newtonsoft.Json.Linq;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.IO;
    using System.Text;
    internal class Parser
    {
        private readonly string describeFilePath;
        private readonly string sampleEntityPath;
        private readonly string outputFolderPath;
        private readonly string entityName;

        public SFEntityDescribe SFDescribes { get; private set; }
        public AdsEntityDescribe AdsDescribe { get; private set; }

        public Parser(string describeFilePath, string sampleEntityPath, string outputFolderPath, string entityName)
        {
            this.describeFilePath = describeFilePath;
            this.sampleEntityPath = sampleEntityPath;
            this.outputFolderPath = outputFolderPath;
            this.entityName = entityName;
            this.AdsDescribe = new AdsEntityDescribe();
            this.SFDescribes = new SFEntityDescribe();
        }

        public bool Process()
        {
            if (!Directory.Exists(this.outputFolderPath))
            {
                Directory.CreateDirectory(this.outputFolderPath);
            }

            // Read the property name from describe file and sample file.
            this.GetAllPropertiesFromItsDescribeFile();
            this.ValidateFields(this.SFDescribes.Fields);
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
                throw new Exception($"There are duplicated property names :{string.Join("", "", duplicates)}");
            }
            else
            {
                foreach (var column in propertyNamesFromSample)
                {
                    var property = this.SFDescribes.Fields.Find(item => item.Name.Equals(column));
                    if (property != null)
                    {
                        // TODO: compounded field like Address cannot be represented now.
                        if (!property.IsCompounded)
                        {
                            this.AdsDescribe.Fields.Add(new MSAEntityFieldDescribe(property));
                        }
                    }
                    else
                    {
                        if (!column.Equals("attributes"))
                        {
                            throw new Exception($"Cannot find column {column} in the describe file");
                        }
                    }
                }

                this.ValidateFields(this.AdsDescribe.Fields);
            }

            var unKnownProperties = this.AdsDescribe.Fields.FindAll(property => property.Type == "unknown").ToList();
            if (unKnownProperties.Count > 0)
            {
                throw new Exception($"Unknown data fields: {string.Join(",", unKnownProperties.Select(field => field.Type).ToList())}");
            }

            this.GenerateFSEntityFile();
            this.GenerateQueryBuilder();
            this.GenerateParquetWriter();

            return true;
        }

        private void GetAllPropertiesFromItsDescribeFile()
        {
            string content = File.ReadAllText(this.describeFilePath);
            var dynamicObject = JsonConvert.DeserializeObject<JObject>(content);
            JArray fields = dynamicObject.Value<JArray>("fields");
            foreach (var field in fields)
            {
                string compoundFieldName = (string)(field["compoundFieldName"]);
                if (!string.IsNullOrEmpty(compoundFieldName) && !this.SFDescribes.CompoundFieldNames.Contains(compoundFieldName))
                {
                    this.SFDescribes.CompoundFieldNames.Add((string)(field["compoundFieldName"]));
                }

                this.SFDescribes.Fields.Add(new SFEntityFieldDescribe((string)(field["name"]), (string)(field["soapType"]), (bool)(field["nillable"]), int.Parse((string)(field["precision"])), int.Parse((string)(field["scale"]))));
            }
        }

        private List<string> GetAllPropertyNamesFromSample()
        {
            string content = File.ReadAllText(this.sampleEntityPath);
            var dynamicObject = JsonConvert.DeserializeObject<JObject>(content);
            return dynamicObject.Properties().Select(property => property.Name).ToList();
        }

        private void GenerateFSEntityFile()
        {
            StringBuilder fileContent = new StringBuilder();
            fileContent.Append(@"namespace Xandr.Salesforce.Data.Pull.SFEntity
{
    using Newtonsoft.Json;

");
            fileContent.AppendLine($"    internal class {this.entityName}");
            fileContent.AppendLine(@"    {
        [JsonProperty(""attributes"", NullValueHandling = NullValueHandling.Ignore)]
        public required Attributes Attributes { get; set; }
");
            foreach (var field in this.AdsDescribe.Fields)
            {
                fileContent.AppendLine($"        [JsonProperty(\"{field.SFName}\", NullValueHandling = NullValueHandling.Ignore)]");
                fileContent.AppendLine($"        public {field.TypeWithNullable} {field.Name} {{ get; set; }}");
                fileContent.Append("\r\n");
            }

            fileContent.Append(@"}
}");
            File.WriteAllText(Path.Combine(this.outputFolderPath, $"{this.entityName}.cs"), fileContent.ToString());
        }

        private void GenerateQueryBuilder()
        {
            string defaultQuery = string.Concat("SELECT +", string.Join(",", this.AdsDescribe.Fields.Select(field => field.SFName).ToList()), $"+FROM+{this.entityName}");
            StringBuilder fileContent = new StringBuilder();
            fileContent.Append(@"namespace Xandr.Salesforce.Data.Pull.QueryBuilder
{
    using Xandr.Salesforce.Data.Pull.Interface;
    using Xandr.Salesforce.Data.Pull.Utils;
");
            fileContent.AppendLine(
$"    internal class {this.entityName}QueryBuilder : SFEntityQueryBuilderBase, ISFEntityQueryBuilder");
            fileContent.AppendLine(
@"    {
" +
        $"        private readonly string EntityNameC = \"{this.entityName}\";\r\n" +
        $"        private readonly string DefaultQuery = @\"{defaultQuery}\";\r\n" +
        @"        private readonly string DeltaWhereClause = ""+WHERE+LastModifiedDate>="";

" +
$"        public {this.entityName}QueryBuilder(XandrSalesforceConfig config) : base(config) {{ }}" +
@"

        public string EntityName { get { return this.EntityNameC; } }

        public string BuildEntityReadDefaultQuery()
        {
            return string.Concat(GetBaseQueryUrl(), DefaultQuery);
        }

        public string BuildEntityReadDeltaQuery(DateTime lastUpdateTimeStamp)
        {
            string whereClause = this.DeltaWhereClause + lastUpdateTimeStamp.ToUniversalTime().ToString(""o"");
            return string.Concat(BuildEntityReadDefaultQuery(), whereClause);
        }
    }
}");
            File.WriteAllText(Path.Combine(this.outputFolderPath, $"{this.entityName}QueryBuilder.cs"), fileContent.ToString());
        }

        private void GenerateParquetWriter()
        {
            StringBuilder fileContent = new StringBuilder();
            fileContent.Append(@"namespace Xandr.Salesforce.Data.Pull.AdsDataService
{
    using System.IO;
    using System.Collections.Generic;
    using Parquet;
    using Parquet.Data;
    using Xandr.Salesforce.Data.Pull.SFEntity;
    using Parquet.Schema;
    using System.Threading.Tasks;
    using System;
    using Microsoft.Extensions.Logging;

");

            fileContent.Append($"    internal class {this.entityName}ParquetWriter :BaseParquetWriter<{this.entityName}>\r\n");
            fileContent.Append(
@"    {
");
            foreach (var field in this.AdsDescribe.Fields)
            {
                fileContent.AppendLine($"        private List<{field.TypeWithNullable}>? _{field.NameFirstCharaterInLowerCase};");
            }

            fileContent.AppendLine("\n");
            fileContent.Append($"        public {this.entityName}ParquetWriter(ILogger logger) : base(logger)");
            fileContent.Append(
@"
        {
        }
");
            fileContent.Append(@"
        protected override bool HasCache
        {
            get { return this._id != null && this._id.Count > 0; }
        }

        protected override bool ShouldFlushCache
        {
            get { return this._id != null && this._id.Count >= this.RowGroupSize; }
        }

");

            fileContent.Append($"        protected override Task<bool> WriteItem({this.entityName} item)");
            fileContent.Append(@"
    {
");
            foreach (var field in this.AdsDescribe.Fields)
            {
                fileContent.AppendLine($"        this._{field.NameFirstCharaterInLowerCase}!.Add(item.{field.Name});");
            }

            fileContent.Append(@"        return Task.FromResult(true);
    }
");

            fileContent.Append(@"
        protected override async Task FlushCache()
        {
");
            foreach (var field in this.AdsDescribe.Fields)
            {
                fileContent.AppendLine($"      var {field.NameFirstCharaterInLowerCase}Field= new DataField<{field.TypeWithNullable}>(\"{field.Name}\");");
            }
            fileContent.Append("\r\n");
            fileContent.AppendLine("var schema = new ParquetSchema(");
            foreach (var field in this.AdsDescribe.Fields)
            {
                fileContent.AppendLine(field == this.AdsDescribe.Fields.Last() ? $"     {field.NameFirstCharaterInLowerCase}Field);" :
                    $"      {field.NameFirstCharaterInLowerCase}Field,");
            }

            fileContent.Append(@"
            using (var parquetWriter = await ParquetWriter.CreateAsync(schema:schema, output: this.Stream!, formatOptions: null, append: this.AppendFile))
            {
                using (ParquetRowGroupWriter groupWriter = parquetWriter.CreateRowGroup())
                {
");
            foreach (var field in this.AdsDescribe.Fields)
            {
                fileContent.AppendLine($"      await groupWriter.WriteColumnAsync(new DataColumn({field.NameFirstCharaterInLowerCase}Field, this._{field.NameFirstCharaterInLowerCase}!.ToArray()));");
            }

            fileContent.Append(@"}
        }
    }
");

            fileContent.Append(@"
         protected override void ResetCache()
        {
            this.AppendFile = true; 
");
            foreach (var field in this.AdsDescribe.Fields)
            {
                fileContent.AppendLine($"      this._{field.NameFirstCharaterInLowerCase} = new List<{field.TypeWithNullable}>(this.RowGroupSize);");
            }

            fileContent.Append(@"}
    }
}");

            File.WriteAllText(
                Path.Combine(this.outputFolderPath, $"{this.entityName}ParquetWriter.cs"),
                fileContent.ToString());
        }

        private void ValidateFields(IEnumerable<EntityFieldDescribe> fields)
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
