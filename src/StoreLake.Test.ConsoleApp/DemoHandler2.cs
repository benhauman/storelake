using System.Data;
using Helpline.Data.TestStore;
using System.Linq;
using Helpline.Data;
using System.Collections.Generic;
using Microsoft.SqlServer.Server;
using System;
using StoreLake.TestStore.Database;

namespace ConsoleApp4
{
    //[System.ComponentModel.Description("")]
    internal sealed class DemoHandler2 : Helpline.Data.HelplineDataCommandHandlerFacade
    {
        public override bool CanExecute(DataSet db, int agentid, int globalid) // HL_ACCESS_EXECUTE: 0x0010
        {
            // INNER JOIN
            // for LEFT OUTER JOIN : https://stackoverflow.com/questions/267488/linq-to-sql-multiple-left-outer-joins

            var q = from ag in db.hlsysagenttogroup()
                    where ag.agentid == agentid
                    join gacl in db.hlsysglobalacl() on ag.groupid equals gacl.groupid
                    where gacl.id == globalid && gacl.accessmask == 0x0010
                    select true;

            if (q.Any())
            {
                return true;
            }

            /*
            var q = from ag in db.hlsysagenttogroup().Where(ag => ag.agentid == agentid)
                    join gacl in db.hlsysglobalacl().AsEnumerable() on ag.groupid equals gacl.groupid
                    select gacl;
            if (q.Any())
            {
                return true;
            }


            
            if ((from ag in db.hlsysagenttogroup().Where(ag => ag.agentid == agentid)
                 from gacl in db.hlsysglobalacl().Where(gacl => gacl.id == globalid && gacl.accessmask == 0x0010 && gacl.groupid == ag.groupid)
                 select gacl).Any())
            {
                return true;
            }


            

if ((from groupid in db.hlsysagenttogroup().Where(ag => ag.agentid == agentid).Select(ag => ag.groupid)
     join gacl in db.hlsysglobalacl().Where(gacl => gacl.id == globalid && gacl.accessmask == 0x0010) on new { groupid } equals new { gacl.groupid }
     select gacl).Any())
{
    return true;
}


foreach(var ag in db.hlsysagenttogroup().Where(ag => ag.agentid == agentid))
{
    foreach(var gacl in db.hlsysglobalacl().Where(gacl => gacl.id == globalid && gacl.accessmask == 0x0010 && gacl.groupid == ag.groupid))
    {
        return true;
    }
}
*/
            return false;
        }

        public override void AdministrationRefreshRelationModels(DataSet db)
        {
            // do nothing
        }
        public override int AddToWatchList(DataSet db, int agentid, IEnumerable<IntThreeSetRow> ids)
        {
            foreach(var id in ids)
            {
                db.hlsyswatchlist().AddRowWithValues(agentid, id.vb, id.vc, db.GetUtcDate());
            }

            return db.hlsyswatchlist().Count;
        }
        //public override int AddToWatchList(DataSet db, int agentid, IEnumerable<Tuple<int, int, int>> ids)
        //{
        //    //foreach (var id in ids.Select(x => new IntTreeSetRow(null)))
        //    //{
        //    //    id.va
        //    //}
        //        //Helpline.Data.IntThreeSet
        //        //return base.AddToWatchList(db, agentid, ids);
        //        return 999;
        //}
        //
        //public override int AddToWatchList(DataSet db, int agentid, IEnumerable<SqlDataRecord> ids)
        //{
        //    //System.Tuple<int, int, int>
        //    return base.AddToWatchList(db, agentid, ids);
        //}

        //public int AddToWatchListX(DataSet db, int agentid, IntThreeSet ids)
        //{
        //    return 1;
        //}
    }
}