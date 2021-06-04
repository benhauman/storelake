using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace StoreLake.Sdk.SqlDom
{
    public sealed class OutputColumnDescriptor
    {
        public string OutputColumnName { get; private set; }
        internal readonly string SourceColumnName;
        public readonly System.Data.DbType? ColumnDbType; // null: not resolved
        public OutputColumnDescriptor(System.Data.DbType columnDbType)
        {
            this.ColumnDbType = columnDbType;
        }
        public OutputColumnDescriptor(string sourceColumnName, System.Data.DbType columnDbType)
        {
            this.OutputColumnName = sourceColumnName;
            this.SourceColumnName = sourceColumnName;
            this.ColumnDbType = columnDbType;
        }
        public OutputColumnDescriptor(string sourceColumnName)
        {
            this.OutputColumnName = sourceColumnName;
            this.SourceColumnName = sourceColumnName;
            this.ColumnDbType = null;
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
