﻿using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace StoreLake.Sdk.SqlDom
{
    public sealed class OutputColumnDescriptor
    {
        public string OutputColumnName { get; private set; }
        internal readonly string SourceColumnName;
        public readonly System.Data.DbType ColumnDbType;
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

        internal OutputColumnDescriptor SetOutputColumnName(IdentifierOrValueExpression columnName)
        {
            if (columnName != null)
            {
                this.OutputColumnName = columnName.Value;
            }

            return this;
        }
    }
}