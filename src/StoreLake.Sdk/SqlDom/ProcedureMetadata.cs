using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.Data;

namespace StoreLake.Sdk.SqlDom
{
    public sealed class ProcedureMetadata : IBatchParameterMetadata
    {
        public ProcedureMetadata(string procedureName, TSqlFragment bodyFragment, Dictionary<string, ProcedureCodeParameter> procedureParameters)
        {
            ProcedureName = procedureName;
            BodyFragment = bodyFragment;
            foreach(var prm in procedureParameters)
            {
                parameters.Add(prm.Key, prm.Value);
            }
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
                if (prm.TypeNotNull == typeof(Int64))
                {
                    return DbType.Int64;
                }
                if (prm.TypeNotNull == typeof(DateTime))
                {
                    return DbType.DateTime;
                }
                if (prm.TypeNotNull == typeof(Byte))
                {
                    return DbType.Byte;
                }
                if (prm.TypeNotNull == typeof(bool))
                {
                    return DbType.Boolean;
                }
                throw new NotImplementedException(prm.TypeNotNull.Name);
            }
            else
            {
                return null;
            }
        }

        //public void AddParameter(string parameterName, ProcedureCodeParameter parameterType)
        //{
        //    parameters.Add(parameterName, parameterType);
        //}
    }

    public sealed class ProcedureCodeParameter
    {
        internal readonly bool IsUserDefinedTableType;
        internal readonly string UserDefinedTableTypeSqlFullName;
        internal readonly Type TypeNotNull;
        internal readonly Type TypeNull;
        internal readonly DbType ParameterDbType;
        public ProcedureCodeParameter(Type typeNotNull, Type typeNull, DbType parameterDbType)
        {
            TypeNotNull = typeNotNull;
            TypeNull = typeNull;
            ParameterDbType = parameterDbType;
        }
        public ProcedureCodeParameter(string userDefinedTybleTypeFullName, DbType parameterDbType)
        {
            IsUserDefinedTableType = true;
            UserDefinedTableTypeSqlFullName = userDefinedTybleTypeFullName;
            ParameterDbType = parameterDbType;
        }

        internal string ParameterCodeName { get;  set; }

        internal static ProcedureCodeParameter Create<TNotNull, TNull>(DbType parameterDbType)
        {
            return new ProcedureCodeParameter(typeof(TNotNull), typeof(TNull), parameterDbType);
        }
        internal static ProcedureCodeParameter CreateUdt(string userDefinedTybleTypeFullName)
        {
            return new ProcedureCodeParameter(userDefinedTybleTypeFullName, DbType.Object);
        }

    }

}