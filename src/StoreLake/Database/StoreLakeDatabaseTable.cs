using Dibix.TestStore.Database;
using System;
using System.Data;
using System.Runtime.CompilerServices;

namespace Dibix.TestStore.Database
{
    public sealed class StoreLakeDatabaseTable<TDatabase, TRow>
        where TDatabase : StoreLakeDatabase
        where TRow : StoreLakeDatabaseEntityBaseT<TDatabase>, new()
    {
        private readonly TDatabase _db;
        private readonly string _tableName;
        internal StoreLakeDatabaseTable(TDatabase db, [CallerMemberName] string tableName = "")
        {
            _db = db;
            _tableName = tableName;
        }

        public TRow InsertInto(Action<TRow> setup)
        {
            DataTable table = _db.GetTableByName(_tableName);
            if (table == null)
            {
                throw new InvalidOperationException("Table '" + _tableName + "' could not be found.");
            }

            DataRow dbrow = table.NewRow();

            TRow entity = new TRow();
            entity.SetupRow(_db, dbrow, true);

            if (setup != null)
            {
                setup(entity);
            }

            table.Rows.Add(dbrow);
            entity.RowAttached();

            return entity;

        }
    }
}
