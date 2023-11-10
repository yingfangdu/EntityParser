using System.Collections.Generic;

namespace EntityParser
{
    internal interface EntityFieldDescribe
    {
        string Name { get; }
    }

    public class SFEntityFieldDescribe  : EntityFieldDescribe
    {
        public SFEntityFieldDescribe(string name, string type, bool isNullable, int precision, int scale)
        {
            this.Name = name;
            this.Type = type;
            this.IsNullable = isNullable;
            this.Precision = precision;
            this.Scale = scale;
        }

        public string Name { get; private set; }
        public string Type { get; private set; }
        public bool IsNullable { get; private set; }
        public bool IsCompounded { get { return Utility.IsCompoundType(this.Type); } }
        public int Precision { get; private set; }
        public int Scale { get; private set; }
    }

    public class MSAEntityFieldDescribe  : EntityFieldDescribe
    {
        public MSAEntityFieldDescribe(SFEntityFieldDescribe sfEntityDescribe)
        {
            this.SFName = sfEntityDescribe.Name;
            this.Name = Utility.RefineEntityName(sfEntityDescribe.Name);
            this.Type = Utility.SoapTypeToCSharpTypeMap(sfEntityDescribe.Type);
            this.IsNullable = sfEntityDescribe.IsNullable;
            this.SQLType = Utility.GetSQLType(this.Type, sfEntityDescribe.Precision, sfEntityDescribe.Scale);
        }

        public string SFName { get; private set; }

        public string Name { get; private set; }
        public string Type { get; private set; }
        public bool IsNullable { get; private set; }

        public string SQLType { get; private set; }
    }

    internal class SFEntityDescribe
    {
        private List<SFEntityFieldDescribe> fields = new List<SFEntityFieldDescribe>();
        private List<string> compoundFieldNames = new List<string>();

        public List<SFEntityFieldDescribe> Fields { get { return this.fields; } }
        public List<string> CompoundFieldNames { get { return this.compoundFieldNames; } }
    }

    internal class AdsEntityDescribe
    {
        private List<MSAEntityFieldDescribe> fields = new List<MSAEntityFieldDescribe>();

        public List<MSAEntityFieldDescribe> Fields { get { return this.fields; } }
    }
}
