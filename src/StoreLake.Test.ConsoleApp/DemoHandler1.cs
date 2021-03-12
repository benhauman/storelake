using Helpline.Data.TestStore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;

namespace ConsoleApp4
{
    internal class DemoHandler1
    {
        private static readonly IComparable GetAgentNameByIdCommandText = new DynamicSqlTextCompare()
.OnFalse(cmd =>
{
    // put a breakpoint here
})
.And(cmd => cmd.Parameters.Count == 1)
.And(cmd => cmd.CommandText.Contains("SELECT name"))
.And(cmd => cmd.CommandText.Contains("hlsysagent"))
.And(cmd => cmd.CommandText.Contains("WHERE"))
.And(cmd => cmd.CommandText.Contains("@id"))

            ;

        internal static DbDataReader GetAgentNameById(DataSet db, DbCommand cmd)
        {
            int id = (int)cmd.Parameters["id"].Value;
            //throw new NotImplementedException("cnt:" + db.hlsysagent().Count);


            var tb_table = new DataTable();
            var column_name = new DataColumn("name", typeof(string));
            tb_table.Columns.Add(column_name);

            IEnumerable<DataRow> output_rows = db.hlsysagent().Where(ag => ag.agentid == id).Select(ag =>
            {
                DataRow row = tb_table.NewRow();
                row[column_name] = ag.name;
                return row;
            }).ToArray();

            foreach (DataRow row in output_rows)
            {
                tb_table.Rows.Add(row);
            }
            return new DataTableReader(tb_table);
        }
    }
}