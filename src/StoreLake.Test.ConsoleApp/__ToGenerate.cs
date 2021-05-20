﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;

namespace Helpline.Data.TestStore
{
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
}