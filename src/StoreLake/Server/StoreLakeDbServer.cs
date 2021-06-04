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

        //internal StoreLakeDbServer RegisterHandlerReadWithCommandText(System.Linq.Expressions.Expression<Func<DataSet, DbCommand, DbDataReader>> handlerExpr)
        //{
        //    CommandExecutionHandler handler = new CommandExecutionHandlerImpl(null, handlerExpr);
        //    handlers.Add(handler);
        //    return this;
        //}

        //internal StoreLakeDbServer RegisterHandlerReadForCommandText(Type commandTextOwner, System.Linq.Expressions.Expression<Func<DataSet, DbCommand, DbDataReader>> handlerExpr)
        //{
        //    CommandExecutionHandler handler = new CommandExecutionHandlerImpl(commandTextOwner, handlerExpr);
        //    handlers.Add(handler);
        //    return this;
        //}

        private readonly List<CommandExecutionHandler> handlers_text = new List<CommandExecutionHandler>();
        private readonly IDictionary<string, CommandExecutionHandler> handlers_key = new SortedDictionary<string, CommandExecutionHandler>(StringComparer.OrdinalIgnoreCase);
        private void RegisterCommandExecutionHandler(CommandExecutionHandler handler)
        {
            if (handler.IsProcedureHandler())
            {
                string commandText = (string)handler.RetrieveCommandText();
                handlers_key.Add(commandText, handler);
            }
            else
            {
                this.handlers_text.Add(handler);
            }
        }


        private int HandleExecuteNonQuery(DbCommand cmd)
        {
            DataSet db = GetDatabaseForConnectionCore(cmd.Connection);
            Func<DataSet, DbCommand, int> handlerMethod = null;
            if (handlers_key.TryGetValue(cmd.CommandText, out CommandExecutionHandler handler_key))
            {
                handlerMethod = StoreLakeDao.TryWrite(handler_key, cmd);
                if (handlerMethod == null)
                {
                    throw new NotImplementedException(); // handler is registered for this commandtext but the method cannot be compiled?!?
                }
            }
            else
            {
                foreach (var handler_text in handlers_text)
                {
                    Func<DataSet, DbCommand, int> x_handlerMethod = StoreLakeDao.TryWrite(handler_text, cmd);
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

            string cmd_CommandText = cmd.CommandText;
            if (cmd.CommandType == CommandType.StoredProcedure)
            {
                //int indexOf_SchemaSeparator = cmd_CommandText.IndexOf('.');
                //if (indexOf_SchemaSeparator < 0)
                //{
                //    // no schema at all
                //}
                //string schemaName = cmd_CommandText.Substring(0, indexOf_SchemaSeparator);
                //string procedureName = cmd_CommandText.Substring(indexOf_SchemaSeparator + 1);
                //if (schemaName[0] != '[' || procedureName[0] != '[')
                //{
                //    if (schemaName[0] != '[')
                //        schemaName = '[' + schemaName + ']';
                //    if (procedureName[0] != '[')
                //        procedureName = '[' + procedureName + ']';
                //
                //    cmd_CommandText = schemaName + "." + procedureName;
                //}
            }

            Func<DataSet, DbCommand, DbDataReader> handlerMethod = null;
            if (handlers_key.TryGetValue(cmd.CommandText, out CommandExecutionHandler handler_key))
            {
                if (!handler_key.HasCompiledMethodRead())
                {
                    handlerMethod = handler_key.DoCompileReadMethod();
                    handler_key.SetCompiledReadMethod(handlerMethod);
                }
                else
                {
                    handlerMethod = handler_key.CompiledReadMethod();
                }

                //handlerMethod = StoreLakeDao.TryRead(handler_key, cmd_CommandText);
                if (handlerMethod == null)
                {
                    throw new NotImplementedException(); // handler is registered for this commandtext but the method cannot be compiled?!?
                }
            }
            else
            {
                foreach (var handler_text in handlers_text)
                {
                    Func<DataSet, DbCommand, DbDataReader> x_handlerMethod = StoreLakeDao.TryRead(handler_text, cmd);
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
                        bool isProcedureHandler = false;
                        if (handlerCommandText == null)
                        {
                            isProcedureHandler = true;
                            handlerCommandText = '[' + schemaName + "].[" + mi.Name + ']'; // procedure name
                        }
                        var handler = new TypedMethodHandler(mi, isProcedureHandler, handlerCommandText);
                        handler.ValidateReadMethod(mi);
                        RegisterCommandExecutionHandler(handler);
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
                        RegisterCommandExecutionHandler(handler);
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

            var handler = new TypedMethodHandler(mi, false, handlerCommandText);
            handler.ValidateReadMethod(accessor_method);
            return handler;
        }
    }

}