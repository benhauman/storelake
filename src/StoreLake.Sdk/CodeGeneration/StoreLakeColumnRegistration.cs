using System.Data;

namespace Dibix.TestStore.Database
{
    internal sealed class StoreLakeColumnRegistration
    {
        public string ColumnName { get; set; }
        public bool IsNullable { get; set; }
        public SqlDbType ColumnDbType { get; set; }
    }

}
