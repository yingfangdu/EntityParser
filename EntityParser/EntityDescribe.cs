namespace EntityParser
{
    internal interface EntityDescribe
    {
        string Name { get; }
    }

    public class SFEntityDescribe  : EntityDescribe
    {
        public SFEntityDescribe(string name, string type, bool isNullable)
        {
            this.Name = name;
            this.Type = type;
            this.IsNullable = isNullable;
        }

        public string Name { get; private set; }
        public string Type { get; private set; }
        public bool IsNullable { get; private set; }
    }

    public class MSAEntityDescribe  : EntityDescribe
    {
        public MSAEntityDescribe(SFEntityDescribe sfEntityDescribe)
        {
            this.Name = Utility.RefineEntityName(sfEntityDescribe.Name);
            this.Type = Utility.SoapTypeToCSharpTypeMap(sfEntityDescribe.Type);
            this.IsNullable = sfEntityDescribe.IsNullable;
        }

        public string Name { get; private set; }
        public string Type { get; private set; }
        public bool IsNullable { get; private set; }
    }
}
