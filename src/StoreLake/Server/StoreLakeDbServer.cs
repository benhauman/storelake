using StoreLake.TestStore;
using StoreLake.TestStore.Database;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
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
        private readonly IDictionary<string, DataSet> _dbs = new SortedDictionary<string, DataSet>(StringComparer.OrdinalIgnoreCase);
        public StoreLakeDbServer(DataSet db)
        {
            this._dbs.Add(db.DataSetName, db);
        }

        internal StoreLakeDbConnection CreateConnection(StoreLakeDbProviderFactory dbClient)
        {
            return new StoreLakeDbConnection(dbClient)
            {
                CreateCommand_Override = (connection) => CreateCommand((StoreLakeDbConnection)connection)
            };
        }

        public DataSet GetDatabaseForConnection(DbConnection connection)
        {
            return GetDatabaseForConnectionCore(connection);
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
            DataSet db = GetDatabaseForConnectionCore(cmd.Connection);
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


        private DataSet GetDatabaseForConnectionCore(DbConnection connection)
        {
            string databaseName = connection.Database;
            if (!_dbs.TryGetValue(databaseName, out DataSet db))
            //if (!string.Equals(_db.DataSetName, databaseName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Unknown datatabase [" + databaseName + "]");
            }

            return db;
        }

        private DbDataReader HandleExecuteDbDataReader(CommandBehavior cb, DbCommand cmd)
        {
            string databaseName = cmd.Connection.Database;
            if (string.IsNullOrEmpty(databaseName) && _dbs.Count == 1)
            {
                databaseName = _dbs.Keys.ElementAt(0);
            }

            if (!_dbs.TryGetValue(databaseName, out DataSet db))
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

        //public void RegisterAddedCommandHandlerContracts(DataSet db)
        //{
        //    Type[] contracts = DatabaseCommandExecuteHandlerExtensionZ.CollectRegisteredCommandHandlerContracts(db).ToArray();
        //    foreach (Type contractType in contracts)
        //    {
        //        Type implementationType = db.GetCommandExecuteHandlerTypeForContract(contractType);
        //        RegisterAddedCommandHandlerContract(db, contractType, implementationType);
        //    }
        //}
        //public void RegisterAddedCommandHandlerContract<T>(DataSet db) where T : class, new()
        //{
        //    RegisterAddedCommandHandlerContract(db, typeof(T), typeof(T));
        //}
        public void RegisterAddedCommandHandlerContract<T>(DataSet db, T procedures) where T : class, new()
        {
            RegisterAddedCommandHandlerContract(db, procedures.GetType(), procedures.GetType());
        }
        public void RegisterAddedCommandHandlerContract(DataSet db, Type contractType, Type implementationType)
        {
            //foreach (Type contractType in contracts)
            {
                //Type implementationType = db.GetCommandExecuteHandlerTypeForContract(contractType);
                if (!contractType.IsAssignableFrom(implementationType))
                {
                    throw new InvalidOperationException("Handler type does not match registered contract type. Expected:" + contractType.AssemblyQualifiedName + ", Actual:" + implementationType.AssemblyQualifiedName);
                }

                //string schemaName = db.GetCommandExecuteHandlerSchemaForContract(contractType);
                string schemaName = string.IsNullOrEmpty(db.Namespace) ? "dbo" : db.Namespace;

                Type methodOwner = contractType;

                foreach (var mi in implementationType.GetMethods())
                {
                    if (mi.DeclaringType == typeof(object))
                    {
                        // skip
                    }
                    else
                    {
                        IComparable handlerCommandText = StoreLakeDao.TryGetCommandText(methodOwner, mi.Name);
                        if (handlerCommandText == null)
                        {
                            handlerCommandText = schemaName + "." + mi.Name; // procedure name
                        }
                        var handler = new TypedMethodHandler(mi, handlerCommandText);
                        handler.ValidateReadMethod(mi);
                        this.handlers.Add(handler);
                    }
                }
            }
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



            var handler = new TypedMethodHandler(mi, handlerCommandText);
            handler.ValidateReadMethod(accessor_method);
            return handler;
        }
    }

}