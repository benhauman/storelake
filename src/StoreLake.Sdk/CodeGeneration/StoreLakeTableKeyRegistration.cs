using System.Collections.Generic;

namespace Dibix.TestStore.Database
{
    internal sealed class StoreLakeTableKeyRegistration
    {
        public string TableSchema { get; set; }
        public string TableName { get; set; }

        public string KeyName { get; set; }
        public string KeySchema { get; set; }
        public List<StoreLakeKeyColumnRegistration> Columns { get; set; } = new List<StoreLakeKeyColumnRegistration>();
    }

}
