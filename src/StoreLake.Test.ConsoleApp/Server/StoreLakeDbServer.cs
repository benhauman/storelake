using StoreLake.TestStore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;

namespace StoreLake.Test.ConsoleApp.Server
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

        internal StoreLakeDbServer RegisterHandlerReadWithCommandText(System.Linq.Expressions.Expression<Func<DataSet, DbCommand, DbDataReader>> handlerExpr)
        {
            CommandExecutionHandler handler = new CommandExecutionHandlerImpl(null, handlerExpr);
            handlers.Add(handler);
            return this;
        }

        internal StoreLakeDbServer RegisterHandlerReadForCommandText(Type commandTextOwner, System.Linq.Expressions.Expression<Func<DataSet, DbCommand, DbDataReader>> handlerExpr)
        {
            CommandExecutionHandler handler = new CommandExecutionHandlerImpl(commandTextOwner, handlerExpr);
            handlers.Add(handler);
            return this;
        }

        private readonly List<CommandExecutionHandler> handlers = new List<CommandExecutionHandler>();

        private DbDataReader ExecuteDbDataReader(CommandBehavior cb, DbCommand cmdx)
        {
            Func<DataSet, DbCommand, DbDataReader> handlerMethod = null;
            foreach (var handler in handlers)
            {
                Func<DataSet, DbCommand, DbDataReader> x_handlerMethod = StoreLakeDao.TryRead(handler, cmdx);
                if (x_handlerMethod != null)
                {
                    if (handlerMethod != null)
                    {
                        // another handler for the same command text or the command text comparer is not unique enough 
                        throw new InvalidOperationException("Multiple handlers found for Command (" + cmdx.Parameters.Count + "):" + cmdx.CommandText);
                    }

                    handlerMethod = x_handlerMethod;
                }
                else
                {
                    // this handler does not handles this command text
                }
            }

            if (handlerMethod != null)
            {
                DbDataReader res = handlerMethod(_db, cmdx);
                return res;
            }
            throw new NotImplementedException("SQL (" + cmdx.Parameters.Count + "):" + cmdx.CommandText);
        }


        internal StoreLakeDbServer RegTypedHandler(Type accessorType, Type handlerType)
        {
            var mis = handlerType.GetMethods();
            foreach(var mi in mis)
            {
                TypedMethodHandler handler = TryUseHandlerMethod(accessorType, handlerType, mi);
                if (handler != null)
                {
                    this.handlers.Add(handler);
                }
            }
            return this;
        }

        private static TypedMethodHandler TryUseHandlerMethod(Type accessorType, Type methodOwner, System.Reflection.MethodInfo mi)
        {
            var prms = mi.GetParameters();
            if (prms.Length == 0)
                return null;
            var prm0 = prms[0];
            if (prm0.ParameterType != typeof(DataSet))
                return null;
            IComparable handlerCommandText = StoreLakeDao.TryGetCommandText(methodOwner, mi.Name);
            if (handlerCommandText == null)
            {
                // no command handler here. try an accessor type - (from attribute?)?
                handlerCommandText = StoreLakeDao.TryGetCommandText(accessorType, mi.Name);
                if (handlerCommandText == null)
                {
                    return null;
                }
            }

            var handler = new TypedMethodHandler(methodOwner, mi, handlerCommandText);

            return handler;
        }
    }

}