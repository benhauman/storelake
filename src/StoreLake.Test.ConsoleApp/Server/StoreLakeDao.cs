using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace StoreLake.Test.ConsoleApp.Server
{
    internal static class StoreLakeDao
    {
        class HandlerReadDesc
        {
            internal readonly string MethodName;
            internal readonly Func<DataSet, DbCommand, DbDataReader> HandlerMethod;
            public HandlerReadDesc(string methodName, Func<DataSet, DbCommand, DbDataReader> handlerMethod)
            {
                this.MethodName = methodName;
                this.HandlerMethod = handlerMethod;
            }
        }
        private static SortedDictionary<string, HandlerReadDesc> s_map_CommandText_Read_Method = new SortedDictionary<string, HandlerReadDesc>();
        class HandlerExecDesc
        {
            internal readonly string MethodName;
            internal readonly Func<DataSet, DbCommand, int> HandlerMethod;
            public HandlerExecDesc(string methodName, Func<DataSet, DbCommand, int> handlerMethod)
            {
                this.MethodName = methodName;
                this.HandlerMethod = handlerMethod;
            }
        }
        private static SortedDictionary<string, HandlerExecDesc> s_map_CommandText_Exec_Method = new SortedDictionary<string, HandlerExecDesc>();

        internal static bool TryRead(object methodName, Type tAccessor, DataSet db, DbCommand cmdx, out DbDataReader res, Func<DataSet, DbCommand, DbDataReader> p)
        {
            throw new NotImplementedException();
        }

        internal static Func<DataSet, DbCommand, DbDataReader> TryRead(CommandExecutionHandler handlerRegistry, DbCommand cmd)
        {
            HandlerReadDesc handler;
            if (!s_map_CommandText_Read_Method.TryGetValue(cmd.CommandText, out handler))
            {
                // not yet cached
                IComparable handlerCommandText = handlerRegistry.RetrieveCommandText();// GetCommandTextImpl(tAccessor, handlerMethodName);

                string handlerCommandTextX = handlerCommandText as string;

                bool isOk;
                if (handlerCommandTextX == null)
                {
                    isOk = handlerCommandText.CompareTo(cmd) == 0;
                }
                else
                {
                    isOk = string.Equals(cmd.CommandText, handlerCommandTextX);
                }


                if (isOk)
                {
                    Func<DataSet, DbCommand, DbDataReader> handlerMethod = handlerRegistry.CompiledMReadethod();
                    handler = new HandlerReadDesc(handlerRegistry.RetrieveHandlerMethodName(), handlerMethod);
                    s_map_CommandText_Read_Method.Add(cmd.CommandText, handler);
                }
                else
                {
                    handler = null;
                }
            }
            else
            {
                // registered
            }

            if (handler != null && handler.MethodName == handlerRegistry.RetrieveHandlerMethodName())
            {

                return handler.HandlerMethod;
            }


            return null;
        }

        internal static bool TryWrite(Type tAccessor, DataSet db, DbCommand cmd, out int rslt, System.Linq.Expressions.Expression<Func<DataSet, DbCommand, int>> handlerExpr)
        {
            System.Linq.Expressions.MethodCallExpression methodCallExpr = (System.Linq.Expressions.MethodCallExpression)handlerExpr.Body;
            string handlerMethodName = methodCallExpr.Method.Name;

            HandlerExecDesc handler;
            if (!s_map_CommandText_Exec_Method.TryGetValue(cmd.CommandText, out handler))
            {
                // not registered
                IComparable handlerCommandText = GetCommandTextImpl(tAccessor, handlerMethodName);
                //string handlerCommandText = gct(handlerMethodName);// RepositoryDML_CommandText.RepositoryDML_GetCommandTextDML(handlerMethodName);

                string handlerCommandTextX = handlerCommandText as string;

                bool isOk;
                if (handlerCommandTextX == null)
                {
                    isOk = handlerCommandText.CompareTo(cmd) == 0;
                }
                else
                {
                    isOk = string.Equals(cmd.CommandText, handlerCommandTextX);
                }

                if (isOk)
                {
                    var parameter_db = Expression.Parameter(typeof(DataSet), "db");
                    var parameter_cmd = Expression.Parameter(typeof(DbCommand), "cmd");
                    MethodCallExpression call = Expression.Call(methodCallExpr.Method, parameter_db, parameter_cmd);// methodCallExpr.Arguments
                    Expression<Func<DataSet, DbCommand, int>> lambda = Expression.Lambda<Func<DataSet, DbCommand, int>>(call, parameter_db, parameter_cmd); // visitor.ExtractedParameters

                    Func<DataSet, DbCommand, int> handlerMethod = lambda.Compile();
                    handler = new HandlerExecDesc(handlerMethodName, handlerMethod);
                    s_map_CommandText_Exec_Method.Add(cmd.CommandText, handler);
                }
                else
                {
                    handler = null;
                }
            }
            else
            {
                // registered
            }

            if (handler != null && handler.MethodName == handlerMethodName)
            {
                rslt = handler.HandlerMethod(db, cmd);
                return true;
            }

            rslt = 0;
            return false;
        }

        private static SortedDictionary<string, IComparable> s_cache = new SortedDictionary<string, IComparable>();


        internal static IComparable GetCommandTextImpl(Type methodOwner, string methodName)
        {
            var commandText = TryGetCommandText(methodOwner, methodName);
            if (commandText == null)
            {
                string fieldName = methodName + "CommandText";
                throw new InvalidOperationException("Field not found:" + fieldName);
            }

            return commandText;
        }
        internal static IComparable TryGetCommandText(Type methodOwner, string methodName)
        {
            string key = methodOwner.Name + "#" + methodName;
            IComparable commandText;
            if (!s_cache.TryGetValue(key, out commandText))
            {
                var bf = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;

                var mi = methodOwner.GetMethod(methodName, bf);
                if (mi == null)
                {
                    // throw new InvalidOperationException("Method not found:" + methodName);
                }

                string fieldName = methodName + "CommandText";
                var fi = methodOwner.GetField(fieldName, bf);
                if (fi == null)
                {
                    var fis = methodOwner.GetFields(bf);
                    return null;
                }

                commandText = (IComparable)fi.GetValue(null);
                s_cache.Add(key, commandText);
            }

            return commandText;
        }
    }


    internal abstract class CommandExecutionHandler
    {
        internal abstract string RetrieveHandlerMethodName();
        internal abstract IComparable RetrieveCommandText();
        internal abstract Func<DataSet, DbCommand, DbDataReader> CompiledMReadethod();
    }

    [DebuggerDisplay("{DebuggerText}")]
    internal sealed class TypedMethodHandler : CommandExecutionHandler
    {
        private readonly Type _instanceType;
        private readonly MethodInfo _mi;
        private readonly IComparable _commandText;
        Func<DataSet, DbCommand, DbDataReader> _compiledMethod;
        public TypedMethodHandler(Type instanceType, MethodInfo mi, IComparable commandText)
        {
            _instanceType = instanceType;
            _mi = mi;
            _commandText = commandText;
        }
        internal string DebuggerText
        {
            get
            {
                var prms = _mi.GetParameters();
                StringBuilder debug_params = new StringBuilder();
                for (int ix = 1; ix < prms.Length; ix++)
                {
                    ParameterInfo method_prm = prms[ix];

                    string debug_method_prm; debug_method_prm = _builtinTypeAlias.TryGetValue(method_prm.ParameterType.FullName, out debug_method_prm) ? debug_method_prm : method_prm.ParameterType.FullName;
                    debug_params.Append(", " + debug_method_prm + " " + method_prm.Name);
                }

                string debug_ret; debug_ret = _builtinTypeAlias.TryGetValue(_mi.ReturnType.FullName, out debug_ret) ? debug_ret : _mi.ReturnType.FullName;
                return "public " + debug_ret + " " + _mi.Name + "(DataSet db" + debug_params.ToString() + ") of '" + _instanceType.Name + "'";
            }
        }

        internal override IComparable RetrieveCommandText()
        {
            return _commandText;
        }
        internal override string RetrieveHandlerMethodName()
        {
            return _mi.Name;
        }

        internal override Func<DataSet, DbCommand, DbDataReader> CompiledMReadethod()
        {
            return HandleRead;
        }

        private DbDataReader HandleRead(DataSet db, DbCommand cmd)
        {
            var prms = _mi.GetParameters();

            List<object> parameter_values = new List<object>();
            parameter_values.Add(db);
            if (prms.Length == 2 && prms[1].ParameterType == typeof(DbCommand))
            {
                parameter_values.Add(cmd);

                if (_compiledMethod == null)
                {
                    _compiledMethod = CommandExecutionHandlerImpl.CompileKnownMethodSignature(_mi.IsStatic, _mi, _instanceType);
                }

                if (_mi.IsStatic)
                {


                    return _compiledMethod(db, cmd);
                }
                else
                {
                    throw new NotImplementedException("public DbDataReader " + _mi.Name + "(DataSet db, DbCommand cmd) of '" + _instanceType.Name + "'");

                }
            }
            else
            {
                List<object> invoke_parameters = new List<object>();
                invoke_parameters.Add(db);

                StringBuilder debug_params = new StringBuilder();
                for (int ix = 1; ix < prms.Length; ix++)
                {
                    ParameterInfo method_prm = prms[ix];
                    var cmd_prm = cmd.Parameters[method_prm.Name];

                    object prm_value = GetTypedCommandParameter(cmd, method_prm);
                    invoke_parameters.Add(prm_value);

                    string debug_method_prm; debug_method_prm = _builtinTypeAlias.TryGetValue(method_prm.ParameterType.FullName, out debug_method_prm) ? debug_method_prm : method_prm.ParameterType.FullName;
                    debug_params.Append(", " + debug_method_prm + " " + method_prm.Name);
                }

                if (_mi.IsStatic)
                {
                    object returnValues = _mi.Invoke(null, invoke_parameters.ToArray());
                    DbDataReader result = CreateDbDataReaderFromSingleSetReturnValues(_mi.ReturnType, returnValues); // single/multiple result set(from dibix); enumerable or not; complex or not(names for columns?(from SQL/accessor)) 
                    return result;
                }
                else
                {

                    string debug_ret; debug_ret = _builtinTypeAlias.TryGetValue(_mi.ReturnType.FullName, out debug_ret) ? debug_ret : _mi.ReturnType.FullName;
                    throw new NotImplementedException("public " + debug_ret + " " + _mi.Name + "(DataSet db" + debug_params.ToString() + ") of '" + _instanceType.Name + "'");
                }
            }



        }

        private static DbDataReader CreateDbDataReaderFromSingleSetReturnValues(Type returnType, object returnValues)
        {
            if (returnValues == null)
            {
                var tb_table = new DataTable();
                var column_value = new DataColumn("value", returnType);
                tb_table.Columns.Add(column_value);
                // 
                return new DataTableReader(tb_table);
            }
            else
            {
                if (returnValues.GetType().IsArray)
                {
                    throw new NotImplementedException("array");
                }
                if (typeof(System.Collections.IEnumerable).IsAssignableFrom(returnValues.GetType()) && returnType.IsGenericType)
                {
                    var elementType = returnType.GetGenericArguments()[0];
                    if (elementType == typeof(string))
                    {
                        var tb_table = new DataTable();
                        var column_value = new DataColumn("value", elementType);
                        tb_table.Columns.Add(column_value);
                        System.Collections.IEnumerable ie = (System.Collections.IEnumerable)returnValues;
                        var etor = ie.GetEnumerator();
                        while(etor.MoveNext())
                        {
                            string value = (string)etor.Current;

                            DataRow row = tb_table.NewRow();
                            row[column_value] = (string)value;
                            tb_table.Rows.Add(row);
                        }
                        return new DataTableReader(tb_table);
                    }

                    throw new NotImplementedException("enumerable");
                }
                throw new NotImplementedException(returnValues.GetType().Name);
            }

        }

        private static object GetTypedCommandParameter(DbCommand cmd, ParameterInfo method_prm)
        {
            if (method_prm.ParameterType == typeof(int))
            {
                return cmd.GetCommandParameterInt32NotNull(method_prm.Name);
            }

            string debug_method_prm; debug_method_prm = _builtinTypeAlias.TryGetValue(method_prm.ParameterType.FullName, out debug_method_prm) ? debug_method_prm : method_prm.ParameterType.FullName;
            throw new NotImplementedException("Command parameter for { " + debug_method_prm + " " + method_prm.Name + " }");
        }

        private static IDictionary<string, string> _builtinTypeAlias = new SortedDictionary<string, string>() {
            { typeof(bool).FullName, "bool" },
            { typeof(byte).FullName, "byte" },
            { typeof(char).FullName, "char" },
            { typeof(Decimal).FullName, "decimal" },
            { typeof(double).FullName, "double" },
            { typeof(float).FullName, "float" }, // Single
            { typeof(int).FullName, "int" },
            { typeof(long).FullName, "long" },
            { typeof(short).FullName, "short" },
            { typeof(string).FullName, "string" },
    };
    }
    [DebuggerDisplay("{CommandTextOwner.Name}.{_handlerMethodName}")]
    internal sealed class CommandExecutionHandlerImpl : CommandExecutionHandler
    {
        internal readonly Type CommandTextOwner;
        internal readonly Type HandlerMethodOwner;
        private readonly string _handlerMethodName;
        internal readonly System.Linq.Expressions.MethodCallExpression MethodCallExpr;
        internal readonly bool HandlerIsStatic;
        public CommandExecutionHandlerImpl(Type commandTextOwner, System.Linq.Expressions.Expression<Func<DataSet, DbCommand, DbDataReader>> handlerExpr)
        {
            System.Linq.Expressions.MethodCallExpression methodCallExpr = (System.Linq.Expressions.MethodCallExpression)handlerExpr.Body;
            string handlerMethodName = methodCallExpr.Method.Name;

            HandlerMethodOwner = methodCallExpr.Method.DeclaringType;
            _handlerMethodName = handlerMethodName;
            MethodCallExpr = (System.Linq.Expressions.MethodCallExpression)handlerExpr.Body;
            HandlerIsStatic = methodCallExpr.Method.IsStatic;
            CommandTextOwner = commandTextOwner ?? HandlerMethodOwner;
        }

        internal override IComparable RetrieveCommandText()
        {
            IComparable handlerCommandText = StoreLakeDao.GetCommandTextImpl(this.CommandTextOwner, this._handlerMethodName); // or Invoker
            return handlerCommandText;
        }

        internal override Func<DataSet, DbCommand, DbDataReader> CompiledMReadethod()
        {
            return CompileKnownMethodSignature(HandlerIsStatic, MethodCallExpr.Method, HandlerMethodOwner);
        }

        internal static Func<DataSet, DbCommand, DbDataReader> CompileKnownMethodSignature(bool HandlerIsStatic, MethodInfo mi, Type methodOwner)
        {
            // !!!! **** lubo: get rid of the cmd argument inside of the 'methodCallExpr' - just dont use if for compilation***!!!
            var parameter_db = Expression.Parameter(typeof(DataSet), "db");
            var parameter_cmd = Expression.Parameter(typeof(DbCommand), "cmd");
            MethodCallExpression call;
            if (HandlerIsStatic)
            {
                call = Expression.Call(mi, parameter_db, parameter_cmd);// methodCallExpr.Arguments
            }
            else
            {
                Expression instance = Expression.New(methodOwner);
                call = Expression.Call(instance, mi, parameter_db, parameter_cmd);// methodCallExpr.Arguments
            }
            Expression<Func<DataSet, DbCommand, DbDataReader>> lambda = Expression.Lambda<Func<DataSet, DbCommand, DbDataReader>>(call, parameter_db, parameter_cmd); // visitor.ExtractedParameters
            Func<DataSet, DbCommand, DbDataReader> handlerMethod = lambda.Compile();
            return handlerMethod;
        }

        internal override string RetrieveHandlerMethodName()
        {
            return this._handlerMethodName;
        }
    }
}