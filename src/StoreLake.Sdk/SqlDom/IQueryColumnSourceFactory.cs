using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Data;
using System.Diagnostics;

[assembly: DebuggerDisplay(@"\{Bzz = {BaseIdentifier.Value}}", Target = typeof(SchemaObjectName))]
[assembly: DebuggerDisplay(@"\{Bzz = {StoreLake.Sdk.SqlDom.SqlDomExtensions.WhatIsThis(SchemaObject)}}", Target = typeof(NamedTableReference))]
[assembly: DebuggerDisplay(@"{StoreLake.Sdk.SqlDom.SqlDomExtensions.WhatIsThis(this)}", Target = typeof(TSqlFragment))]
// see [DebuggerTypeProxy(typeof(HashtableDebugView))]
namespace StoreLake.Sdk.SqlDom
{
    internal interface IQueryColumnSourceFactory
    {
        QueryColumnSourceUDTF NewQueryColumnSourceUDTF(QuerySpecificationModel parent, SchemaObjectFunctionTableReference udtfRef);
        QueryColumnSourceVarTable NewQueryColumnSourceVarTable(QuerySpecificationModel parent, VariableTableReference varTableRef);
        QueryColumnSourceNT NewQueryColumnSourceNT(QuerySpecificationModel parent, NamedTableReference ntRef);
        QueryColumnSourceNT NewQueryColumnSourceNT(QueryOnModificationOutputModel parent, NamedTableReference ntRef);
        QueryColumnSourceVALUES NewQueryColumnSourceValues(QuerySpecificationModel parent, InlineDerivedTable derivedTable);
        QuerySourceOnQuery NewSourceOnCte(QuerySpecificationModel parent, string key);
        QueryOnReqursiveCte NewSourceOnRecursiveCte(QuerySpecificationModel parent, string key, QuerySourceOnQuery cte);
        QuerySourceOnQuery NewSourceOnQueryDerivedTable(QuerySpecificationModel parent, string key, IQueryModel queryModel);
        QuerySourceOnConstant NewConstantSource(QuerySpecificationModel parent, string key, DbType constantType);

        QuerySourceOnNull NewNullSource(QuerySpecificationModel parent, string key);
        QuerySourceOnVariable NewVariableSource(QuerySpecificationModel mqe, VariableReference varRef, DbType variableDbType);
        string NewNameForColumnLiteral(QuerySpecificationModel parent, Literal lit);
        string NewNameForColumnInt64(QuerySpecificationModel parent, long lit);
        string NewNameForColumnInt32(QuerySpecificationModel parent, int lit);
        QuerySourceFullTextTable NewFullTextTable(QuerySpecificationModel parent, FullTextTableReference fttRef);
    }

    internal sealed class QueryColumnSourceFactory : IQueryColumnSourceFactory
    {
        int lastid = 0;
        public QueryColumnSourceFactory()
        {

        }

        private int NewId(QueryModelBase parent)
        {
            lastid++;
            return lastid;
        }

        public QuerySpecificationModel NewRootSpecification(QuerySpecification qspec, string key)
        {
            lastid++;
            return new QuerySpecificationModel(lastid, qspec, key);
        }
        public QueryUnionModel NewRootUnion(QuerySpecification[] qspecs, string key)
        {
            lastid++;
            return new QueryUnionModel(lastid, qspecs, key);
        }

        public QueryOnModificationOutputModel NewRootOnModificationOutput(DataModificationSpecification mspec, string key)
        {
            lastid++;
            return new QueryOnModificationOutputModel(lastid, mspec, key);
        }

        public QueryColumnSourceUDTF NewQueryColumnSourceUDTF(QuerySpecificationModel parent, SchemaObjectFunctionTableReference udtfRef)
        {
            return new QueryColumnSourceUDTF(NewId(parent), udtfRef);
        }

        public QueryColumnSourceVarTable NewQueryColumnSourceVarTable(QuerySpecificationModel parent, VariableTableReference varTableRef)
        {
            return new QueryColumnSourceVarTable(NewId(parent), varTableRef);
        }

        public QueryColumnSourceNT NewQueryColumnSourceNT(QueryOnModificationOutputModel parent, NamedTableReference ntRef)
        {
            return new QueryColumnSourceNT(NewId(parent), ntRef);
        }

        public QueryColumnSourceNT NewQueryColumnSourceNT(QuerySpecificationModel parent, NamedTableReference ntRef)
        {
            return new QueryColumnSourceNT(NewId(parent), ntRef);
        }

        public QueryColumnSourceVALUES NewQueryColumnSourceValues(QuerySpecificationModel parent, InlineDerivedTable derivedTable)
        {
            return new QueryColumnSourceVALUES(NewId(parent), derivedTable);
        }

        public QuerySourceOnQuery NewSourceOnCte(QuerySpecificationModel parent, string key)
        {
            return new QuerySourceOnQuery(NewId(parent), key);
        }

        public QuerySourceOnConstant NewConstantSource(QuerySpecificationModel parent, string key, DbType constantType)
        {
            return new QuerySourceOnConstant(NewId(parent), key, constantType);
        }

        public QuerySourceOnNull NewNullSource(QuerySpecificationModel parent, string key)
        {
            return new QuerySourceOnNull(NewId(parent), key);
        }

        public QuerySourceOnVariable NewVariableSource(QuerySpecificationModel parent, VariableReference varRef, DbType variableDbType)
        {
            return new QuerySourceOnVariable(NewId(parent), varRef, variableDbType);
        }

        public string NewNameForColumnLiteral(QuerySpecificationModel parent, Literal lit)
        {
            return "?" + NewId(parent) + "?";
        }
        public string NewNameForColumnInt64(QuerySpecificationModel parent, long lit)
        {
            return "?" + NewId(parent) + "?";
        }
        public string NewNameForColumnInt32(QuerySpecificationModel parent, int lit)
        {
            return "?" + NewId(parent) + "?";
        }

        public QuerySourceOnQuery NewSourceOnQueryDerivedTable(QuerySpecificationModel parent, string key, IQueryModel cte_qmodel)
        {
            return new QuerySourceOnQuery(NewId(parent), key).SetQuery(cte_qmodel);
        }

        public QueryOnReqursiveCte NewSourceOnRecursiveCte(QuerySpecificationModel parent, string key, QuerySourceOnQuery cte)
        {
            return new QueryOnReqursiveCte(NewId(parent), key, cte);
        }

        public QuerySourceFullTextTable NewFullTextTable(QuerySpecificationModel parent, FullTextTableReference fttRef)
        {
            return new QuerySourceFullTextTable(NewId(parent), fttRef);
        }
    }
}