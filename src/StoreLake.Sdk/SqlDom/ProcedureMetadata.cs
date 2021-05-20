using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;

namespace StoreLake.Sdk.SqlDom
{
    public sealed class ProcedureMetadata
    {
        public ProcedureMetadata(string procedureName, TSqlFragment bodyFragment)
        {
            ProcedureName = procedureName;
            BodyFragment = bodyFragment;
        }

        public string ProcedureName { get; private set; }
        public TSqlFragment BodyFragment { get; private set; }

        internal readonly IDictionary<string, ProcedureCodeParameter> parameters = new SortedDictionary<string, ProcedureCodeParameter>();
    }

    class ProcedureCodeParameter
    {
        internal readonly Type TypeNotNull;
        internal readonly Type TypeNull;
        public ProcedureCodeParameter(Type typeNotNull, Type typeNull)
        {
            TypeNotNull = typeNotNull;
            TypeNull = typeNull;
        }

        internal string ParameterCodeName { get;  set; }

        internal static ProcedureCodeParameter Create<TNotNull, TNull>()
        {
            return new ProcedureCodeParameter(typeof(TNotNull), typeof(TNull));
        }

    }

}