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
    [DebuggerDisplay("{DebuggerText}")]
    internal abstract class QueryColumnBase
    {
        public abstract string OutputColumnName { get; }
        public abstract string SourceColumnName { get; }
        public abstract DbType? ColumnDbType { get; }

        public abstract void SetColumnDbType(DbType columnDbType);
        public abstract void SetSourceColumnName(string sourceColumnName, DbType columnDbType);

        private string DebuggerText
        {
            get
            {
                return (OutputColumnName ?? "?")
                    + " (" + (ColumnDbType.HasValue ? "" + ColumnDbType.Value : "?") + ")"
                    + " : " + (SourceColumnName ?? "?");
            }
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
                    + " : " + (SourceColumnName ?? "?");
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
        QueryColumnSourceUDTF NewQueryColumnSourceUDTF(SchemaObjectFunctionTableReference udtfRef);
        QueryColumnSourceVarTable NewQueryColumnSourceVarTable(VariableTableReference varTableRef);
        QueryColumnSourceMQE NewQueryColumnSourceMQE(QuerySpecification qspec, string key);
        QueryColumnSourceNT NewQueryColumnSourceNT(NamedTableReference ntRef);
    }
    internal sealed class QueryColumnSourceFactory : IQueryColumnSourceFactory
    {
        int lastid = 0;
        public QueryColumnSourceFactory()
        {

        }
        private int NewId()
        {
            lastid++;
            return lastid;
        }

        public QueryColumnSourceUDTF NewQueryColumnSourceUDTF(SchemaObjectFunctionTableReference udtfRef)
        {
            return new QueryColumnSourceUDTF(NewId(), udtfRef);
        }

        public QueryColumnSourceVarTable NewQueryColumnSourceVarTable(VariableTableReference varTableRef)
        {
            return new QueryColumnSourceVarTable(NewId(), varTableRef);
        }

        public QueryColumnSourceMQE NewQueryColumnSourceMQE(QuerySpecification qspec, string key)
        {
            return new QueryColumnSourceMQE(NewId(), qspec, key);
        }

        public QueryColumnSourceNT NewQueryColumnSourceNT(NamedTableReference ntRef)
        {
            return new QueryColumnSourceNT(NewId(), ntRef);
        }
    }

    [DebuggerDisplay("{Id}:{_key}")]
    internal abstract class QueryColumnSourceBase
    {
        protected string Alias;

        private readonly IDictionary<string, QueryColumnSourceBase> sources = new SortedDictionary<string, QueryColumnSourceBase>();

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

        internal virtual bool TryFindColumnSC(BatchOutputColumnTypeResolver batchResolver, string sourceNameOrAlias, string outputColumnName, ColumnModel cm)
        {
            if (this.sources.TryGetValue(sourceNameOrAlias, out QueryColumnSourceBase source))
            {
                if (source.TryGetColumnByName(batchResolver, outputColumnName, out QueryColumnBase col))
                {
                    cm.ColumnName = col.OutputColumnName;
                    cm.ColumnDbType = col.ColumnDbType;
                    return true;
                }

                throw new NotImplementedException(Key + "." + outputColumnName);
            }
            throw new NotImplementedException();
        }

        internal bool TryFindColumnC(StatementModel model, string columnName, ColumnModel cm)
        {
            throw new NotImplementedException();
        }


        private readonly Dictionary<string, QueryColumnBase> resolved_OutputColumns = new Dictionary<string, QueryColumnBase>();
        private bool TryGetColumnByName(BatchOutputColumnTypeResolver batchResolver, string columnName, out QueryColumnBase col)
        {
            string columnKey = columnName.ToUpperInvariant();
            if (resolved_OutputColumns.TryGetValue(columnKey, out col))
            {
                return true;
            }
            if (TryResolveColumn(batchResolver, columnName))
            {
                if (resolved_OutputColumns.TryGetValue(columnKey, out col))
                {
                    return true;
                }
            }

            return false;
        }

        protected bool IsColumnResolved(string outputColumnName, out QueryColumnBase col)
        {
            return resolved_OutputColumns.TryGetValue(outputColumnName.ToUpperInvariant(), out col);
        }

        protected QueryColumnBase AddResolveOutputdColumn(QueryColumnBase resolved_column)
        {
            resolved_OutputColumns.Add(resolved_column.OutputColumnName.ToUpperInvariant(), resolved_column);
            return resolved_column;
        }

        protected virtual bool TryResolveColumn(BatchOutputColumnTypeResolver batchResolver, string columnName)
        {
            return false;
        }

        internal abstract QueryColumnBase TryResolveSelectedColumn(BatchOutputColumnTypeResolver batchResolver, string outputColumnName, string sourceColumnName);

        internal abstract bool TryResolveSourceColumnType(BatchOutputColumnTypeResolver batchResolver, string sourceColumnName, out DbType columnType);
    }

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
            throw new NotImplementedException(Key + "." + sourceColumnName + " => " + outputColumnName);
        }

        internal QueryColumnBase AddOutputColumn(string outputColumnName, string sourceColumnName, DbType? columnDbType)
        {
            if (IsColumnResolved(outputColumnName, out QueryColumnBase col))
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

        internal void AddOutputColumn(QueryColumnBase outputColumn)
        {
            if (!IsColumnResolved(outputColumn.OutputColumnName.ToUpperInvariant(), out QueryColumnBase col))
            {
                throw new NotSupportedException("Output column not registered.");
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

        internal override bool TryResolveSourceColumnType(BatchOutputColumnTypeResolver batchResolver, string sourceColumnName, out DbType columnType)
        {
            throw new NotImplementedException();
        }
    }


    [DebuggerDisplay("{DebuggerText}")]
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

            if (IsColumnResolved(outputColumnNameSafe, out QueryColumnBase col))
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
            SetAlias(udtf.Alias);
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

            return base.AddResolveOutputdColumn(new QueryColumnE(this, outputColumnName, sourceColumnName, columnDbType.Value));
        }

        internal override bool TryResolveSourceColumnType(BatchOutputColumnTypeResolver batchResolver, string sourceColumnName, out DbType columnType)
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
    }
}