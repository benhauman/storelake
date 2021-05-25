using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Data;
using System.Diagnostics;

namespace StoreLake.Sdk.SqlDom
{
    [DebuggerDisplay("{DebuggerText}")]
    public sealed class ProcedureOutputColumn
    {
        private OutputColumnDescriptor columnDbType;
        private readonly TSqlFragment origin;
        internal ProcedureOutputColumn(TSqlFragment origin, OutputColumnDescriptor columnDbType)
        {
            this.origin = origin;
            this.columnDbType = columnDbType;

            if (columnDbType == null)
            {
                // put breakpoint here
                DebuggerText = "???";
            }
            else
            {
                DebuggerText = (columnDbType.OutputColumnName != null ? columnDbType.OutputColumnName : null)
                    + " " + (columnDbType.SourceColumnName != null ? "(" + columnDbType.SourceColumnName + ")" : null)
                    + " : (" + columnDbType.ColumnDbType + ")";
            }
        }

        private readonly string DebuggerText;

        public DbType? ColumnDbType
        {
            get
            {
                if (columnDbType != null)
                    return columnDbType.ColumnDbType;
                return null;
            }
        }

        internal bool HasMissingInformation
        {
            get
            {
                return columnDbType == null;
            }
        }

        internal void ApplyMissingInformation(ProcedureOutputColumn procedureOutputColumn)
        {
            columnDbType = procedureOutputColumn.columnDbType;
        }
    }
}
