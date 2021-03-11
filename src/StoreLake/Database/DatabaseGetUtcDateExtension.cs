using System;
using System.Data;
using System.Linq;

namespace Dibix.TestStore.Database
{
    public static class DatabaseGetUtcDateExtension
    {
        public static DateTime GetUtcDate<TDataSet>(this TDataSet ds) where TDataSet : DataSet
        {
            return GetTable<TimeNowDataTable>(ds, TimeNowDataTable.TimeNowTableName).Single().TimeNow;
        }

        #region Registration
        // introduces a special table '__timenow__' with a datetimne column NOT NULL and one row with initial value
        public static void Register(DataSet ds)
        {
            ds.BeginInit();
            InitDataSetClass(ds);

            // PostDeploy
            DateTime db_utc_now = new DateTime(2001, 2, 3, 4, 5, 6, 789, DateTimeKind.Utc);
            var table = GetTable<TimeNowDataTable>(ds, TimeNowDataTable.TimeNowTableName);
            table.Rows.Add(1, db_utc_now);

            ds.EndInit();
        }
        #endregion

        #region Implementation
        private static TTable GetTable<TTable>(DataSet ds, string tableName) where TTable : DataTable
        {
            TTable table = (TTable)ds.Tables[tableName];
            if (table == null)
            {
                throw new ArgumentException("Table [" + tableName + "] could not be found.", "tableName");
            }
            return table;
        }


        private static void InitDataSetClass<TDataSet>(TDataSet ds) where TDataSet : DataSet
        {
            var table = new TimeNowDataTable();
            ds.Tables.Add(table);
        }

        private sealed class TimeNowDataTable : TypedTableBase<TimeNowRow>
        {
            internal const string TimeNowTableName = "__timenow__";
            internal readonly DataColumn TimeNowColumn;
            public TimeNowDataTable()
            {
                base.TableName = TimeNowTableName;
                BeginInit();
                var idColumn = new DataColumn("id", typeof(int), null, MappingType.Element);
                base.Columns.Add(idColumn);
                TimeNowColumn = new DataColumn("timenow", typeof(DateTime), null, MappingType.Element);
                base.Columns.Add(TimeNowColumn);
                EndInit();
            }

            protected override Type GetRowType()
            {
                return typeof(TimeNowRow); // called by NewRowArray (NewRecordFromArray=>NewRerordBase=>GrowRecordCapacity)
            }

            protected override DataRow NewRowFromBuilder(DataRowBuilder builder)
            {
                return new TimeNowRow(builder);
            }
        }

        private sealed class TimeNowRow : DataRow
        {
            private readonly TimeNowDataTable row_table;
            internal TimeNowRow(DataRowBuilder rb) : base(rb)
            {
                row_table = (TimeNowDataTable)base.Table;
            }

            public DateTime TimeNow
            {
                get
                {
                    return (DateTime)base[row_table.TimeNowColumn];
                }
                set
                {
                    base[row_table.TimeNowColumn] = value;
                }
            }
        }
        #endregion
    }
}
