using Helpline.Data.TestStore;
using Helpline.Repository.DomainModel.UserInfo;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StoreLake.Test.ConsoleApp
{
    class DemoHandler2Repository : Helpline.Repository.Data.HelplineDataDatabaseAccessHandlerFacade
    {
        public override UserInfoResult GetUserInfo(DataSet db, int agentid)
        {
            UserInfoResult result = new UserInfoResult();
            //result.Agents.Add(new Agent() { Id = 1, Name = "ag1" });
            //result.Agents.Add(new Agent() { Id = 1, Name = "ag2" });

            // LEFT OUTER JOIN
            (from ag in db.hlsysagent()
                         from ab in db.hlsysagentroutingblacklist()
                                .Where(r_ab => r_ab.agentid == ag.agentid)
                                .DefaultIfEmpty()
                         select new Agent()
                         {
                             Id = ag.agentid,
                             Name = ag.name,
                             FullName = ag.fullname,
                             Description = ag.description,
                             IsActive = ag.active == 1,
                             HideForRouting = ab != null // IIF([ab].[agentid] IS NULL, 0, 1)
                         }
            ).ToList().ForEach(x => result.Agents.Add(x));
            /*foreach (hlsysagentRow ag in db.hlsysagent())
            {
                // [hlsysagentroutingblacklist]
                result.Agents.Add(new Agent()
                {
                    Id = ag.agentid,
                    Name = ag.name,
                    FullName = ag.fullname,
                    Description = ag.description,
                    IsActive = ag.active == 1,
                    HideForRouting = db.hlsysagentroutingblacklist().Any(ab => ab.agentid == ag.agentid)
                });
            }*/
            return result;
        }
    }
}
