using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StoreLake.TestStore.Server
{
    internal static class DbCommandExtensions
    {
        public static TUdt Assert_DbParameter_GetValue_Udt_record<TUdt>(this DbCommand command, string parameterName, Action<TUdt, Microsoft.SqlServer.Server.SqlDataRecord> collector)
            where TUdt : class, new() // see StructuredType<TDefinition>
        {
            TUdt udt = new TUdt();
            var records = command.Assert_DbParameter_GetValue_NoNull<IEnumerable<Microsoft.SqlServer.Server.SqlDataRecord>>(parameterName);
            foreach (var record in records)
            {
                collector(udt, record);
            }
            return udt;
        }
        public static string Assert_DbParameter_GetValue_NoNull_string(this DbCommand command, string parameterName)
        {
            return command.Assert_DbParameter_GetValue_NoNull<string>(parameterName);
        }
        public static int GetCommandParameterInt32NotNull(this DbCommand command, string parameterName)
        {
            return Assert_DbParameter_GetValue_NoNull<int>(command, parameterName);
        }
        private static T Assert_DbParameter_GetValue_NoNull<T>(this DbCommand command, string parameterName) // see 'GetCommandParameter...NotNull'
        {
            DbParameter prm = command.Parameters.TryFindParameter(parameterName);
            if (prm == null)
            {
                throw new InvalidOperationException("Parameter could not be found:" + parameterName);
            }

            if (prm.Value == null || prm.Value == DBNull.Value)
            {
                throw new InvalidOperationException("Parameter value is null:" + parameterName);
            }

            return (T)prm.Value;
        }

        public static T Assert_DbParameter_GetValue<T>(this DbCommand command, string parameterName)
        {
            DbParameter prm = command.Parameters.TryFindParameter(parameterName);
            if (prm == null)
            {
                /// Assert.IsNotNull(prm, "Parameter:" + parameterName);
                return default(T);
            }

            if (((object)prm.Value) == DBNull.Value)
            {
                return default(T);
            }

            return (T)prm.Value;
        }

        public static bool AnyParameterString(this DbCommand command, string value)
        {
            for (int ix = 0; ix < command.Parameters.Count; ix++)
            {
                var prm = command.Parameters[ix];
                if (prm.DbType == DbType.String && prm.Value != DBNull.Value)
                {
                    if (string.Equals(value, (string)prm.Value, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                }
            }

            return false;
        }

        public static bool AnyParameterGuid(this DbCommand command, string value)
        {
            for (int ix = 0; ix < command.Parameters.Count; ix++)
            {
                var prm = command.Parameters[ix];
                if (prm.DbType == DbType.Guid && prm.Value != DBNull.Value)
                {
                    if (string.Equals(new Guid(value), (Guid)prm.Value))
                    {
                        return true;
                    }

                }
            }

            return false;
        }

        public static bool AnyParameterStringStartsWith(this DbCommand command, string value)
        {
            for (int ix = 0; ix < command.Parameters.Count; ix++)
            {
                var prm = command.Parameters[ix];
                if (prm.DbType == DbType.String && prm.Value != DBNull.Value)
                {
                    if (((string)prm.Value).StartsWith(value))
                    {
                        return true;
                    }

                }
            }

            return false;
        }
        public static bool ContainsParameters(this DbCommand command, int parametersCount, params string[] parameterNames)
        {
            if (parametersCount != command.Parameters.Count)
            {
                return false;
            }

            foreach (string parameterName in parameterNames)
            {
                if (!command.ContainsParameter(parameterName))
                    return false;
            }

            return true;
        }

        public static bool ContainsParameter(this DbCommand command, string parameterName)
        {
            return command.Parameters.TryFindParameter(parameterName) != null;
        }
        public static int ParametersCount(this DbCommand command)
        {
            return command.Parameters.Count;
        }
        public static DbParameter TryFindParameter(this DbCommand command, string parameterName)
        {
            return command.Parameters.TryFindParameter(parameterName);
        }

        public static DbParameter TryFindParameter(this DbParameterCollection parameters, string parameterName)
        {
            for (int idx = 0; idx < parameters.Count; idx++)
            {
                DbParameter p = parameters[idx];
                if (p.ParameterName.StartsWith("@"))
                {
                    if (parameterName.StartsWith("@"))
                    {
                        if (string.Equals(p.ParameterName, parameterName, StringComparison.OrdinalIgnoreCase))
                            return p; // ok
                    }
                    else
                    {
                        if (string.Equals(p.ParameterName, "@" + parameterName, StringComparison.OrdinalIgnoreCase))
                            return p; // ok
                    }
                }
                else
                {
                    if (parameterName.StartsWith("@"))
                    {
                        if (string.Equals("@" + p.ParameterName, parameterName, StringComparison.OrdinalIgnoreCase))
                            return p; // ok
                    }
                    else
                    {
                        if (string.Equals(p.ParameterName, parameterName, StringComparison.OrdinalIgnoreCase))
                            return p; // ok
                    }

                }
                if ((string.Equals(p.ParameterName, parameterName, StringComparison.OrdinalIgnoreCase))
                 || (string.Equals(p.ParameterName, "@" + parameterName, StringComparison.OrdinalIgnoreCase)))
                {
                    return p;
                }
            }

            return null;
        }






    }
}
