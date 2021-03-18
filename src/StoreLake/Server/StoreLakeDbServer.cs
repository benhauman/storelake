using StoreLake.TestStore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;

namespace StoreLake.TestStore.Server
{
    public static class StoreLakeDbProviderFactoryExtensions
    {
        public static DbProviderFactory CreateDbProviderFactoryInstance(this StoreLakeDbServer server)
        {
            return StoreLakeDbProviderFactory.CreateInstance(x =>
            {
                x.CreateConnection_Override = server.CreateConnection;
            });
        }
    }
    public sealed class StoreLakeDbServer
    {
        private readonly Dictionary<string, DataSet> _dbs = new Dictionary<string, DataSet>();
        public StoreLakeDbServer(DataSet db)
        {
            this._dbs.Add(db.DataSetName.ToUpperInvariant(), db);
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
                ExecuteDbDataReader_Override = (cmd, cb) => HandleExecuteDbDataReader(cb, cmd),
                ExecuteNonQuery_Override = (cmd) => HandleExecuteNonQuery(cmd)
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



        private int HandleExecuteNonQuery(DbCommand cmd)
        {
            string databaseName = cmd.Connection.Database;
            if (!_dbs.TryGetValue(databaseName.ToUpperInvariant(), out DataSet db))
            //if (!string.Equals(_db.DataSetName, databaseName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Unknown datatabase [" + databaseName + "]");
            }
            Func<DataSet, DbCommand, int> handlerMethod = null;
            foreach (var handler in handlers)
            {
                Func<DataSet, DbCommand, int> x_handlerMethod = StoreLakeDao.TryWrite(handler, cmd);
                if (x_handlerMethod != null)
                {
                    if (handlerMethod != null)
                    {
                        // another handler for the same command text or the command text comparer is not unique enough 
                        throw new InvalidOperationException("Multiple handlers found for Command (" + cmd.Parameters.Count + "):" + cmd.CommandText);
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
                int res = handlerMethod(db, cmd);
                return res;
            }
            throw new NotImplementedException("SQL (" + cmd.Parameters.Count + "):" + cmd.CommandText);
        }

        private DbDataReader HandleExecuteDbDataReader(CommandBehavior cb, DbCommand cmd)
        {
            string databaseName = cmd.Connection.Database;
            if (!_dbs.TryGetValue(databaseName.ToUpperInvariant(), out DataSet db))
            //if (!string.Equals(_db.DataSetName, databaseName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Unknown datatabase [" + databaseName + "]");
            }
            Func<DataSet, DbCommand, DbDataReader> handlerMethod = null;
            foreach (var handler in handlers)
            {
                Func<DataSet, DbCommand, DbDataReader> x_handlerMethod = StoreLakeDao.TryRead(handler, cmd);
                if (x_handlerMethod != null)
                {
                    if (handlerMethod != null)
                    {
                        // another handler for the same command text or the command text comparer is not unique enough 
                        throw new InvalidOperationException("Multiple handlers found for Command (" + cmd.Parameters.Count + "):" + cmd.CommandText);
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
                DbDataReader res = handlerMethod(db, cmd);
                return res;
            }
            throw new NotImplementedException("SQL (" + cmd.Parameters.Count + "):" + cmd.CommandText);
        }

        public StoreLakeDbServer RegisterCommandHandlerFacade<THandler>(Type accessorType) where THandler : class, new()
        {
            return RegisterCommandHandlerMethods(accessorType, typeof(THandler));
        }

        public StoreLakeDbServer RegisterCommandHandlerMethods(Type accessorType, Type handlerType)
        {
            var mis = handlerType.GetMethods(
                //System.Reflection.BindingFlags.Public 
                //        | System.Reflection.BindingFlags.Instance
                //        | System.Reflection.BindingFlags.Static
                //        | System.Reflection.BindingFlags.FlattenHierarchy
                        );
            foreach (var mi in mis)
            {
                if (mi.DeclaringType == typeof(object))
                {
                    // skip 'Equals', 'ToString', 'GetHashCode'
                }
                else
                {
                    TypedMethodHandler handler = TryUseHandlerMethod(accessorType, handlerType, mi);
                    if (handler != null)
                    {
                        this.handlers.Add(handler);
                    }
                }
            }
            return this;
        }

        private static TypedMethodHandler TryUseHandlerMethod(Type accessorType, Type methodOwner, System.Reflection.MethodInfo mi)
        {
            System.Reflection.MethodInfo accessor_method = accessorType.GetMethod(mi.Name);
            if (accessor_method == null)
            {
                //return null; // this Handle Method does not handle accessor's method;
                throw new InvalidOperationException("Access method '" + mi.Name + "' on type '" + accessorType.Name + "' could not be found.");
            }

            var prms = mi.GetParameters();
            if (prms.Length == 0)
                return null;
            var prm0 = prms[0];
            IComparable handlerCommandText = StoreLakeDao.TryGetCommandText(methodOwner, mi.Name);
            if (handlerCommandText == null)
            {
                // no command handler here. try an accessor type - (from attribute?)?
                handlerCommandText = StoreLakeDao.TryGetCommandText(accessorType, mi.Name);
                if (handlerCommandText == null)
                {
                    //System.Reflection.MethodInfo accessor_method_x = accessorType.GetMethod(mi.Name);
                    if (accessor_method == null)
                    {
                        return null;
                    }

                    throw new InvalidOperationException(TypedMethodHandler.BuildMismatchMethodExpectionText(mi, accessor_method, "CommandText field '" + mi.Name + "CommandText' could not be found."));
                }
            }
            //if (prm0.ParameterType != typeof(DataSet))
            //    return null;



            var handler = new TypedMethodHandler(methodOwner, mi, handlerCommandText);
            handler.ValidateReadMethod(accessor_method);
            return handler;
        }
    }

}