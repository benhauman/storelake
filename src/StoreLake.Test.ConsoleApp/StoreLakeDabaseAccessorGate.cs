using Dibix;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;

namespace StoreLake.Test.ConsoleApp
{
    /*
    class StoreLakeDao2
    {
        private static SortedDictionary<string, IComparable> s_cache = new SortedDictionary<string, IComparable>();

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
*/
    public sealed class StoreLakeDabaseAccessorGate
    {
        class MyHandlerRead
        {
            private readonly Type _instanceType;
            private readonly MethodInfo _mi;
            private readonly IComparable _commandText;

            public MyHandlerRead(Type instanceType, MethodInfo mi, IComparable commandText)
            {
                _instanceType = instanceType;
                _mi = mi;
                _commandText = commandText;
            }

            [DebuggerDisplay("{name} ({type}) : {value}")]
            class InvokeParameter
            {
                public string name;
                public DbType type;
                public object value;
                public bool isOutput;
            }
            internal object HandleRead(DataSet db, Dibix.ParametersVisitor parameters)
            {
                IDictionary<string, InvokeParameter> parameters_values = new SortedDictionary<string, InvokeParameter>();
                parameters.VisitInputParameters((string name, DbType type, object value, int? size, bool isOutput, CustomInputType customInputType) =>
                {
                    parameters_values.Add(name, new InvokeParameter() { name = name, type = type, value = value, isOutput = isOutput });
                });
                var method_parameters = _mi.GetParameters();
                List<object> invoke_parameter_values = new List<object>();
                for(int ix = 0; ix< method_parameters.Length;ix++)
                {
                    var method_parameter = method_parameters[ix];
                    if (method_parameter.ParameterType == typeof(DataSet))
                    {
                        invoke_parameter_values.Add(db);
                    }
                    else
                    {
                        InvokeParameter parameter_value;
                        if (!parameters_values.TryGetValue(method_parameter.Name, out parameter_value))
                        {
                            // not specified => use default value if defined;
                            throw new NotImplementedException("Parameter '" + method_parameter.Name + "' : " + method_parameter.ParameterType.Name);
                        }

                        invoke_parameter_values.Add(parameter_value.value);
                    }
                }

                object invoke_result;
                if (_mi.IsStatic)
                {
                    invoke_result = _mi.Invoke(null, invoke_parameter_values.ToArray());
                }
                else
                {
                    object handler_instance = Activator.CreateInstance(_instanceType);
                    invoke_result = _mi.Invoke(handler_instance, invoke_parameter_values.ToArray());

                }

                parameters.VisitOutputParameters((string parameterName) =>
                {
                    throw new NotImplementedException("Output parameter:" + parameterName);
                });

                return invoke_result;
            }

        }
        private readonly Dictionary<IComparable, MyHandlerRead> handlers_read = new Dictionary<IComparable, MyHandlerRead>();
        /*
        public void RegisterAccessHandlerFacade<THandlerFacade>(Type databaseAccesorType)
        {
            var handler_methods = typeof(THandlerFacade).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach(var handler_method in handler_methods)
            {
                string handlerMethodName = handler_method.Name;
                IComparable handlerCommandText = StoreLakeDao2.TryGetCommandText(typeof(THandlerFacade), handlerMethodName);
                if (handlerCommandText == null)
                {
                    handlerCommandText = StoreLakeDao2.TryGetCommandText(databaseAccesorType, handlerMethodName);
                    if (handlerCommandText == null)
                    {
                        throw new InvalidOperationException("CommandText for '"+ handlerMethodName + "' could not be found. Accessor:" + databaseAccesorType.FullName);
                    }
                }

                handlers_read.Add(handlerCommandText, new MyHandlerRead(handler_method.DeclaringType, handler_method, handlerCommandText));
            }

        }
        public void RegisterRead(Type commandTextOwner, System.Linq.Expressions.Expression<Func<IDatabaseAccessorFactory, object>> handlerExpr)
        {
            System.Linq.Expressions.MethodCallExpression methodCallExpr;
            UnaryExpression unExpr = handlerExpr.Body as UnaryExpression;
            if (unExpr != null)
            {
                methodCallExpr= (System.Linq.Expressions.MethodCallExpression)unExpr.Operand;
            }
            else
            {
                methodCallExpr = (System.Linq.Expressions.MethodCallExpression)handlerExpr.Body;
            }
            string handlerMethodName = methodCallExpr.Method.Name;
            IComparable handlerCommandText = StoreLakeDao2.TryGetCommandText(commandTextOwner, handlerMethodName); // or Invoker
            if (handlerCommandText == null)
            {
                throw new InvalidOperationException("CommandText could not be found.");
            }

            /*
            var parameter_db = Expression.Parameter(typeof(DataSet), "db");
            var parameter_cmd = Expression.Parameter(typeof(IParametersVisitor), "parameters");
            MethodCallExpression call = Expression.Call(methodCallExpr.Method, parameter_db, parameter_cmd);// methodCallExpr.Arguments
            Expression<Func<DataSet, IParametersVisitor, object>> lambda = Expression.Lambda<Func<DataSet, IParametersVisitor, object>>(call, parameter_db, parameter_cmd); // visitor.ExtractedParameters

            Func<DataSet, IParametersVisitor, object> handlerMethod = lambda.Compile();* /

            handlers_read.Add(handlerCommandText, new MyHandlerRead(methodCallExpr.Method.DeclaringType, methodCallExpr.Method, handlerCommandText));
        }
        */
        public Func<DataSet, Dibix.ParametersVisitor, object> TryGetHandlerRead(string sql)
        {
            if (handlers_read.TryGetValue(sql, out MyHandlerRead handler))
            {
                return handler.HandleRead;
            }
            return null;
        }
    }

    /*
    internal sealed class StoreLakeParametersAccessor : IParametersVisitor
    {
        private readonly Action<string, DbType, object, bool> collector;

        public StoreLakeParametersAccessor(Action<string, Type, bool, object> collector)
        {
            this.collector = collector;
        }

        internal void AddParameter(string name, DbType type, object value, bool isOutput)
        { }

        void IParametersVisitor.VisitInputParameters(InputParameterVisitor visitParameter)
        {
            //_parameters.Each(delegate (Parameter x)
            //{
            //    visitParameter(x.Name, x.Type, x.Value, x.OutputParameter != null);
            //});
        }

        public void VisitOutputParameters(OutputParameterVisitor visitParameter)
        {
            //_parameters.Where((Parameter x) => x.OutputParameter != null).Each(delegate (Parameter x)
            //{
            //    x.OutputParameter.ResolveValue(visitParameter(x.Name));
            //});
        }
    }
    */

}
