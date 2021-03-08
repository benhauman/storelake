using Dibix.TestStore.Database;
using System;
using System.Data;
using System.Text;
using System.Threading.Tasks;

namespace Dibix.TestStore.Demo
{
    public sealed class DemoTestDatabase : StoreLakeDatabase
    {
        public DemoTestDatabase(DataSet ds) : base(ds)
        {
        }
    }
}

