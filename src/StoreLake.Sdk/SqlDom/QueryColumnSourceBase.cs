using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;

[assembly: DebuggerDisplay(@"\{Bzz = {BaseIdentifier.Value}}", Target = typeof(SchemaObjectName))]
[assembly: DebuggerDisplay(@"\{Bzz = {StoreLake.Sdk.SqlDom.SqlDomExtensions.WhatIsThis(SchemaObject)}}", Target = typeof(NamedTableReference))]
[assembly: DebuggerDisplay(@"{StoreLake.Sdk.SqlDom.SqlDomExtensions.WhatIsThis(this)}", Target = typeof(TSqlFragment))]
// see [DebuggerTypeProxy(typeof(HashtableDebugView))]
namespace StoreLake.Sdk.SqlDom
{
    internal abstract class QueryColumnBase
    {
        public abstract string OutputColumnName { get; }
        public abstract string SourceColumnName { get; }
        public abstract DbType? ColumnDbType { get; }

        public abstract void SetColumnDbType(DbType columnDbType);
        public abstract void SetSourceColumnName(string sourceColumnName, DbType columnDbType);

        internal readonly int SourceId;
        protected QueryColumnBase(int sourceId)
        {
            SourceId = sourceId;
        }
    }

    class ColumnModel
    {
        internal string ColumnName;
        internal System.Data.DbType? ColumnDbType;
        internal bool IsOk { get { return ColumnDbType.HasValue; } }
    }

    [DebuggerDisplay("{DebuggerText}")]
    internal sealed class QueryColumnE : QueryColumnBase
    {
        private readonly string outputColumnName;
        private string _sourceColumnName;
        private DbType? _columnDbType;

        private readonly QueryColumnSourceBase source;

        public QueryColumnE(QueryColumnSourceBase source, string outputColumnName, string sourceColumnName, DbType? columnDbType)
            : base(source.Id)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (string.IsNullOrEmpty(outputColumnName))
                throw new ArgumentNullException(nameof(outputColumnName));

            this.source = source;
            this.outputColumnName = outputColumnName;
            this._sourceColumnName = sourceColumnName;
            this._columnDbType = columnDbType;
        }
        private string DebuggerText
        {
            get
            {
                return (OutputColumnName ?? "?")
                    + " (" + (ColumnDbType.HasValue ? "" + ColumnDbType.Value : "?") + ")"
                    + " : " + (SourceColumnName ?? "?")
                    + " Source:" + SourceId
                    ;
            }
        }

        public override string OutputColumnName => outputColumnName;

        public override string SourceColumnName => _sourceColumnName;

        public override DbType? ColumnDbType => _columnDbType;

        public override void SetColumnDbType(DbType columnDbType)
        {
            if (ColumnDbType.HasValue)
                throw new InvalidOperationException("Column type already resolved.");
            this._columnDbType = columnDbType;
        }
        public override void SetSourceColumnName(string sourceColumnName, DbType columnDbType)
        {
            if (string.IsNullOrEmpty(sourceColumnName))
                throw new ArgumentNullException("sourceColumnName");

            if (string.IsNullOrEmpty(SourceColumnName))
                throw new InvalidOperationException("Column type already resolved.");

            if (ColumnDbType.HasValue)
                throw new InvalidOperationException("Column type already resolved.");

            this._sourceColumnName = sourceColumnName;
            this._columnDbType = columnDbType;
        }
    }

    internal interface IQueryColumnSourceFactory
    {
        QueryColumnSourceUDTF NewQueryColumnSourceUDTF(QueryColumnSourceMQE parent, SchemaObjectFunctionTableReference udtfRef);
        QueryColumnSourceVarTable NewQueryColumnSourceVarTable(QueryColumnSourceMQE parent, VariableTableReference varTableRef);
        QueryColumnSourceMQE NewQueryColumnSourceCTE(QueryColumnSourceMQE parent, QuerySpecification qspec, string key);
        QueryColumnSourceNT NewQueryColumnSourceNT(QueryColumnSourceMQE parent, NamedTableReference ntRef);
        QueryColumnSourceVALUES NewQueryColumnSourceValues(QueryColumnSourceMQE parent, InlineDerivedTable derivedTable);
    }
    internal sealed class QueryColumnSourceFactory : IQueryColumnSourceFactory
    {
        int lastid = 0;
        public QueryColumnSourceFactory()
        {

        }
        private int NewId(QueryColumnSourceBase parent)
        {
            lastid++;
            return lastid;
        }

        public QueryColumnSourceMQE NewRoot(QuerySpecification qspec, string key)
        {
            lastid++;
            return new QueryColumnSourceMQE(lastid, qspec, key);
        }

        public QueryColumnSourceUDTF NewQueryColumnSourceUDTF(QueryColumnSourceMQE parent, SchemaObjectFunctionTableReference udtfRef)
        {
            return new QueryColumnSourceUDTF(NewId(parent), udtfRef);
        }

        public QueryColumnSourceVarTable NewQueryColumnSourceVarTable(QueryColumnSourceMQE parent, VariableTableReference varTableRef)
        {
            return new QueryColumnSourceVarTable(NewId(parent), varTableRef);
        }

        public QueryColumnSourceMQE NewQueryColumnSourceCTE(QueryColumnSourceMQE parent, QuerySpecification qspec, string key)
        {
            return new QueryColumnSourceMQE(NewId(parent), qspec, key);
        }

        public QueryColumnSourceNT NewQueryColumnSourceNT(QueryColumnSourceMQE parent, NamedTableReference ntRef)
        {
            return new QueryColumnSourceNT(NewId(parent), ntRef);
        }

        public QueryColumnSourceVALUES NewQueryColumnSourceValues(QueryColumnSourceMQE parent, InlineDerivedTable derivedTable)
        {
            return new QueryColumnSourceVALUES(NewId(parent), derivedTable);
        }
    }


    internal abstract class QueryColumnSourceBase
    {
        protected string Alias;

        internal readonly IDictionary<string, QueryColumnSourceBase> sources = new SortedDictionary<string, QueryColumnSourceBase>();

        private readonly string _key;
        internal readonly int Id; // unique in the whole batch
        public QueryColumnSourceBase(int id, string key)
        {
            this.Id = id;
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException("key");
            _key = key;
        }

        internal string Key => _key;

        internal int SourceCount => sources.Count;
        internal QueryColumnSourceBase SourceSingle
        {
            get
            {
                return sources.Values.Single();
            }
        }


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

        protected void AddColumnSource(QueryColumnSourceBase source)
        {
            sources.Add(source.Key, source);
        }

        public QueryColumnSourceBase SetAlias(Identifier alias)
        {
            Alias = alias == null ? null : alias.Dequote();
            return this;
        }

        internal bool TryFindSource(string sourceNameOrAlias, out QueryColumnSourceBase source)
        {
            if (this.sources.TryGetValue(sourceNameOrAlias, out source))
            {
                return true;
            }

            return false;
        }



        internal bool TryFindColumnC(StatementModel model, string columnName, out QueryColumnBase cm)
        {
            throw new NotImplementedException();
        }


        internal readonly Dictionary<string, QueryColumnBase> resolved_OutputColumns = new Dictionary<string, QueryColumnBase>();

        protected bool IsOutputColumnResolved(string outputColumnName, out QueryColumnBase col)
        {
            return resolved_OutputColumns.TryGetValue(outputColumnName.ToUpperInvariant(), out col);
        }

        protected QueryColumnBase AddResolveOutputdColumn(QueryColumnBase resolved_column)
        {
            resolved_OutputColumns.Add(resolved_column.OutputColumnName.ToUpperInvariant(), resolved_column);
            return resolved_column;
        }

        protected abstract bool TryResolveOutputColumn(BatchOutputColumnTypeResolver batchResolver, string sourceNameOrAlias, string columnName);

        internal abstract QueryColumnBase TryResolveSelectedColumn(BatchOutputColumnTypeResolver batchResolver, string outputColumnName, string sourceColumnName);

        internal abstract bool TryResolveSourceColumnType(BatchOutputColumnTypeResolver batchResolver, string sourceColumnName, out DbType columnType);
    }

    [DebuggerDisplay("Mqe Id:{Id}, Key:{_key}")]
    internal sealed class QueryColumnSourceMQE : QueryColumnSourceBase
    {
        internal readonly QuerySpecification QrySpec;
        internal readonly List<QueryColumnSourceMQE> union_queries = new List<QueryColumnSourceMQE>(); // UNION(s)
        // ??? SelectElements => from first + UNION the others
        public QueryColumnSourceMQE(int id, QuerySpecification qspec, string key)
            : base(id, key)
        //: base(alias == null ? "$$$_mque_$$$" : alias.Dequote())
        {
            QrySpec = qspec;
        }
        internal void AddMqeColumnSource(QueryColumnSourceBase ts)
        {
            base.AddColumnSource(ts);
        }



        internal void AddUnionQuery(QueryColumnSourceMQE query)
        {
            union_queries.Add(query);
        }

        internal override QueryColumnBase TryResolveSelectedColumn(BatchOutputColumnTypeResolver batchResolver, string outputColumnName, string sourceColumnName)
        {
            string outputColumnNameSafe = outputColumnName ?? sourceColumnName;
            if (IsOutputColumnResolved(outputColumnNameSafe, out QueryColumnBase col))
            {
                return col;
            }
            throw new NotImplementedException(Key + "." + sourceColumnName + " => " + outputColumnName);
        }

        internal QueryColumnBase AddOutputColumn(string outputColumnName, string sourceColumnName, DbType? columnDbType)
        {
            if (IsOutputColumnResolved(outputColumnName, out QueryColumnBase col))
            {
                if (columnDbType.HasValue && !col.ColumnDbType.HasValue)
                {
                    col.SetColumnDbType(columnDbType.Value);
                }

                if (col.SourceColumnName == null && sourceColumnName != null)
                {
                    col.SetSourceColumnName(sourceColumnName, columnDbType.Value); // both (source) properties must be there
                }
                return col;
            }
            else
            {
                return base.AddResolveOutputdColumn(new QueryColumnE(this, outputColumnName, sourceColumnName, columnDbType));
            }
        }


        internal void AddOutputColumnWithoutType(QueryColumnBase outputColumn)
        {
            if (!IsOutputColumnResolved(outputColumn.OutputColumnName.ToUpperInvariant(), out QueryColumnBase col))
            {
                if (ContainsSourceId(outputColumn.SourceId))
                {
                    col = base.AddResolveOutputdColumn(new QueryColumnE(this, outputColumn.OutputColumnName, outputColumn.SourceColumnName, outputColumn.ColumnDbType));
                }
                else
                {
                    throw new NotSupportedException("Output column not registered.");
                }
            }
        }

        internal void AddOutputColumn(QueryColumnBase outputColumn)
        {
            if (!IsOutputColumnResolved(outputColumn.OutputColumnName.ToUpperInvariant(), out QueryColumnBase col))
            {
                if (ContainsSourceId(outputColumn.SourceId))
                {
                    col = base.AddResolveOutputdColumn(new QueryColumnE(this, outputColumn.OutputColumnName, outputColumn.SourceColumnName, outputColumn.ColumnDbType));
                }
                else
                {
                    throw new NotSupportedException("Output column not registered.");
                }
            }


            if (!col.ColumnDbType.HasValue)
            {
                if (outputColumn.ColumnDbType.HasValue)
                {
                    col.SetColumnDbType(outputColumn.ColumnDbType.Value); // mismatched output queries?!?!
                }
                else
                {
                    throw new NotSupportedException("Output column type not resolved.");
                }
            }

            if (!outputColumn.ColumnDbType.HasValue)
            {
                throw new ArgumentException("Output column type not resolved.", nameof(outputColumn));
            }
        }

        private bool ContainsSourceId(int sourceId)
        {
            if (this.Id == sourceId)
                return true;
            foreach (var source in this.sources.Values)
            {
                if (source.Id == sourceId)
                    return true;
            }
            return false;
        }

        internal override bool TryResolveSourceColumnType(BatchOutputColumnTypeResolver batchResolver, string sourceColumnName, out DbType columnType)
        {
            throw new NotImplementedException();
        }

        internal bool TryFindColumnSC(BatchOutputColumnTypeResolver batchResolver, string sourceNameOrAlias, string outputColumnName, out QueryColumnBase col)
        {
            if (base_TryFindColumnSC(batchResolver, sourceNameOrAlias, outputColumnName, out col))
                return true;

            foreach (QueryColumnSourceMQE union_query in union_queries)
            {
                if (union_query.TryFindColumnSC(batchResolver, sourceNameOrAlias, outputColumnName, out col))
                {
                    return true;
                }
            }

            return false;
        }
        private bool base_TryFindColumnSC(BatchOutputColumnTypeResolver batchResolver, string sourceNameOrAlias, string outputColumnName, out QueryColumnBase col)
        {
            if (this.sources.TryGetValue(sourceNameOrAlias, out QueryColumnSourceBase source))
            {
                if (TryGetOutputColumnByName(source, batchResolver, sourceNameOrAlias, outputColumnName, out col))
                {
                    return true;
                }

                //throw new NotImplementedException(Key + "." + outputColumnName);
                return false;
            }
            else
            {
                throw new NotImplementedException();
            }
        }
        private static bool TryGetOutputColumnByName(QueryColumnSourceBase that, BatchOutputColumnTypeResolver batchResolver, string sourceNameOrAlias, string columnName, out QueryColumnBase col)
        {
            string columnKey = columnName.ToUpperInvariant();
            if (that.resolved_OutputColumns.TryGetValue(columnKey, out col))
            {
                return true;
            }
            //??? if (that.TryResolveOutputColumn(batchResolver, sourceNameOrAlias, columnName))
            //??? {
            //???     if (that.resolved_OutputColumns.TryGetValue(columnKey, out col))
            //???     {
            //???         return true;
            //???     }
            //??? }

            return false;
        }
        protected override bool TryResolveOutputColumn(BatchOutputColumnTypeResolver batchResolver, string sourceNameOrAlias, string sourceColumnName)
        {
            return false;
        }

        internal void CollectSubQueries(Action<QueryColumnSourceMQE> collector)
        {
            foreach (var source in sources.Values)
            {
                if (source is QueryColumnSourceMQE subQuery)
                {
                    subQuery.CollectSubQueries(collector);
                    collector(subQuery);
                }
            }

            // BOTTOM-UP (from the last in the union to the first one.) The first selected in the union is the output column information provider
            for (int ix = union_queries.Count; ix > 0; ix--)
            {
                QueryColumnSourceMQE qry = union_queries[ix - 1];
                qry.CollectSubQueries(collector);
            }

            collector(this);
        }

        private bool _queryOutputResolved;
        internal bool IsQueryOutputResolved => _queryOutputResolved;

        internal int ResolvedOutputColumnsCount => resolved_OutputColumns.Count;

        internal QueryColumnBase[] CollectResolvedOutputColumnWithoutType()
        {
            return resolved_OutputColumns.Values.Where(x => !x.ColumnDbType.HasValue).ToArray();
        }

        internal QueryColumnBase HasResolvedOutputColumnWithoutType()
        {
            foreach (var col in resolved_OutputColumns.Values)
            {
                if (!col.ColumnDbType.HasValue)
                {
                    return col;
                }
            }

            return null;
        }
        internal void SetAsOutputResolved()
        {
            if (_queryOutputResolved)
                throw new NotImplementedException("query already resolved.");

            if (QrySpec.SelectElements.Count != resolved_OutputColumns.Count)
            {
                throw new NotImplementedException("Not all selected output column has been resolved. Selected:" + QrySpec.SelectElements.Count + ", Resolved:" + resolved_OutputColumns.Count);
            }

            QueryColumnBase resolvedOutputColumnWithoutType = HasResolvedOutputColumnWithoutType();
            if (resolvedOutputColumnWithoutType != null)
            {
                throw new NotImplementedException("A selected output column without type. Name:" + resolvedOutputColumnWithoutType.OutputColumnName);
            }

            foreach (var union_query in union_queries)
            {
                if (!union_query.IsQueryOutputResolved)
                {
                    throw new NotImplementedException("An UNION query not resolved");
                }
            }
            _queryOutputResolved = true;
        }
        internal bool TryGetResolvedOutputColumn(string outputColumnName, out QueryColumnBase outputColumn)
        {
            if (IsOutputColumnResolved(outputColumnName, out outputColumn))
                return true;
            foreach (var union_query in union_queries)
            {
                if (union_query.IsOutputColumnResolved(outputColumnName, out outputColumn))
                    return true;
            }
            return false;
        }
    }


    [DebuggerDisplay("NT {DebuggerText}")]
    internal sealed class QueryColumnSourceNT : QueryColumnSourceBase
    {
        internal readonly NamedTableReference NtRef;
        internal readonly string SchemaName;
        internal readonly string TableName;
        public QueryColumnSourceNT(int id, NamedTableReference ntRef)
            : base(id, BuildKey(ntRef.SchemaObject, ntRef.Alias))
        {
            NtRef = ntRef;
            SchemaName = NtRef.SchemaObject.SchemaIdentifier != null
                ? NtRef.SchemaObject.SchemaIdentifier.Dequote()
                : null;
            TableName = NtRef.SchemaObject.BaseIdentifier.Dequote();
            //SetAlias(ntRef.Alias);
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

        IColumnSourceMetadata resolved_table;
        internal override QueryColumnBase TryResolveSelectedColumn(BatchOutputColumnTypeResolver batchResolver, string outputColumnName, string sourceColumnName)
        {
            if (resolved_table == null)
            {
                resolved_table = batchResolver.SchemaMetadata.TryGetColumnSourceMetadata(SchemaName, TableName);
                if (resolved_table == null)
                {
                    // table not exists???
                    throw new NotImplementedException(Key + "." + sourceColumnName + " => " + outputColumnName);

                }
            }

            string outputColumnNameSafe = outputColumnName ?? sourceColumnName;

            if (IsOutputColumnResolved(outputColumnNameSafe, out QueryColumnBase col))
            {
                if (col.ColumnDbType.HasValue)
                {
                    return col;
                }
            }

            DbType? columnDbType = resolved_table.TryGetColumnTypeByName(sourceColumnName);
            if (columnDbType == null)
            {
                return null;
            }
            if (col != null)
            {
                col.SetColumnDbType(columnDbType.Value);
                return col;
            }
            else
            {
                return base.AddResolveOutputdColumn(new QueryColumnE(this, outputColumnNameSafe, sourceColumnName, columnDbType.Value));
            }
        }

        internal override bool TryResolveSourceColumnType(BatchOutputColumnTypeResolver batchResolver, string sourceColumnName, out DbType columnDbType)
        {
            if (resolved_table == null)
            {
                resolved_table = batchResolver.SchemaMetadata.TryGetColumnSourceMetadata(SchemaName, TableName);
                if (resolved_table == null)
                {
                    // table not exists???
                    throw new NotImplementedException(Key + "." + sourceColumnName);

                }
            }

            DbType? sourcColumnDbType = resolved_table.TryGetColumnTypeByName(sourceColumnName);
            if (sourcColumnDbType.HasValue)
            {
                columnDbType = sourcColumnDbType.Value;
                return true;
            }
            throw new NotImplementedException();
        }

        protected override bool TryResolveOutputColumn(BatchOutputColumnTypeResolver batchResolver, string sourceNameOrAlias, string sourceColumnName)
        {
            if (resolved_table == null)
            {
                resolved_table = batchResolver.SchemaMetadata.TryGetColumnSourceMetadata(SchemaName, TableName);
                if (resolved_table == null)
                {
                    // table not exists???
                    throw new NotImplementedException(Key + "." + sourceColumnName);

                }
            }

            string outputColumnNameSafe = sourceColumnName;

            DbType? columnDbType = resolved_table.TryGetColumnTypeByName(sourceColumnName);
            if (columnDbType == null)
            {
                return false;
            }

            base.AddResolveOutputdColumn(new QueryColumnE(this, outputColumnNameSafe, sourceColumnName, columnDbType.Value));
            return true;
        }
    }

    [DebuggerDisplay("UDTF:{DebuggerText}")]
    internal sealed class QueryColumnSourceUDTF : QueryColumnSourceBase
    {
        internal readonly SchemaObjectFunctionTableReference Udtf;
        internal readonly string SchemaName;
        internal readonly string FunctionName;
        public QueryColumnSourceUDTF(int id, SchemaObjectFunctionTableReference udtf)
            : base(id, BuildKey(udtf.SchemaObject, udtf.Alias))
        {
            Udtf = udtf;
            SchemaName = udtf.SchemaObject.SchemaIdentifier != null
                ? udtf.SchemaObject.SchemaIdentifier.Dequote()
                : null;
            FunctionName = udtf.SchemaObject.BaseIdentifier.Dequote();
            //SetAlias(udtf.Alias);
        }

        private string DebuggerText
        {
            get
            {
                return (SchemaName == null ? "" : "[" + SchemaName + "]")
                    + "[" + FunctionName + "]"
                    + (Alias == null ? "" : " AS [" + Alias + "]");
            }
        }

        private IColumnSourceMetadata resolved_source_metadata;
        internal override QueryColumnBase TryResolveSelectedColumn(BatchOutputColumnTypeResolver batchResolver, string outputColumnName, string sourceColumnName)
        {
            if (resolved_source_metadata == null)
            {
                resolved_source_metadata = batchResolver.SchemaMetadata.TryGetFunctionTableMetadata(SchemaName, FunctionName);
                if (resolved_source_metadata == null)
                {
                    throw new NotImplementedException(SchemaName + "." + FunctionName + "  => " + outputColumnName);
                }
            }

            DbType? columnDbType = resolved_source_metadata.TryGetColumnTypeByName(sourceColumnName);
            if (columnDbType == null)
            {
                return null;
            }

            string outputColumnNameSafe = outputColumnName ?? sourceColumnName;

            return base.AddResolveOutputdColumn(new QueryColumnE(this, outputColumnNameSafe, sourceColumnName, columnDbType.Value));
        }

        internal override bool TryResolveSourceColumnType(BatchOutputColumnTypeResolver batchResolver, string sourceColumnName, out DbType columnType)
        {
            throw new NotImplementedException();
        }
        protected override bool TryResolveOutputColumn(BatchOutputColumnTypeResolver batchResolver, string sourceNameOrAlias, string sourceColumnName)
        {
            throw new NotImplementedException();
        }

    }

    [DebuggerDisplay("VarT: {DebuggerText}")]
    internal sealed class QueryColumnSourceVarTable : QueryColumnSourceBase
    {
        internal readonly VariableTableReference VarTableRef;
        internal readonly string VariableName;
        public QueryColumnSourceVarTable(int id, VariableTableReference varTableRef)
            : base(id, varTableRef.Alias.Dequote())
        {
            VarTableRef = varTableRef;
            VariableName = varTableRef.Variable.Name;
            //SetAlias(varTableRef.Alias);
        }

        private string DebuggerText
        {
            get
            {
                return VariableName
                    + (Alias == null ? "" : " AS [" + Alias + "]");
            }
        }

        IColumnSourceMetadata resolved_source_metadata;
        internal override QueryColumnBase TryResolveSelectedColumn(BatchOutputColumnTypeResolver batchResolver, string outputColumnName, string sourceColumnName)
        {
            if (resolved_source_metadata == null)
            {
                resolved_source_metadata = batchResolver.TryGetTableVariable(VarTableRef.Variable.Name);
                if (resolved_source_metadata == null)
                {
                    throw new NotImplementedException(VarTableRef.WhatIsThis());
                }
            }

            DbType? columnDbType = resolved_source_metadata.TryGetColumnTypeByName(sourceColumnName);
            if (columnDbType == null)
            {
                return null;
            }
            return base.AddResolveOutputdColumn(new QueryColumnE(this, outputColumnName, sourceColumnName, columnDbType.Value));

        }

        internal override bool TryResolveSourceColumnType(BatchOutputColumnTypeResolver batchResolver, string sourceColumnName, out DbType columnType)
        {
            throw new NotImplementedException();
        }
        protected override bool TryResolveOutputColumn(BatchOutputColumnTypeResolver batchResolver, string sourceNameOrAlias, string sourceColumnName)
        {
            if (resolved_source_metadata == null)
            {
                resolved_source_metadata = batchResolver.TryGetTableVariable(VarTableRef.Variable.Name);
                if (resolved_source_metadata == null)
                {
                    throw new NotImplementedException(VarTableRef.WhatIsThis());
                }
            }

            DbType? columnDbType = resolved_source_metadata.TryGetColumnTypeByName(sourceColumnName);
            if (columnDbType == null)
            {
                return false;
            }
            base.AddResolveOutputdColumn(new QueryColumnE(this, sourceColumnName, sourceColumnName, columnDbType.Value));
            return true;

        }

    }


    [DebuggerDisplay("VALUES: {DebuggerText}")]
    internal sealed class QueryColumnSourceVALUES : QueryColumnSourceBase
    {
        internal readonly InlineDerivedTable _derivedTable;
        public QueryColumnSourceVALUES(int id, InlineDerivedTable derivedTable)
            : base(id, derivedTable.Alias.Dequote())
        {
            _derivedTable = derivedTable;
            //SetAlias(varTableRef.Alias);
        }

        private string DebuggerText
        {
            get
            {
                return "(...)"
                    + (Alias == null ? "" : " AS [" + Alias + "]");
            }
        }

        internal override QueryColumnBase TryResolveSelectedColumn(BatchOutputColumnTypeResolver batchResolver, string outputColumnName, string sourceColumnName)
        {
            throw new NotImplementedException(_derivedTable.WhatIsThis());
            //DbType? columnDbType = resolved_source_metadata.TryGetColumnTypeByName(sourceColumnName);
            //if (columnDbType == null)
            //{
            //    return null;
            //}
            //return base.AddResolveOutputdColumn(new QueryColumnE(this, outputColumnName, sourceColumnName, columnDbType.Value));

        }

        internal override bool TryResolveSourceColumnType(BatchOutputColumnTypeResolver batchResolver, string sourceColumnName, out DbType columnType)
        {
            throw new NotImplementedException();
        }

        protected override bool TryResolveOutputColumn(BatchOutputColumnTypeResolver batchResolver, string sourceNameOrAlias, string sourceColumnName)
        {
            throw new NotImplementedException();
        }

    }
}