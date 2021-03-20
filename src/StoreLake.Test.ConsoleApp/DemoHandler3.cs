using Helpline.Data.TestStore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp4
{
    public class HelplineDataAccessHandlerFacade
    {
        public virtual bool CanExecute(DataSet db, int agentid, int globalid)
        {
            throw new NotImplementedException();
        }
    }
    internal sealed class DemoHandler3 : HelplineDataAccessHandlerFacade
    {
        public override bool CanExecute(DataSet db, int agentid, int globalid)
        {
            var q = from ag in db.hlsysagenttogroup()
                    where ag.agentid == agentid
                    join gacl in db.hlsysglobalacl() on ag.groupid equals gacl.groupid
                    where gacl.id == globalid && gacl.accessmask == 0x0010
                    select true;

            if (q.Any())
            {
                return true;
            }

            return false;
        }
    }
}
