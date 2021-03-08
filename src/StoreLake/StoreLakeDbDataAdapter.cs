using System;
using System.Data.Common;


namespace Dibix.TestStore
{
    public sealed class StoreLakeDbDataAdapter : DbDataAdapter
    {
        public StoreLakeDbDataAdapter()
        {

        }
        public Action<StoreLakeDbDataAdapter, System.Data.DataTable, StoreLakeDbCommand> FillTable_Setup { get; set; }
        protected override int Fill(System.Data.DataTable[] dataTables, int startRecord, int maxRecords, System.Data.IDbCommand command, System.Data.CommandBehavior behavior)
        {
            if (dataTables.Length == 1)
            {
                if (FillTable_Setup != null)
                {
                    foreach (var tbl in dataTables)
                    {
                        FillTable_Setup(this, tbl, (StoreLakeDbCommand)command);
                    }
                    return 0;
                }
            }
            throw new NotImplementedException();
            //return base.Fill(dataTables, startRecord, maxRecords, command, behavior);
        }

        internal DbCommand FillSchemaCommand { get; set; }

        protected override System.Data.DataTable[] FillSchema(System.Data.DataSet dataSet, System.Data.SchemaType schemaType, System.Data.IDbCommand command, string srcTable, System.Data.CommandBehavior behavior)
        {
            var cmd = FillSchemaCommand ?? command;
            return base.FillSchema(dataSet, schemaType, cmd, srcTable, behavior);
        }
    }
}