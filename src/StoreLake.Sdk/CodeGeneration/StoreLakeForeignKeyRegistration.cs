using System.Collections.Generic;

namespace StoreLake.Sdk.CodeGeneration
{
    internal sealed class StoreLakeForeignKeyRegistration
    {
        public string DefiningTableName { get; internal set; }
        public string DefiningTableSchema { get; internal set; }
        public string ForeignTableName { get; internal set; }
        public string ForeignTableSchema { get; internal set; }

        public string ForeignKeyName { get; set; }
        public string ForeignKeySchema { get; set; }
        public List<StoreLakeKeyColumnRegistration> DefiningColumns { get; set; } = new List<StoreLakeKeyColumnRegistration>();

        public List<StoreLakeKeyColumnRegistration> ForeignColumns { get; set; } = new List<StoreLakeKeyColumnRegistration>();
    }
}
