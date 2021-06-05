using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;

[assembly: DebuggerDisplay(@"\{Bzz = {BaseIdentifier.Value}}", Target = typeof(SchemaObjectName))]
[assembly: DebuggerDisplay(@"\{Bzz = {StoreLake.Sdk.SqlDom.SqlDomExtensions.WhatIsThis(SchemaObject)}}", Target = typeof(NamedTableReference))]
[assembly: DebuggerDisplay(@"{StoreLake.Sdk.SqlDom.SqlDomExtensions.WhatIsThis(this)}", Target = typeof(TSqlFragment))]
// see [DebuggerTypeProxy(typeof(HashtableDebugView))]
namespace StoreLake.Sdk.SqlDom
{
    internal abstract class QueryModelBase : IQueryModel
    {
        internal readonly int Id;
        internal readonly string Key;

        protected QueryModelBase(int id, string key)
        {
            Id = id;
            Key = key;
        }
        bool IQueryModel.TryGetQueryOutputColumn(BatchOutputColumnTypeResolver batchResolver, string outputColumnName, out QueryColumnBase outputColumn)
        {
            return IQueryModel_TryGetQueryOutputColumn(batchResolver, outputColumnName, out outputColumn);
        }

        protected abstract bool IQueryModel_TryGetQueryOutputColumn(BatchOutputColumnTypeResolver batchResolver, string outputColumnName, out QueryColumnBase outputColumn);

        bool IQueryModel.TryGetQuerySingleOutputColumn(BatchOutputColumnTypeResolver batchResolver, out QueryColumnBase outputColumn)
        {
            return IQueryModel_TryGetQuerySingleOutputColumn(batchResolver, out outputColumn);
        }

        protected abstract bool IQueryModel_TryGetQuerySingleOutputColumn(BatchOutputColumnTypeResolver batchResolver, out QueryColumnBase outputColumn);
    }


    [DebuggerDisplay("K:{Key}")]
    internal sealed class QuerySpecificationModel : QueryModelBase
    {
        internal readonly QuerySpecification QrySpec;
        internal readonly IDictionary<string, QueryColumnSourceBase> sources = new SortedDictionary<string, QueryColumnSourceBase>(StringComparer.OrdinalIgnoreCase);
        public QuerySpecificationModel(int id, QuerySpecification qspec, string key)
            : base(id, key)
        {
            QrySpec = qspec;
        }

        internal readonly Dictionary<string, QueryColumnBase> resolved_OutputColumns = new Dictionary<string, QueryColumnBase>(StringComparer.OrdinalIgnoreCase);
        private bool _queryOutputResolved;
        private void MarkAsOutputResolved() { _queryOutputResolved = true; }
        internal bool IsQueryOutputResolved => _queryOutputResolved;

        internal int ResolvedOutputColumnsCount => resolved_OutputColumns.Count;

        internal QueryColumnBase[] CollectResolvedOutputColumnWithoutType()
        {
            return resolved_OutputColumns.Values.Where(x => !x.ColumnDbType.HasValue).ToArray();
        }

        internal bool IsOutputColumnResolved(string outputColumnName, out QueryColumnBase col)
        {
            return resolved_OutputColumns.TryGetValue(outputColumnName, out col);
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

        internal void AddMqeColumnSource(QueryColumnSourceBase source)
        {
            sources.Add(source.Key, source);
        }

        internal int SourceCount => sources.Count;
        internal QueryColumnSourceBase SourceSingle
        {
            get
            {
                return sources.Values.Single();
            }
        }


        internal bool TryFindSource(string sourceNameOrAlias, out QueryColumnSourceBase source)
        {
            if (this.sources.TryGetValue(sourceNameOrAlias, out source))
            {
                return true;
            }

            return false;
        }

        private QueryColumnSourceBase ContainsSourceId(int sourceId)
        {
            foreach (var source in this.sources.Values)
            {
                if (source.Id == sourceId)
                    return source;
            }

            return null;
        }

        private QueryColumnBase AddResolveOutputColumn(QueryColumnBase resolved_column)
        {
            resolved_OutputColumns.Add(resolved_column.OutputColumnName, resolved_column);
            return resolved_column;
        }


        internal QueryColumnBase AddOutputColumn(QueryColumnBase outputColumn)
        {
            if (!IsOutputColumnResolved(outputColumn.OutputColumnName, out QueryColumnBase col))
            {
                QueryColumnSourceBase source = ContainsSourceId(outputColumn.SourceId);
                if (source != null)
                {
                    col = AddResolveOutputColumn(new QueryColumnE(source, outputColumn.OutputColumnName, outputColumn.SourceColumnName, outputColumn.ColumnDbType, outputColumn.AllowNull));
                }
                else
                {
                    if (outputColumn.Source is QuerySourceOnNull)
                    {
                        return AddResolveOutputColumn(outputColumn);
                    }
                    else if (outputColumn.Source is QuerySourceOnConstant)
                    {
                        return AddResolveOutputColumn(outputColumn);
                    }
                    else if (outputColumn.Source is QuerySourceOnVariable)
                    {
                        return AddResolveOutputColumn(outputColumn);
                    }
                    else
                    {
                        // APPLY ( SELECT ... => the source is not in this query specification!!
                        //throw new NotSupportedException("Output column not registered.");
                        return AddResolveOutputColumn(outputColumn);
                    }
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
                    // Output column type not resolved!
                }
            }

            if (!outputColumn.ColumnDbType.HasValue)
            {
                //                throw new ArgumentException("Output column type not resolved.", nameof(outputColumn));
            }

            return outputColumn;
        }

        internal void SetAsOutputResolved()
        {
            if (IsQueryOutputResolved)
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

            MarkAsOutputResolved();
        }

        protected override bool IQueryModel_TryGetQueryOutputColumn(BatchOutputColumnTypeResolver batchResolver, string outputColumnName, out QueryColumnBase outputColumn)
        {
            if (IsOutputColumnResolved(outputColumnName, out outputColumn))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        protected override bool IQueryModel_TryGetQuerySingleOutputColumn(BatchOutputColumnTypeResolver batchResolver, out QueryColumnBase outputColumn)
        {
            outputColumn = resolved_OutputColumns.Values.Single();
            return true;
        }

    }

    [DebuggerDisplay("UNION Id:{Id}, Key:{Key}")]
    internal sealed class QueryUnionModel : QueryModelBase
    {
        private readonly QuerySpecification[] QSpecs;
        internal readonly List<QuerySpecificationModel> union_queries = new List<QuerySpecificationModel>(); // UNION(s)

        public QueryUnionModel(int id, QuerySpecification[] qspecs, string key)
            : base(id, key)
        {
            QSpecs = qspecs;
        }


        internal void AddUnionQuery(QuerySpecificationModel query)
        {
            union_queries.Add(query);
        }

        protected override bool IQueryModel_TryGetQueryOutputColumn(BatchOutputColumnTypeResolver batchResolver, string outputColumnName, out QueryColumnBase outputColumn)
        {
            foreach (var sub_mqe in union_queries)
            {
                if (sub_mqe.IsOutputColumnResolved(outputColumnName, out outputColumn))
                {
                    //AddOutputColumn(outputColumn);
                    return true;
                }

                if (_isRecirsive)
                    break;
            }

            throw new NotImplementedException(Key + "." + outputColumnName); // source not found?!?!
        }

        protected override bool IQueryModel_TryGetQuerySingleOutputColumn(BatchOutputColumnTypeResolver batchResolver, out QueryColumnBase outputColumn)
        {
            outputColumn = union_queries[0].resolved_OutputColumns.Values.Single();
            return true;
        }


        private bool _isRecirsive;
        internal void SetAsRecursive()
        {
            _isRecirsive = true;
        }
    }

    [DebuggerDisplay("OUTPUT Id:{Id}, Key:{Key}")]
    internal sealed class QueryOnModificationOutputModel : QueryModelBase
    {
        private readonly DataModificationSpecification MSpec;

        public QueryOnModificationOutputModel(int id, DataModificationSpecification mspec, string key)
            : base(id, key)
        {
            MSpec = mspec;
        }

        private readonly IDictionary<string, QueryColumnBase> _outputColumns = new SortedDictionary<string, QueryColumnBase>(StringComparer.OrdinalIgnoreCase);
        internal void AddOutputColumn(QueryColumnBase column)
        {
            _outputColumns.Add(column.OutputColumnName, column);
        }

        protected override bool IQueryModel_TryGetQueryOutputColumn(BatchOutputColumnTypeResolver batchResolver, string outputColumnName, out QueryColumnBase outputColumn)
        {
            if (_outputColumns.TryGetValue(outputColumnName, out outputColumn))
            {
                return true;
            }
            else
            {
                return false;
            }

        }

        protected override bool IQueryModel_TryGetQuerySingleOutputColumn(BatchOutputColumnTypeResolver batchResolver, out QueryColumnBase outputColumn)
        {
            outputColumn = _outputColumns.Values.Single();
            return true;
        }


        private QueryColumnSourceBase _source;
        internal void SetTargetAsSource(QueryColumnSourceBase source)
        {
            _source = source;
        }
    }
}