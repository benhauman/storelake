using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;

namespace ConsoleApp4
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
                IComparable handlerCommandText = handlerRegistry.CommandText();// GetCommandTextImpl(tAccessor, handlerMethodName);

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
                    Func<DataSet, DbCommand, DbDataReader> handlerMethod = handlerRegistry.CompiledMethod();
                    handler = new HandlerReadDesc(handlerRegistry.HandlerMethodName, handlerMethod);
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

            if (handler != null && handler.MethodName == handlerRegistry.HandlerMethodName)
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
                    throw new InvalidOperationException("Field not found:" + fieldName);
                }

                commandText = (IComparable)fi.GetValue(null);
                s_cache.Add(key, commandText);
            }

            return commandText;
        }
    }



    internal sealed class CommandExecutionHandler
    {
        internal readonly Type HandlerMethodOwner;
        internal readonly string HandlerMethodName;
        internal readonly System.Linq.Expressions.MethodCallExpression MethodCallExpr;
        internal readonly bool HandlerIsStatic;
        public CommandExecutionHandler(System.Linq.Expressions.Expression<Func<DataSet, DbCommand, DbDataReader>> handlerExpr)
        {
            System.Linq.Expressions.MethodCallExpression methodCallExpr = (System.Linq.Expressions.MethodCallExpression)handlerExpr.Body;
            string handlerMethodName = methodCallExpr.Method.Name;

            HandlerMethodOwner = methodCallExpr.Method.DeclaringType;
            HandlerMethodName = handlerMethodName;
            MethodCallExpr = (System.Linq.Expressions.MethodCallExpression)handlerExpr.Body;
            HandlerIsStatic = methodCallExpr.Method.IsStatic;
        }

        internal IComparable CommandText()
        {
            IComparable handlerCommandText = StoreLakeDao.GetCommandTextImpl(this.HandlerMethodOwner, this.HandlerMethodName); // or Invoker
            return handlerCommandText;
        }

        internal Func<DataSet, DbCommand, DbDataReader> CompiledMethod()
        {
            // !!!! **** lubo: get rid of the cmd argument inside of the 'methodCallExpr' - just dont use if for compilation***!!!
            var parameter_db = Expression.Parameter(typeof(DataSet), "db");
            var parameter_cmd = Expression.Parameter(typeof(DbCommand), "cmd");
            MethodCallExpression call;
            if (HandlerIsStatic)
            {
                call = Expression.Call(MethodCallExpr.Method, parameter_db, parameter_cmd);// methodCallExpr.Arguments
            }
            else
            {
                Expression instance = Expression.New(HandlerMethodOwner);
                call = Expression.Call(instance, MethodCallExpr.Method, parameter_db, parameter_cmd);// methodCallExpr.Arguments
            }
            Expression<Func<DataSet, DbCommand, DbDataReader>> lambda = Expression.Lambda<Func<DataSet, DbCommand, DbDataReader>>(call, parameter_db, parameter_cmd); // visitor.ExtractedParameters
                                                                                                                                                                      //Func<DatabaseData, DbCommand, DbDataReader> handlerMethod = handlerExpr.Compile();//.DynamicInvoke();
            Func<DataSet, DbCommand, DbDataReader> handlerMethod = lambda.Compile();
            return handlerMethod;
        }

    }
}