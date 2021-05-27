using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

[assembly: DebuggerDisplay(@"\{Bzz = {BaseIdentifier.Value}}", Target = typeof(SchemaObjectName))]
[assembly: DebuggerDisplay(@"\{Bzz = {StoreLake.Sdk.SqlDom.SqlDomExtensions.WhatIsThis(SchemaObject)}}", Target = typeof(NamedTableReference))]
[assembly: DebuggerDisplay(@"{StoreLake.Sdk.SqlDom.SqlDomExtensions.WhatIsThis(this)}", Target = typeof(TSqlFragment))]
// see [DebuggerTypeProxy(typeof(HashtableDebugView))]
namespace StoreLake.Sdk.SqlDom
{
    internal abstract class QueryColumnBase
    {

    }

    internal sealed class QueryColumnE : QueryColumnBase
    {
        private readonly SelectElement se;
        public QueryColumnE(SelectElement se)
        {
            this.se = se;
            throw new NotImplementedException(se.WhatIsThis());
        }
    }
    internal abstract class QueryColumnSourceBase
    {
        protected string Alias;

        private readonly IDictionary<string, QueryColumnSourceBase> sources = new SortedDictionary<string, QueryColumnSourceBase>();
        internal readonly List<QueryColumnSourceBase> queries = new List<QueryColumnSourceBase>(); // UNION(s)
        private readonly List<SelectElement> selectElements = new List<SelectElement>();

        private readonly string _key;
        public QueryColumnSourceBase(string key)
        {
            _key = key;
        }

        private string Key => _key;


        internal static string BuildKey(SchemaObjectName schemaObject, Identifier alias)
        {
            if (alias != null)
            {
                return alias.Dequote();
            }
            else
            {
                if (schemaObject.SchemaIdentifier != null)
                {
                    return schemaObject.SchemaIdentifier.Dequote() + " . " +
                           schemaObject.BaseIdentifier.Dequote();
                }
                else
                {
                    return schemaObject.BaseIdentifier.Dequote();
                }
            }
        }
        internal static string BuildKey(Identifier itemName, Identifier alias)
        {
            if (alias != null)
            {
                return alias.Dequote();
            }
            else
            {
                return itemName.Dequote();
            }
        }

        internal void AddColumnSource(QueryColumnSourceBase source)
        {
            sources.Add(source.Key, source);
        }

        public void AddQuery(QueryColumnSourceBase expr)
        {
            queries.Add(expr);
        }

        public QueryColumnSourceBase SetAlias(Identifier alias)
        {
            Alias = alias == null ? null : alias.Dequote();
            return this;
        }

        public void AddSelectElement(SelectElement ses)
        {
        }
    }

    internal sealed class QueryColumnSourceMQE : QueryColumnSourceBase
    {
        // ??? SelectElements => from first + UNION the others
        public QueryColumnSourceMQE(Identifier alias)
            : base("$$$_mque_$$$")
        {
            SetAlias(alias);
        }
    }


    [DebuggerDisplay("{DebuggerText}")]
    internal sealed class QueryColumnSourceNT : QueryColumnSourceBase
    {
        internal readonly NamedTableReference NtRef;
        internal readonly string SchemaName;
        internal readonly string TableName;
        public QueryColumnSourceNT(NamedTableReference ntRef)
            : base(BuildKey(ntRef.SchemaObject, ntRef.Alias))
        {
            NtRef = ntRef;
            SchemaName = NtRef.SchemaObject.SchemaIdentifier != null
                ? NtRef.SchemaObject.SchemaIdentifier.Dequote()
                : null;
            TableName = NtRef.SchemaObject.BaseIdentifier.Dequote();
            SetAlias(ntRef.Alias);
        }
        private string DebuggerText
        {
            get
            {
                return (SchemaName == null ? "" : "[" + SchemaName + "]")
                    + "[" + TableName + "]"
                    + (Alias == null ? "" : " AS [" + Alias + "]");
            }
        }
    }

    [DebuggerDisplay("CTE: {DebuggerText}")]
    internal sealed class QueryColumnSourceCTE : QueryColumnSourceBase
    {
        internal readonly CommonTableExpression NtRef;
        internal readonly string CteName;
        public QueryColumnSourceCTE(CommonTableExpression ntRef, Identifier alias)
            : base(BuildKey(ntRef.ExpressionName, alias))
        {
            NtRef = ntRef;
            CteName = NtRef.ExpressionName.Dequote();
            SetAlias(alias);
        }

        private string DebuggerText
        {
            get
            {
                return CteName
                    + (Alias == null ? "" : " AS [" + Alias + "]");
            }
        }

        internal void AddCteSource(QueryColumnSourceBase ts)
        {
            base.AddColumnSource(ts);
        }
    }

    [DebuggerDisplay("UDTF:{DebuggerText}")]
    internal sealed class QueryColumnSourceUDTF : QueryColumnSourceBase
    {
        internal readonly SchemaObjectFunctionTableReference Udtf;
        internal readonly string SchemaName;
        internal readonly string TableName;
        public QueryColumnSourceUDTF(SchemaObjectFunctionTableReference udtf)
            : base(BuildKey(udtf.SchemaObject, udtf.Alias))
        {
            Udtf = udtf;
            SchemaName = udtf.SchemaObject.SchemaIdentifier != null
                ? udtf.SchemaObject.SchemaIdentifier.Dequote()
                : null;
            TableName = udtf.SchemaObject.BaseIdentifier.Dequote();
            SetAlias(udtf.Alias);
        }

        private string DebuggerText
        {
            get
            {
                return (SchemaName == null ? "" : "[" + SchemaName + "]")
                    + "[" + TableName + "]"
                    + (Alias == null ? "" : " AS [" + Alias + "]");
            }
        }
    }

    [DebuggerDisplay("VarT: {DebuggerText}")]
    internal sealed class QueryColumnSourceVarTable : QueryColumnSourceBase
    {
        internal readonly VariableTableReference VarTableRef;
        internal readonly string VariableName;
        public QueryColumnSourceVarTable(VariableTableReference varTableRef)
            : base(varTableRef.Alias.Dequote())
        {
            VarTableRef = varTableRef;
            VariableName = varTableRef.Variable.Name;
            SetAlias(varTableRef.Alias);
        }

        private string DebuggerText
        {
            get
            {
                return VariableName
                    + (Alias == null ? "" : " AS [" + Alias + "]");
            }
        }
    }
}