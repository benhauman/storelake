using Microsoft.SqlServer.Server;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace StoreLake.TestStore.Server
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
                    Func<DataSet, DbCommand, DbDataReader> handlerMethod = handlerRegistry.CompiledReadMethod();
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


        internal static Func<DataSet, DbCommand, int> TryWrite(CommandExecutionHandler handlerRegistry, DbCommand cmd)
        {
            HandlerExecDesc handler;
            if (!s_map_CommandText_Exec_Method.TryGetValue(cmd.CommandText, out handler))
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
                    Func<DataSet, DbCommand, int> handlerMethod = handlerRegistry.CompiledExecMethod();
                    handler = new HandlerExecDesc(handlerRegistry.RetrieveHandlerMethodName(), handlerMethod);
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

            if (handler != null && handler.MethodName == handlerRegistry.RetrieveHandlerMethodName())
            {

                return handler.HandlerMethod;
            }


            return null;
        }

        internal static bool TryWriteX(Type tAccessor, DataSet db, DbCommand cmd, out int rslt, System.Linq.Expressions.Expression<Func<DataSet, DbCommand, int>> handlerExpr)
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
        internal abstract Func<DataSet, DbCommand, DbDataReader> CompiledReadMethod();
        internal abstract Func<DataSet, DbCommand, int> CompiledExecMethod();
    }

    [DebuggerDisplay("{DebuggerText}")]
    internal sealed class TypedMethodHandler : CommandExecutionHandler
    {
        private readonly Type _instanceType;
        private readonly MethodInfo _mi;
        private readonly IComparable _commandText;
        Func<DataSet, DbCommand, DbDataReader> _compiledMethod_Read;
        Func<DataSet, DbCommand, int> _compiledMethod_Exec;
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
                return BuildMethodDeclarationAsText(_mi);
            }
        }

        internal static string BuildMethodDeclarationAsText(MethodInfo mi)
        {
            var prms = mi.GetParameters();
            StringBuilder debug_params = new StringBuilder();
            for (int ix = 0; ix < prms.Length; ix++)
            {
                ParameterInfo method_prm = prms[ix];
                if (ix == 0)
                {
                    string debug_method_prm;
                    if (method_prm.ParameterType == typeof(DataSet))
                    {
                        debug_method_prm = "DataSet";
                    }
                    else
                    {
                        debug_method_prm = TypeNameAsText(method_prm.ParameterType);
                    }

                    debug_params.Append("" + debug_method_prm + " " + method_prm.Name);
                }
                else
                {
                    string debug_method_prm; debug_method_prm = TypeNameAsText(method_prm.ParameterType);
                    debug_params.Append(", " + debug_method_prm + " " + method_prm.Name);
                }
            }

            string debug_ret; debug_ret = TypeNameAsText(mi.ReturnType);
            return "public " + debug_ret + " " + mi.Name + "(" + debug_params.ToString() + ") of '" + mi.DeclaringType.Name + "'";
        }

        internal override IComparable RetrieveCommandText()
        {
            return _commandText;
        }
        internal override string RetrieveHandlerMethodName()
        {
            return _mi.Name;
        }

        internal override Func<DataSet, DbCommand, DbDataReader> CompiledReadMethod()
        {
            return HandleRead;
        }

        internal override Func<DataSet, DbCommand, int> CompiledExecMethod()
        {
            return HandleExec;
        }

        internal static string BuildMismatchMethodExpectionText(System.Reflection.MethodInfo a_method, System.Reflection.MethodInfo h_method, string reason)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("Handle method does not match accessor's method signature.");
            text.AppendLine("Reason:" + reason);
            text.AppendLine("Access:" + BuildMethodDeclarationAsText(a_method));
            text.AppendLine("Handle:" + BuildMethodDeclarationAsText(h_method));
            return text.ToString();

        }

        internal void ValidateReadMethod(System.Reflection.MethodInfo a_method)
        {
            var h_method = _mi;

            var h_prms = h_method.GetParameters();
            if (h_prms.Length == 0)
            {
                throw new InvalidOperationException(BuildMismatchMethodExpectionText(a_method, h_method, "Handle method does not have parameters."));
            }

            if (h_method.ReturnType == typeof(DbDataReader)) // raw Handler?
            {
                if (h_prms.Length == 2
                    && h_prms[0].ParameterType == typeof(DataSet)
                    && h_prms[1].ParameterType == typeof(DbCommand)
                    )
                {
                    // raw handler 
                    return;
                }
                else
                {
                    throw new InvalidOperationException(BuildMismatchMethodExpectionText(a_method, h_method, "Handle method does not expected raw parameters (DataSet, DbCommand)."));
                }
            }

            else
            {

                if (a_method.ReturnType != h_method.ReturnType)
                {
                    throw new InvalidOperationException(BuildMismatchMethodExpectionText(a_method, h_method, "Wrong return type."));
                }


                var a_prms = a_method.GetParameters();
                if (a_prms.Length != h_prms.Length)
                {
                    throw new InvalidOperationException(BuildMismatchMethodExpectionText(a_method, h_method, "Parameters cound mismatched."));
                }

                for (int ix = 0; ix < h_prms.Length; ix++)
                {
                    ParameterInfo h_prm = h_prms[ix];
                    ParameterInfo a_prm = a_prms[ix];
                    if (ix == 0)
                    {
                        if (h_prm.ParameterType != typeof(DataSet))
                        {
                            throw new InvalidOperationException(BuildMismatchMethodExpectionText(a_method, h_method, "First parameter has a wrong type."));
                        }
                    }
                    else
                    {
                        if (h_prm.ParameterType != a_prm.ParameterType)
                        {
                            if (typeof(IEnumerable<SqlDataRecord>).IsAssignableFrom(a_prm.ParameterType))
                            {
                                // udt
                            }
                            else
                            {
                                throw new InvalidOperationException(BuildMismatchMethodExpectionText(a_method, h_method, "Wrong handle parameter '" + h_prm.ParameterType + "' type at position:" + (ix + 1) + "/" + h_prms.Length));
                            }
                        }
                    }
                }
            }
        }

        private int HandleExec(DataSet db, DbCommand cmd)
        {
            var prms = _mi.GetParameters();

            List<object> parameter_values = new List<object>();
            parameter_values.Add(db);
            if (prms.Length == 2 && prms[1].ParameterType == typeof(DbCommand))
            {
                parameter_values.Add(cmd);

                if (_compiledMethod_Exec == null)
                {
                    _compiledMethod_Exec = CommandExecutionHandlerImpl.CompileKnownMethodSignatureExec(_mi.IsStatic, _mi, _instanceType);
                }

                if (_mi.IsStatic)
                {


                    return _compiledMethod_Exec(db, cmd);
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

                    string debug_method_prm; debug_method_prm = TypeNameAsText(method_prm.ParameterType);
                    debug_params.Append(", " + debug_method_prm + " " + method_prm.Name);
                }

                if (_mi.IsStatic)
                {
                    object returnValues = _mi.Invoke(null, invoke_parameters.ToArray());
                    return returnValues == null ? 0 : (int)returnValues;
                }
                else
                {
                    object handlerInstance = Activator.CreateInstance(this._instanceType);
                    object returnValues = _mi.Invoke(handlerInstance, invoke_parameters.ToArray());

                    return returnValues == null ? 0 : (int)returnValues;
                    //string debug_ret; debug_ret = TypeNameAsText(_mi.ReturnType);
                    //throw new NotImplementedException("public " + debug_ret + " " + _mi.Name + "(DataSet db" + debug_params.ToString() + ") of '" + _instanceType.Name + "'");
                }
            }
        }

        private DbDataReader HandleRead(DataSet db, DbCommand cmd)
        {
            var prms = _mi.GetParameters();

            List<object> parameter_values = new List<object>();
            parameter_values.Add(db);
            if (prms.Length == 2 && prms[1].ParameterType == typeof(DbCommand))
            {
                parameter_values.Add(cmd);

                if (_compiledMethod_Read == null)
                {
                    _compiledMethod_Read = CommandExecutionHandlerImpl.CompileKnownMethodSignatureRead(_mi.IsStatic, _mi, _instanceType);
                }

                if (_mi.IsStatic)
                {


                    return _compiledMethod_Read(db, cmd);
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

                    string debug_method_prm; debug_method_prm = TypeNameAsText(method_prm.ParameterType);
                    debug_params.Append(", " + debug_method_prm + " " + method_prm.Name);
                }

                if (_mi.IsStatic)
                {
                    object returnValues = _mi.Invoke(null, invoke_parameters.ToArray());
                    DataTable[] result_tables = CreateDbDataReaderFromSingleSetReturnValues(_mi.ReturnType, returnValues); // single/multiple result set(from dibix); enumerable or not; complex or not(names for columns?(from SQL/accessor)) 
                    return new DataTableReader(result_tables);
                }
                else
                {
                    object handlerInstance = Activator.CreateInstance(this._instanceType);
                    object returnValues = _mi.Invoke(handlerInstance, invoke_parameters.ToArray());

                    DataTable[] result_tables = CreateDbDataReaderFromSingleSetReturnValues(_mi.ReturnType, returnValues); // single/multiple result set(from dibix); enumerable or not; complex or not(names for columns?(from SQL/accessor)) 
                    return new DataTableReader(result_tables);
                    //string debug_ret; debug_ret = TypeNameAsText(_mi.ReturnType);
                    //throw new NotImplementedException("public " + debug_ret + " " + _mi.Name + "(DataSet db" + debug_params.ToString() + ") of '" + _instanceType.Name + "'");
                }
            }



        }

        private static string TypeNameAsText(Type t)
        {
            string text;
            if (_builtinTypeAlias.TryGetValue(t.FullName, out text))
            {
                return text;
            }

            if (t.IsGenericType)
            {
                if (typeof(System.Collections.IEnumerable).IsAssignableFrom(t))
                {
                    var typeT = t.GetGenericArguments()[0];
                    return "IEnumerable<" + TypeNameAsText(typeT) + ">";
                }
            }
            return t.FullName;
        }

        private static DataTable[] CreateDbDataReaderFromSingleSetReturnValues(Type returnType, object returnValues)
        {
            if (returnValues == null)
            {
                if (returnType.IsPrimitive || returnType == typeof(string))
                {
                    var tb_table = new DataTable();
                    var column_value = new DataColumn("value", returnType);
                    tb_table.Columns.Add(column_value);
                    // 
                    return new DataTable[] { tb_table };
                }
                else
                {
                    // multiple columns!
                    throw new NotImplementedException();
                }
            }
            else
            {
                // single result set : 0..N-rows
                if (typeof(System.Collections.IEnumerable).IsAssignableFrom(returnType) && returnType.IsGenericType)
                {
                    var elementType = returnType.GetGenericArguments()[0];
                    if (elementType.IsPrimitive || elementType == typeof(string))
                    {
                        var tb_table = new DataTable();
                        var column_value = new DataColumn("value", elementType);
                        tb_table.Columns.Add(column_value);
                        System.Collections.IEnumerable ie = (System.Collections.IEnumerable)returnValues;
                        var etor = ie.GetEnumerator();
                        while (etor.MoveNext())
                        {
                            object col_value = etor.Current;
                            if (col_value.GetType() != elementType)
                            {
                                throw new InvalidOperationException(elementType.Name);
                            }
                            if (col_value.GetType() != column_value.DataType)
                            {
                                throw new InvalidOperationException(elementType.Name);
                            }

                            DataRow row = tb_table.NewRow();
                            row[column_value] = col_value;
                            tb_table.Rows.Add(row);
                        }
                        return new DataTable[] { tb_table };
                    }
                    // complex type : multiple rows
                    {
                        DataTable tb_table = ConvertComplexTypeValuesToTable(returnValues, elementType);
                        return new DataTable[] { tb_table };
                    }
                }

                // single result set : 1-row
                if (returnValues.GetType().IsPrimitive
                    || returnValues.GetType() == typeof(string)
                    || returnValues.GetType() == typeof(Guid)
                    || returnValues.GetType() == typeof(DateTime)
                    )
                {
                    Type elementType = returnValues.GetType();
                    var tb_table = new DataTable();
                    var column_value = new DataColumn("value", elementType);
                    tb_table.Columns.Add(column_value);

                    DataRow row = tb_table.NewRow();
                    row[column_value] = returnValues;
                    tb_table.Rows.Add(row);
                    return new DataTable[] { tb_table };
                }

                // IDatabaseAccessor.QueryMultiple
                return ConvertToMultipleTables(returnType, returnValues);
            }

        }

        private static DataTable ConvertComplexTypeValuesToTable(object returnValues, Type elementType)
        {
            List<KeyValuePair<MemberInfo, DataColumn>> property_column_map = new List<KeyValuePair<MemberInfo, DataColumn>>();
            var tb_table = new DataTable();
            foreach (var pi in elementType.GetFields()) // public fields
            {
                var column_p = new DataColumn(pi.Name, pi.FieldType);
                tb_table.Columns.Add(column_p);
                property_column_map.Add(new KeyValuePair<MemberInfo, DataColumn>(pi, column_p));
            }
            foreach (var pi in elementType.GetProperties())
            {
                var column_p = new DataColumn(pi.Name, pi.PropertyType);
                tb_table.Columns.Add(column_p);
                property_column_map.Add(new KeyValuePair<MemberInfo, DataColumn>(pi, column_p));
            }

            if (property_column_map.Count == 0)
            {
                throw new InvalidOperationException("no members");
            }

            System.Collections.IEnumerable ie = (System.Collections.IEnumerable)returnValues;
            var etor = ie.GetEnumerator();
            while (etor.MoveNext())
            {
                object item = etor.Current;

                DataRow row = tb_table.NewRow();
                foreach (var mi_col in property_column_map)
                {
                    var col = mi_col.Value;
                    object col_value;
                    var pi = mi_col.Key as PropertyInfo;
                    if (pi != null)
                    {
                        col_value = pi.GetValue(item);
                        if (col_value == null)
                        {
                            // null value
                            col_value = DBNull.Value;
                        }
                        else
                        {
                            if (col_value.GetType() != pi.PropertyType)
                            {
                                throw new InvalidOperationException(pi.Name);
                            }
                            if (col_value.GetType() != col.DataType)
                            {
                                throw new InvalidOperationException(pi.Name);
                            }
                        }

                        row[mi_col.Value] = col_value;
                    }
                    else
                    {
                        var fi = ((FieldInfo)mi_col.Key);
                        col_value = fi.GetValue(item);
                        if (col_value.GetType() != fi.FieldType)
                        {
                            throw new InvalidOperationException(fi.Name);
                        }
                        if (col_value.GetType() != col.DataType)
                        {
                            throw new InvalidOperationException(fi.Name);
                        }


                        row[mi_col.Value] = col_value;
                    }

                }
                tb_table.Rows.Add(row);
            }

            return tb_table;
        }

        private static DataTable[] ConvertToMultipleTables(Type returnType, object returnValues)
        {
            List<DataTable> tables = new List<DataTable>();
            PropertyInfo[] pis = returnType.GetProperties();
            foreach (PropertyInfo pi in pis)
            {
                //ICollection<>
                // public ICollection<Agent> Agents
/*                Type elementType;

                if (pi.PropertyType.IsGenericType && pi.PropertyType.GetGenericArguments().Length == 1)
                {
                    var gt = pi.PropertyType.GetGenericTypeDefinition(); // typeof(ICollection<>)
                    elementType = pi.PropertyType.GetGenericArguments()[0];
                }
                else
                {
                    throw new NotImplementedException(returnType.FullName + " # " + pi.Name + " # " + pi.PropertyType.FullName);
                }
                if (elementType.IsPrimitive)
                {
                }
                else
                {

                }
*/
                var prop_values = pi.GetValue(returnValues); // property_values can be null / empty table
                DataTable[] property_tables = CreateDbDataReaderFromSingleSetReturnValues(pi.PropertyType, prop_values);
                if (property_tables.Length == 0)
                {
                    throw new NotSupportedException("No set for property. " + returnType.FullName + " # " + pi.Name + " # " + pi.PropertyType.FullName);
                }
                if (property_tables.Length > 1)
                {
                    throw new NotSupportedException("Multiple sets for property. " + returnType.FullName + " # " + pi.Name + " # " + pi.PropertyType.FullName);
                }
                var tb_table = property_tables[0];
                //var tb_table = ConvertComplexTypeValuesToTable(, elementType);
                tables.Add(tb_table);
            }
            return tables.ToArray();
        }

        private static object GetTypedCommandParameter(DbCommand cmd, ParameterInfo method_prm)
        {
            if (method_prm.ParameterType == typeof(int))
            {
                return cmd.GetCommandParameterInt32NotNull(method_prm.Name);
            }

            Type parameterType = method_prm.ParameterType;
            if (parameterType.IsGenericType && typeof(System.Collections.IEnumerable).IsAssignableFrom(parameterType))
            {
                string parameterName = method_prm.Name;
                DbParameter prm = cmd.Parameters.TryFindParameter(parameterName);
                if (prm == null)
                {
                    throw new InvalidOperationException("Parameter could not be found:" + parameterName);
                }

                if (prm.Value == null || prm.Value == DBNull.Value)
                {
                    throw new InvalidOperationException("Parameter value is null:" + parameterName);
                }


                IEnumerable<Microsoft.SqlServer.Server.SqlDataRecord> records = (IEnumerable<Microsoft.SqlServer.Server.SqlDataRecord>)prm.Value;


                Type tupleType = parameterType.GetGenericArguments()[0];
                Type listType = typeof(List<>).MakeGenericType(tupleType);
                object udt = Activator.CreateInstance(listType);
                //object udt = Activator.CreateInstance(parameterType);
                var udt_Add = listType.GetMethod("Add");
                if (udt_Add == null)
                {
                    throw new InvalidOperationException("UDT method 'Add' could be found:" + parameterType.FullName);
                }

                foreach (Microsoft.SqlServer.Server.SqlDataRecord record in records)
                {
                    //object[] udt_record_values = new object[record.FieldCount];
                    //for (int ix = 0; ix < record.FieldCount; ix++)
                    //{
                    //    udt_record_values[ix] = record[ix];
                    //}

                    object udt_row = Activator.CreateInstance(tupleType, record);
                    udt_Add.Invoke(udt, new object[] { udt_row });
                }

                return udt;
                //if (typeof(Dibix.StructuredType) method_prm.ParameterType.)
                // Assert_DbParameter_GetValue_Udt_record
            }

            {

                string debug_method_prm; debug_method_prm = TypeNameAsText(method_prm.ParameterType);
                throw new NotImplementedException("Command parameter for { " + debug_method_prm + " " + method_prm.Name + " }");
            }
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

        private CommandExecutionHandlerImpl()
        {
        }

        internal override IComparable RetrieveCommandText()
        {
            IComparable handlerCommandText = StoreLakeDao.GetCommandTextImpl(this.CommandTextOwner, this._handlerMethodName); // or Invoker
            return handlerCommandText;
        }

        internal override Func<DataSet, DbCommand, DbDataReader> CompiledReadMethod()
        {
            return CompileKnownMethodSignatureRead(HandlerIsStatic, MethodCallExpr.Method, HandlerMethodOwner);
        }

        internal static Func<DataSet, DbCommand, DbDataReader> CompileKnownMethodSignatureRead(bool HandlerIsStatic, MethodInfo mi, Type methodOwner)
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

        internal static Func<DataSet, DbCommand, int> CompileKnownMethodSignatureExec(bool HandlerIsStatic, MethodInfo mi, Type methodOwner)
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
            Expression<Func<DataSet, DbCommand, int>> lambda = Expression.Lambda<Func<DataSet, DbCommand, int>>(call, parameter_db, parameter_cmd); // visitor.ExtractedParameters
            Func<DataSet, DbCommand, int> handlerMethod = lambda.Compile();
            return handlerMethod;
        }

        internal override string RetrieveHandlerMethodName()
        {
            return this._handlerMethodName;
        }

        internal override Func<DataSet, DbCommand, int> CompiledExecMethod()
        {
            throw new NotImplementedException();
        }
    }
}