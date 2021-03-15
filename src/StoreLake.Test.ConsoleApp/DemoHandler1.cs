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

        public static DbDataReader GetAgentNameById(DataSet db, DbCommand cmd)
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

        public static DbDataReader GetAgentInfoById(DataSet db, DbCommand cmd)
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
                row[column_fullname] = ag.fullname;
                row[column_description] = ag.description;
                row[column_active] = ag.active;
                return row;
            }).ToArray();

            foreach (DataRow row in output_rows)
            {
                tb_table.Rows.Add(row);
            }
            return new DataTableReader(tb_table);
        }



        public static string GetAgentsDescriptionById(DataSet db, int id)
        {
            //int id = (int)cmd.Parameters["id"].Value;
            //throw new NotImplementedException("cnt:" + db.hlsysagent().Count);

            IEnumerable<string> output_rows = db.hlsysagent().Where(ag => ag.agentid == id).Select(ag =>
            {
                return ag.description;
            }).ToArray();

            return output_rows.Single();
        }

        public static IEnumerable<string> GetAllAgentNames(DataSet db)
        {

            IEnumerable<string> output_rows = db.hlsysagent().Select(ag =>
            {
                return ag.name;
            });

            return output_rows;
        }

        public static IEnumerable<AgentInfo> GetAllAgentInfos(DataSet db)
        {
            IEnumerable<AgentInfo> output_rows = db.hlsysagent().Select(ag =>
            {
                AgentInfo row = new AgentInfo();
                row.Id = ag.agentid;
                row.Name = ag.name;
                row.IsActive = ag.active != 0;
                return row;
            }).ToArray();
            return output_rows;
        }
    }
}