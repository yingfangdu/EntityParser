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
            this.GenerateQuery();

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
        internal class Entity {
            [JsonProperty(""attributes"", NullValueHandling = NullValueHandling.Ignore)]
            public Attributes Attributes { get; set; }
";
            string sfEntityClassEnd = @"
    }
}";
            string fileContent = sfEntityClassStart;
            foreach (var field in this.FieldsFromSample)
            {
                fileContent = string.Concat(fileContent, $"\n            [JsonProperty(\"{field.Name}\", NullValueHandling = NullValueHandling.Ignore)]\n            public {field.CSharpType} {field.RefineName} {{ get; set; }}\n");
            }

            fileContent = string.Concat(fileContent, sfEntityClassEnd);
            File.WriteAllText(Path.Combine(this.outputFolderPath, "SFEntity.cs"), fileContent);
        }

        private void GenerateAdsEntityFile()
        {
            string adsEntityClassStart = @"
using System;

namespace Microsoft.Advertising.XandrSFDataService.AdsEntity
{
        [Serializable]
        internal class Entity {
";
            string adsEntityClassEnd = @"
    }
}";
            string fileContent = adsEntityClassStart;
            foreach (var field in this.FieldsFromSample)
            {
                string nullableMarker = field.IsNullable ? "?" : string.Empty;
                fileContent = string.Concat(fileContent, $"\n            public {field.CSharpType}{nullableMarker} {field.RefineName} {{ get; set; }}\n");
            }

            fileContent = string.Concat(fileContent, adsEntityClassEnd);
            File.WriteAllText(Path.Combine(this.outputFolderPath, "AdsEntity.cs"), fileContent);
        }

        private void GenerateQuery()
        {
            string defaultQuery = string.Concat("SELECT+", string.Join(",", this.FieldsFromSample.Select(field => field.Name).ToList()), $"+FROM+{this.entityName}");
            File.WriteAllText(Path.Combine(this.outputFolderPath, "Query.txt"), defaultQuery);
        }
    }
}
