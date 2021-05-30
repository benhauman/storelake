using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.Data;

namespace StoreLake.Sdk.SqlDom
{
    public sealed class ProcedureMetadata : IBatchParameterMetadata
    {
        public ProcedureMetadata(string procedureName, TSqlFragment bodyFragment)
        {
            ProcedureName = procedureName;
            BodyFragment = bodyFragment;
        }

        public string ProcedureName { get; private set; }
        public TSqlFragment BodyFragment { get; private set; }

        internal readonly IDictionary<string, ProcedureCodeParameter> parameters = new SortedDictionary<string, ProcedureCodeParameter>();

        DbType? IBatchParameterMetadata.TryGetParameterType(string parameterName)
        {
            if (parameters.TryGetValue(parameterName, out ProcedureCodeParameter prm))
            {
                if (prm.TypeNotNull == typeof(int))
                {
                    return DbType.Int32;
                }

                throw new NotImplementedException(prm.TypeNotNull.Name);
            }
            else
            {
                return null;
            }
        }
    }

    sealed class ProcedureCodeParameter
    {
        internal readonly bool IsUserDefinedTableType;
        internal readonly string UserDefinedTableTypeSqlFullName;
        internal readonly Type TypeNotNull;
        internal readonly Type TypeNull;
        public ProcedureCodeParameter(Type typeNotNull, Type typeNull)
        {
            TypeNotNull = typeNotNull;
            TypeNull = typeNull;
        }
        public ProcedureCodeParameter(string userDefinedTybleTypeFullName)
        {
            IsUserDefinedTableType = true;
            UserDefinedTableTypeSqlFullName = userDefinedTybleTypeFullName;
        }

        internal string ParameterCodeName { get;  set; }

        internal static ProcedureCodeParameter Create<TNotNull, TNull>()
        {
            return new ProcedureCodeParameter(typeof(TNotNull), typeof(TNull));
        }
        internal static ProcedureCodeParameter CreateUdt(string userDefinedTybleTypeFullName)
        {
            return new ProcedureCodeParameter(userDefinedTybleTypeFullName);
        }

    }

}