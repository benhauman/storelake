using Microsoft.SqlServer.TransactSql.ScriptDom;
using System.Data;
using System.Diagnostics;

namespace StoreLake.Sdk.SqlDom
{
    [DebuggerDisplay("{DebuggerText}")]
    public sealed class ProcedureOutputColumn
    {
        private OutputColumnDescriptor columnDescriptor;
        private readonly TSqlFragment origin;
        internal ProcedureOutputColumn(TSqlFragment origin, OutputColumnDescriptor columnDbType)
        {
            this.origin = origin;
            this.columnDescriptor = columnDbType;

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
                if (columnDescriptor != null)
                    return columnDescriptor.ColumnDbType;
                return null;
            }
        }

        internal  string OutputColumnName// see 'PrepareOutputColumnName'
        {
            get
            {
                if (columnDescriptor != null)
                    return columnDescriptor.OutputOrSourceColumnName;
                return null;
            }
        }

        internal bool HasMissingInformation
        {
            get
            {
                return columnDescriptor == null 
                        || !columnDescriptor.ColumnDbType.HasValue
                        || string.IsNullOrEmpty(columnDescriptor.OutputOrSourceColumnName)
                        ;
            }
        }

        internal void ApplyMissingInformation(ProcedureOutputColumn procedureOutputColumn)
        {
            columnDescriptor = procedureOutputColumn.columnDescriptor;
        }
    }
}
