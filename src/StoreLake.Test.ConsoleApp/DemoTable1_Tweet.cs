using System;
using System.Collections.Generic;
using System.Data;
using System.Runtime.CompilerServices;
using Helpline.Data.TestStore;
using StoreLake.TestStore.Database;

namespace StoreLake.Test.ConsoleApp
{
    public static class TweetExtension
    {
        #region Registration
        public static DataSet Register(DataSet db)
        {
            InitDataSetClass(db);
            return db;
        }
        #endregion

        public static TweetDataTable Tweet(this DataSet db)
        {
            return GetTable<TweetDataTable>(db);
        }

        #region Implementation
        private static TTable GetTable<TTable>(DataSet ds, [CallerMemberName] string tableName = "") where TTable : DataTable
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
            var table = new TweetDataTable();
            ds.Tables.Add(table);
        }


        #endregion
    }

    public sealed class TweetDataTable : TypedTableBase<TweetRow>
    {
        internal const string TweetDataTableTableName = "Tweet";
        internal readonly DataColumn IdColumn;

        public TweetDataTable()
        {
            base.TableName = TweetDataTableTableName;
            BeginInit();
            IdColumn = new DataColumn("id", typeof(int), null, MappingType.Element);
            base.Columns.Add(IdColumn);
            base.Constraints.Add(new UniqueConstraint("PK_" + this.TableName, IdColumn, true));
            EndInit();
        }

        protected override Type GetRowType()
        {
            return typeof(TweetRow); // called by NewRowArray (NewRecordFromArray=>NewRerordBase=>GrowRecordCapacity)
        }

        protected override DataRow NewRowFromBuilder(DataRowBuilder builder)
        {
            return new TweetRow(builder);
        }

        internal TweetRow FindRowByKey(string id)
        {
            return ((TweetRow)(this.Rows.Find(new object[] {
                        id})));
        }

        public TweetRow AddRowWithValues(string id)
        {
            TweetRow dbrow = ((TweetRow)(this.NewRow()));
            dbrow.ItemArray = new object[] {
                    id};
            this.Rows.Add(dbrow);
            return dbrow;
        }

        protected override void OnColumnChanging(DataColumnChangeEventArgs e)
        {
            ((TweetRow)e.Row).ValidateRow();
        }

    }

    public sealed class TweetRow : DataRow
    {
        private readonly TweetDataTable row_table;
        internal TweetRow(DataRowBuilder rb) : base(rb)
        {
            row_table = (TweetDataTable)base.Table;
        }

        public string Id { get { return (string)base[row_table.IdColumn]; } }

        internal void ValidateRow()
        {
            var db = row_table.DataSet;

            if (!db.HelplineDataProcedures().IsTweetValid(db, Id))
                throw new ConstraintException("CK_Tweet_Id");
        }
    }
}
