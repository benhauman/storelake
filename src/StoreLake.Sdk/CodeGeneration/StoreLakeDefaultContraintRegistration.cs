using System;

namespace StoreLake.Sdk.CodeGeneration
{
    internal sealed class StoreLakeDefaultContraintRegistration
    {
        public string ConstraintName { get; set; }
        public string ConstraintSchema { get; set; }
        public string TableSchema { get; set; }
        public string TableName { get; set; }
        public string ColumnName { get; set; }
        public bool IsScalarValue { get; internal set; }
        public string DefaultExpressionScript { get; internal set; }
        public bool IsBuiltInFunctionExpression { get; internal set; } // use 'DefaultExpressionScript'
        public int? ValueInt32 { get; internal set; }
        public string ValueString { get; internal set; }
        public DateTime? ValueDateTime { get; internal set; }
        public decimal? ValueDecimal { get; internal set; }

        public byte[] ValueBytes { get; internal set; }
    }
}
