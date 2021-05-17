using Helpline.Data.TestStore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StoreLake.Test.ConsoleApp
{
    class DemoHandler4 : HelplineDataProcedures
    {
        public override IEnumerable<bool> hlsyssec_canexecuteglobal(DataSet db, int agentid, int globalid)
        {
            return (from ag in db.hlsysagenttogroup()
                    where ag.agentid == agentid
                    join gacl in db.hlsysglobalacl() on ag.groupid equals gacl.groupid
                    where gacl.id == globalid && gacl.accessmask == 0x0010
                    select true).ToArray();
        }

        public override DbDataReader SomeComplicatedMultipleResultSetProc(DataSet db, DbCommand cmd)
        {
            var table_1 = new DataTable();
            var table_1_column_value = new DataColumn("value", typeof(int));
            table_1.Columns.Add(table_1_column_value);

            var table_2 = new DataTable();
            var table_2_column_value = new DataColumn("value", typeof(int));
            table_2.Columns.Add(table_2_column_value);

            return new DataTableReader(new DataTable[] { table_1, table_2 });
        }
    }
}
