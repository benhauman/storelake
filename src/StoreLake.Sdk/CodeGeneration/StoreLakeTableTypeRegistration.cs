using System.Collections.Generic;
using System.Diagnostics;

namespace StoreLake.Sdk.CodeGeneration
{
    [DebuggerDisplay("{TableTypeSchema}.{TableTypeName}")]
    internal class StoreLakeTableTypeRegistration
    {
        public string TableTypeSqlFullName { get; set; }
        public string TableTypeSchema { get; set; }
        public string TableTypeName { get; set; }

        public List<StoreLakeColumnRegistration> Columns { get; set; } = new List<StoreLakeColumnRegistration>();

        public StoreLakeTableTypeKey PrimaryKey { get; set; } = new StoreLakeTableTypeKey();

        public string TableTypeDefinitionName { get; set; }

    }

    internal sealed class StoreLakeTableTypeKey
    {
        public List<string> ColumnNames { get; set; } = new List<string>();
    }
}