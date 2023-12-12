namespace EntityParser
{
    using System.Collections.Generic;
    internal interface EntityFieldDescribe
    {
        string Name { get; }
    }

    public class SFEntityFieldDescribe : EntityFieldDescribe
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

    public class MSAEntityFieldDescribe : EntityFieldDescribe
    {
        public MSAEntityFieldDescribe(SFEntityFieldDescribe sfEntityDescribe)
        {
            this.SFName = sfEntityDescribe.Name;
            this.Name = Utility.RefineEntityName(sfEntityDescribe.Name);
            this.NameFirstCharaterInLowerCase = this.Name.Substring(0, 1).ToLower() + this.Name.Substring(1);
            this.Type = Utility.SoapTypeToCSharpTypeMap(sfEntityDescribe.Type);
            this.IsNullable = sfEntityDescribe.IsNullable;
            this.TypeWithNullable = Utility.AddNullableMarker(this.Type, this.IsNullable);
        }

        public string SFName { get; private set; }
        public string Name { get; private set; }
        public string NameFirstCharaterInLowerCase { get; private set; }
        public string Type { get; private set; }
        public string TypeWithNullable { get; private set; }
        public bool IsNullable { get; private set; }
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
