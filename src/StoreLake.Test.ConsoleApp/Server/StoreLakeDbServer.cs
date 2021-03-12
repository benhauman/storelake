using StoreLake.TestStore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;

namespace ConsoleApp4
{
    internal class StoreLakeDbServer
    {
        private readonly DataSet _db;
        public StoreLakeDbServer(DataSet db)
        {
            this._db = db;
        }

        internal StoreLakeDbConnection CreateConnection(StoreLakeDbProviderFactory dbClient)
        {
            return new StoreLakeDbConnection(dbClient)
            {
                CreateCommand_Override = (connection) => CreateCommand((StoreLakeDbConnection)connection)
            };
        }

        internal DbCommand CreateCommand(StoreLakeDbConnection connection)
        {
            return new StoreLakeDbCommand(connection)
            {
                ExecuteDbDataReader_Override = (cmd, cb) => ExecuteDbDataReader(cb, cmd)
            };
        }


        private readonly List<CommandExecutionHandler> handlers = new List<CommandExecutionHandler>();

        private DbDataReader ExecuteDbDataReader(CommandBehavior cb, DbCommand cmdx)
        {
            handlers.Clear();
            {
                CommandExecutionHandler handler = new CommandExecutionHandler((d, c) => DemoHandler1.GetAgentNameById(d, c));
                handlers.Add(handler);
            }

            foreach (var handler in handlers)
            {
                Func<DataSet, DbCommand, DbDataReader> handlerMethod = StoreLakeDao.TryRead(handler, cmdx);
                if (handlerMethod != null)
                {
                    DbDataReader res = handlerMethod(_db, cmdx);
                    return res;
                }
            }

            throw new NotImplementedException("SQL (" + cmdx.Parameters.Count + "):" + cmdx.CommandText);
        }
    }

}