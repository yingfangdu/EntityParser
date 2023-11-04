namespace EntityParser
{
    public class Entity
    {
        public Entity(string name, string type, bool isNullable)
        {
            this.Name = name;
            this.Type = type;
            this.IsNullable = isNullable;

            this.RefineName = Utility.RefineEntityName(this.Name);
            this.CSharpType = Utility.SoapTypeToCSharpTypeMap(this.Type);
        }

        public string Name { get; private set; }
        public string Type { get; private set; }
        public string RefineName { get; private set; }
        public string CSharpType { get; private set; }
        public bool IsNullable { get; private set; }
    }
}
