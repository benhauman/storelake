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
    /*class DemoHandler4_CommandHandler : HelplineDataProceduresCommandExecuteHandler
    {
        public override DbDataReader hlsyssec_canexecuteglobal(DataSet db, DbCommand cmd)
        {
            return base.hlsyssec_canexecuteglobal(db, cmd);
        }

        public override DbDataReader hlsys_query_userinfo(DataSet db, DbCommand cmd)
        {
            return base.hlsys_query_userinfo(db, cmd);
        }
    }*/

    class DemoHandler4_FacadeHandler : HelplineDataProceduresHandlerFacade
    {
        public override CanExecuteResultSets CanExecute(DataSet db, CanExecuteResultSets output, int agentid, int globalid)
        {
            //if (db != null)
            //    throw new NotImplementedException();
            return output.AddRow(1);
        }
        public override GetUserInfoResultSets GetUserInfo(DataSet db, GetUserInfoResultSets output, int agentid)
        {
            return output.Set1AddRow(id: null, name: null, fullname: null, description: null, isactive: null, hideforrouting: null);
        }

        public override AddToWatchListResultSets AddToWatchList(DataSet db, AddToWatchListResultSets output, int agentid, IEnumerable<hlsys_udt_intthreesetRow> ids)
        {
            //return output.AddRow(value: null);
            return output.AddRow(value: 1);
        }

        public override void AdministrationRefreshRelationModels(DataSet db)
        {
            // do nothing
        }
    }

    /*
    class xDemoHandler4_CommandHandler : HelplineDataProcedures // see 'HelplineDataProceduresCommandExecuteHandler'
    {
        public override IEnumerable<bool> hlsyssec_canexecuteglobal(DataSet db, int agentid, int globalid)
        {
            if (db != null)
            {
                var res = base.hlsyssec_canexecuteglobal(db, agentid, globalid);
            }
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
    */

}
