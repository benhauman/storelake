namespace Helpline.Data.TestStore
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.Common;

    internal static class HelplineDatX
    {
        private static readonly IDictionary<string, Func<HelplineDataProceduresCommandExecuteHandler, Func<DataSet, DbCommand, int>>> s_e = InitializeE();

        private static Dictionary<string, Func<HelplineDataProceduresCommandExecuteHandler, Func<DataSet, DbCommand, int>>> InitializeE()
        {
            var dict = new Dictionary<string, Func<HelplineDataProceduresCommandExecuteHandler, Func<DataSet, DbCommand, int>>>(StringComparer.OrdinalIgnoreCase);
            dict.Add("hlsyssec_cache_refresh", x => x.hlsyssec_cache_refresh);
            return dict;
        }
        public static Func<DataSet, DbCommand, int> TryGetHandlerForCommandExecuteProcedureNonQuery(DataSet db, string procedureFullName)
        {
            Func<HelplineDataProceduresCommandExecuteHandler, Func<DataSet, DbCommand, int>> handler_reg;
            if (s_e.TryGetValue(procedureFullName, out handler_reg))
                return handler_reg(db.HelplineDataProceduresHandler()); // this
            return null;
        }

        private class HandlersTable
        {
            private IDictionary<string, Func<DataSet, DbCommand, int>> cached_handlers_exec = new SortedDictionary<string, Func<DataSet, DbCommand, int>>(StringComparer.OrdinalIgnoreCase);
            internal Func<DataSet, DbCommand, int> TryGetHandlerForProcedureExecuteNonQuery(string schemaName, string procedureName)
            {
                //Func<DataSet, DbCommand, int> handler;
                //if (!cached_handlers_exec.TryGetValue(procedureName, out handler))
                //{
                //    if (string.Equals(procedureName, "hlsyssec_canexecuteglobal", StringComparison.OrdinalIgnoreCase))
                //        return AddCachedHandler(procedureName,  db.HelplineDataProceduresHandler().hlsyssec_canexecuteglobal;
                //}
                return null;
            }
        }
    }
    /*
    static class HelplineDataExtensions_X
    {
        public static TDataSet RegisterDataSetModel<TDataSet>(TDataSet db) where TDataSet : DataSet
        {
            var table = new HelplineDataProcedureDataTable() { TableName = HelplineDataProcedureDataTable.HelplineDataProcedureTableName };
            db.Tables.Add(table);
            return db;
        }
        public static HelplineDataProcedures HelplineDataProcedures(this DataSet db)
        {
            return GetTable<HelplineDataProcedureDataTable>(db, HelplineDataProcedureDataTable.HelplineDataProcedureTableName).handler;
        }

        public static DataSet SetHandlerForHelplineDataProcedures<T>(this DataSet db) where T : HelplineDataProcedures, new()
        {
            GetTable<HelplineDataProcedureDataTable>(db, HelplineDataProcedureDataTable.HelplineDataProcedureTableName).handler = new T();
            return db;
        }

        private sealed class HelplineDataProcedureDataTable : DataTable
        {
            internal static string HelplineDataProcedureTableName = "__HelplineDataProcedureDataTable__";
            internal HelplineDataProcedures handler = new HelplineDataProcedures();
        }

        private static TTable GetTable<TTable>(DataSet ds, string tableName) where TTable : DataTable
        {
            TTable table = (TTable)ds.Tables[tableName];
            if (table == null)
            {
                throw new ArgumentException("Table [" + tableName + "] could not be found.", "tableName");
            }
            return table;
        }

    }

    public class HelplineDataProcedures
    {
        private readonly IDictionary<string, Func<DataSet, DbCommand, DbDataReader>> read_methods;

        public HelplineDataProcedures()
        {
            read_methods = InitializeReadHandlers();
        }

        private IDictionary<string, Func<DataSet, DbCommand, DbDataReader>> InitializeReadHandlers()
        {
            IDictionary<string, Func<DataSet, DbCommand, DbDataReader>> reg = new SortedDictionary<string, Func<DataSet, DbCommand, DbDataReader>>(StringComparer.OrdinalIgnoreCase);

            //RegisterReader("[dbo].[xxxx]", SomeComplicatedMultipleResultSetProc);
            reg.Add("[dbo].[xxxx]", SomeComplicatedMultipleResultSetProc);
            return reg;
        }

        //private static void RegisterReader(string key, Func<HelplineDataProcedures, Func<DataSet, DbCommand, DbDataReader>> fp)
        //{ 
        //    fp()
        //}

        //public class HelplineDataProceduresCommandExecuteHandler
        public virtual IEnumerable<bool> hlsyssec_canexecuteglobal(DataSet db, int agentid, int globalid)
        {
            throw new NotImplementedException();
        }

        public virtual DbDataReader SomeComplicatedMultipleResultSetProc(DataSet db, DbCommand cmd)
        {
            throw new NotImplementedException();
        }

        public virtual bool IsTweetValid(DataSet db, string Id)
        {
            return false;
        }
    }
    */
}