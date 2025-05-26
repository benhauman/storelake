namespace StoreLake.Sdk.CodeGeneration
{
    using System.Data;
    using System.Diagnostics;

    [DebuggerDisplay("{ColumnName} {ColumnDbType} Null:{IsNullable} Identity:{IsIdentity}")]
    internal sealed class StoreLakeColumnRegistration
    {
        public string ColumnName { get; set; }
        public bool IsNullable { get; set; }
        public bool IsIdentity { get; set; }
        public SqlDbType ColumnDbType { get; set; }
    }
}
