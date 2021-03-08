using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dibix.TestStore.Database
{
    internal interface IValidadatableDatabaseEntity<TDatabase>
        where TDatabase : StoreLakeDatabase
    {
        void ValidateDatabaseEntity(TDatabase db);
    }
}
