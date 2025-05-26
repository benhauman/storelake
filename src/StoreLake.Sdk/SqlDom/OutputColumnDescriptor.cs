namespace StoreLake.Sdk.SqlDom
{
    using System.Data;
    using Microsoft.SqlServer.TransactSql.ScriptDom;

    public sealed class OutputColumnDescriptor
    {
        public string OutputColumnName { get; private set; }
        internal readonly string SourceColumnName;
        public readonly ColumnTypeMetadata ColumnType; // null: not resolved
        public OutputColumnDescriptor(ColumnTypeMetadata columnType)
        {
            this.ColumnType = columnType;
        }
        public OutputColumnDescriptor(DbType columnDbType, bool allowNull)
            : this(new ColumnTypeMetadata(columnDbType, allowNull))
        {
        }
        public OutputColumnDescriptor(string sourceColumnName, ColumnTypeMetadata columnType)
        {
            this.OutputColumnName = sourceColumnName;
            this.SourceColumnName = sourceColumnName;
            this.ColumnType = columnType;
        }
        public OutputColumnDescriptor(string sourceColumnName)
        {
            this.OutputColumnName = sourceColumnName;
            this.SourceColumnName = sourceColumnName;
            this.ColumnType = null;
        }

        internal OutputColumnDescriptor SetOutputColumnName(IdentifierOrValueExpression columnName)
        {
            if (columnName != null)
            {
                this.OutputColumnName = columnName.Value;
            }

            return this;
        }

        internal string OutputOrSourceColumnName
        {
            get
            {
                if (!string.IsNullOrEmpty(this.OutputColumnName))
                {
                    return this.OutputColumnName.Trim();
                }
                if (!string.IsNullOrEmpty(this.SourceColumnName))
                {
                    return this.SourceColumnName.Trim();
                }

                return null;
            }
        }
    }
}
