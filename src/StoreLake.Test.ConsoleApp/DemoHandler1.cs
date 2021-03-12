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
.And(cmd => cmd.CommandText.Contains("SELECT name FROM"))
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

        internal static DbDataReader GetAgentInfoById(DataSet db, DbCommand cmd)
        {
            int id = (int)cmd.Parameters["id"].Value;
            //throw new NotImplementedException("cnt:" + db.hlsysagent().Count);


            var tb_table = new DataTable();
            var column_name = new DataColumn("name", typeof(string));
            tb_table.Columns.Add(column_name);
            var column_fullname = new DataColumn("fullname", typeof(string));
            tb_table.Columns.Add(column_fullname);
            var column_description = new DataColumn("description", typeof(string));
            tb_table.Columns.Add(column_description);
            var column_active = new DataColumn("active", typeof(short));
            tb_table.Columns.Add(column_active);

            IEnumerable<DataRow> output_rows = db.hlsysagent().Where(ag => ag.agentid == id).Select(ag =>
            {
                DataRow row = tb_table.NewRow();
                row[column_name] = ag.name;
                row[column_fullname] = null;// ag.IsNull ag.fullname;
                row[column_description] = null;// ag.description;
                row[column_active] = ag.active;
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