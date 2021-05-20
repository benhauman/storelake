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

        internal readonly IDictionary<string, ProcedureParameterType> parameters = new SortedDictionary<string, ProcedureParameterType>();
    }

    class ProcedureParameterType
    {
        internal readonly Type TypeNotNull;
        internal readonly Type TypeNull;
        public ProcedureParameterType(Type typeNotNull, Type typeNull)
        {
            TypeNotNull = typeNotNull;
            TypeNull = typeNull;
        }
        internal static ProcedureParameterType Create<TNotNull, TNull>()
        {
            return new ProcedureParameterType(typeof(TNotNull), typeof(TNull));
        }

    }

}