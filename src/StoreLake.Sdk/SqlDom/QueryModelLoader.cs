﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using Microsoft.SqlServer.TransactSql.ScriptDom;

[assembly: DebuggerDisplay(@"\{Bzz = {BaseIdentifier.Value}}", Target = typeof(SchemaObjectName))]
[assembly: DebuggerDisplay(@"\{Bzz = {StoreLake.Sdk.SqlDom.SqlDomExtensions.WhatIsThis(SchemaObject)}}", Target = typeof(NamedTableReference))]
[assembly: DebuggerDisplay(@"{StoreLake.Sdk.SqlDom.SqlDomExtensions.WhatIsThis(this)}", Target = typeof(TSqlFragment))]
// see [DebuggerTypeProxy(typeof(HashtableDebugView))]
namespace StoreLake.Sdk.SqlDom
{
    internal static class QueryModelLoader
    {
        private sealed class QueryLoadingContext
        {
            internal readonly BatchLoadingContext BatchCtx;
            internal readonly WithCtesAndXmlNamespaces Ctes;
            internal readonly BatchOutputColumnTypeResolver BatchResolver;
            private readonly QueryLoadingContext ParentCtxOrNull;
            public QueryLoadingContext(BatchLoadingContext batchCtx)
            {
                BatchCtx = batchCtx;
                BatchResolver = BatchCtx.BatchResolver;
                Ctes = BatchCtx.Ctes;
            }
            public QueryLoadingContext(QueryLoadingContext parentCtx) // derived context
                : this(parentCtx.BatchCtx)
            {
                ParentCtxOrNull = parentCtx;
            }

            internal readonly IDictionary<string, QueryColumnSourceBase> sources = new SortedDictionary<string, QueryColumnSourceBase>(StringComparer.OrdinalIgnoreCase);
            internal void RegisterQueryColumnSource(QueryColumnSourceBase source)
            {
                sources.Add(source.Key, source);
            }

            internal bool TryFindSource(string sourceNameOrAlias, out QueryColumnSourceBase source)
            {
                if (sources.TryGetValue(sourceNameOrAlias, out source))
                    return true;
                if (ParentCtxOrNull != null && ParentCtxOrNull.TryFindSource(sourceNameOrAlias, out source))
                    return true;
                return false;
            }
        }
        private sealed class BatchLoadingContext
        {
            internal readonly BatchOutputColumnTypeResolver BatchResolver;
            internal readonly WithCtesAndXmlNamespaces Ctes;
            public BatchLoadingContext(BatchOutputColumnTypeResolver batchResolver, WithCtesAndXmlNamespaces ctes)
            {
                BatchResolver = batchResolver;
                Ctes = ctes;
            }

            private readonly IDictionary<string, QuerySourceOnCte> loading_ctes = new SortedDictionary<string, QuerySourceOnCte>(StringComparer.OrdinalIgnoreCase);
            internal void PushCte(string name, QuerySourceOnCte cte_source)
            {
                loading_ctes.Add(name, cte_source);
            }

            internal QuerySourceOnCte IsRecursiveCTE(string cteName)
            {
                return loading_ctes.TryGetValue(cteName, out QuerySourceOnCte cte)
                    ? cte
                    : null;
            }
        }
        private sealed class QueryOrUnion
        {
            internal readonly List<QuerySpecificationModel> queries = new List<QuerySpecificationModel>();
            private readonly IDictionary<string, QuerySpecificationModel> keys = new SortedDictionary<string, QuerySpecificationModel>();
            internal void AddQuery(QuerySpecificationModel source)
            {
                keys.Add(source.Key, source);
                queries.Add(source);
            }
        }

        internal static IQueryModel LoadModificationOutputModel(BatchOutputColumnTypeResolver batchResolver, string queryName, DataModificationSpecification spec, WithCtesAndXmlNamespaces ctes)
        {
            QueryColumnSourceFactory sourceFactory = new QueryColumnSourceFactory();
            return LoadQueryOnModificationOutputModel(new BatchLoadingContext(batchResolver, ctes), sourceFactory, queryName, spec, ctes);
        }

        internal static IQueryModel LoadModel(BatchOutputColumnTypeResolver batchResolver, string queryName, QueryExpression qryExpr, WithCtesAndXmlNamespaces ctes)
        {
            BatchLoadingContext bctx = new BatchLoadingContext(batchResolver, ctes);
            QueryLoadingContext ctx = new QueryLoadingContext(bctx);
            return LoadModelCore(ctx, queryName, qryExpr, ctes);
        }
        private static IQueryModel LoadModelCore(QueryLoadingContext ctx, string queryName, QueryExpression qryExpr, WithCtesAndXmlNamespaces ctes)
        {
            QueryColumnSourceFactory sourceFactory = new QueryColumnSourceFactory();

            if (qryExpr is BinaryQueryExpression bqExpr)
            {
                var qspecs = new List<QuerySpecification>();
                CollectQuerySpecifications(qspecs, qryExpr);

                QueryUnionModel union = sourceFactory.NewRootUnion(qspecs.ToArray(), queryName);

                //foreach (var qspec in qspecs)
                for (int ix = 0; ix < qspecs.Count; ix++)
                {
                    var qspec = qspecs[ix];
                    string name = "q_" + (ix + 1) + "_" + qspecs.Count;
                    QueryLoadingContext q_ctx = new QueryLoadingContext(ctx);
                    var qrymodel = LoadQuerySpecificationModel(q_ctx, ctes, sourceFactory, qspec, name);
                    union.AddUnionQuery(qrymodel);
                }

                // in-union column resolving
                UnionResolve(ctx, union);

                return union;
            }
            else
            {
                return LoadQuerySpecificationModel(ctx, ctes, sourceFactory, (QuerySpecification)qryExpr, queryName);
            }
        }

        private static IQueryModel LoadQueryOnModificationOutputModel(BatchLoadingContext ctx, QueryColumnSourceFactory sourceFactory, string queryName, DataModificationSpecification mspec, WithCtesAndXmlNamespaces ctes)
        {
            QueryOnModificationOutputModel qmodel = sourceFactory.NewRootOnModificationOutput(mspec, queryName);

            QueryColumnSourceBase source;
            if (mspec.Target is NamedTableReference ntRef)
            {
                QueryColumnSourceNT table = sourceFactory.NewQueryColumnSourceNT(qmodel, ntRef);
                source = table;
            }
            else
            {
                throw new NotImplementedException(mspec.Target.WhatIsThis());
            }

            qmodel.SetTargetAsSource(source);

            foreach (SelectElement se in mspec.OutputClause.SelectColumns)
            {
                if (se is SelectScalarExpression sscalarExpr)
                {
                    if (StatementOutputColumnTypeResolverV2.TryGetOutputColumnName(sscalarExpr, out string sourceColumnName))
                    {
                        if (source.TryResolveSourceColumnType(ctx.BatchResolver, sourceColumnName, out SourceColumnType columnDbType))
                        {
                            qmodel.AddOutputColumn(new QueryColumnE(source, sourceColumnName, sourceColumnName, columnDbType.ColumnDbType, columnDbType.AllowNull));
                        }
                        else
                        {
                            throw new NotImplementedException(se.WhatIsThis());
                        }
                    }
                    else
                    {
                        // no column name => ColumnReferenceExpression
                        if (sscalarExpr.Expression is ColumnReferenceExpression colRef)
                        {
                            var colId = colRef.MultiPartIdentifier[colRef.MultiPartIdentifier.Count - 1];
                            sourceColumnName = colId.Dequote();
                            if (source.TryResolveSourceColumnType(ctx.BatchResolver, sourceColumnName, out SourceColumnType columnDbType))
                            {
                                qmodel.AddOutputColumn(new QueryColumnE(source, sourceColumnName, sourceColumnName, columnDbType.ColumnDbType, columnDbType.AllowNull));
                            }
                            else
                            {
                                throw new NotImplementedException(se.WhatIsThis());
                            }
                        }
                        else
                        {
                            throw new NotImplementedException(se.WhatIsThis());
                        }
                    }
                }
                else
                {
                    throw new NotImplementedException(se.WhatIsThis());
                }
            }

            return qmodel;
        }

        private static QuerySpecificationModel LoadQuerySpecificationModel(QueryLoadingContext ctx, WithCtesAndXmlNamespaces ctes, QueryColumnSourceFactory sourceFactory, QuerySpecification qrySpec, string queryName)
        {
            QuerySpecificationModel qmodel = sourceFactory.NewRootSpecification(qrySpec, queryName);

            // sources
            CollectQuerySpecificationSources(ctx, ctes, sourceFactory, qmodel);

            // in-select column resolving
            ResolveSelectedElements(ctx, ctes, sourceFactory, qmodel);

            if (qmodel.QrySpec.SelectElements.Count != qmodel.ResolvedOutputColumnsCount)
            {
                // for 'in-union' resolving
            }
            else
            {
                if (qmodel.HasResolvedOutputColumnWithoutType() != null)
                {
                    // for 'in-union' resolving
                }
                else
                {
                    qmodel.SetAsOutputResolved();
                }
            }

            if (qmodel.QrySpec.SelectElements.Count != qmodel.ResolvedOutputColumnsCount)
            {
                //throw new NotImplementedException("Not all root selected output column has been resolved. Selected:" + model_root.QrySpec.SelectElements.Count + ", Resolved:" + model_root.ResolvedOutputColumnsCount);
            }

            return qmodel;
        }

        private static void UnionResolve(QueryLoadingContext ctx, QueryUnionModel unionModel)
        {
            foreach (QuerySpecificationModel mqeN in unionModel.union_queries)
            {
                if (mqeN.IsQueryOutputResolved && (mqeN.HasResolvedOutputColumnWithoutType() == null))
                {
                    // cte?
                }
                else
                {
                    if (TryResolveUnionNullColumns(ctx, unionModel, mqeN))
                    {

                        if (mqeN.QrySpec.SelectElements.Count != mqeN.ResolvedOutputColumnsCount)
                        {
                            throw new NotImplementedException("Not all selected output column has been resolved. Selected:" + mqeN.QrySpec.SelectElements.Count + ", Resolved:" + mqeN.ResolvedOutputColumnsCount);
                        }

                        mqeN.SetAsOutputResolved();
                    }
                    else
                    {
                        // still not resolved due to NULL columns
                    }
                }
            }
        }

        private static void CollectQuerySpecifications(List<QuerySpecification> qspecs, QueryExpression qryExpr)
        {
            if (qryExpr is QuerySpecification qrySpec)
            {
                qspecs.Add(qrySpec);
            }
            else if (qryExpr is BinaryQueryExpression bqExpr)
            {
                CollectQuerySpecifications(qspecs, bqExpr.FirstQueryExpression);
                CollectQuerySpecifications(qspecs, bqExpr.SecondQueryExpression);
            }
            else
            {
                throw new NotImplementedException(qryExpr.WhatIsThis());
            }
        }

        private static void CollectQuerySpecificationSources(QueryLoadingContext ctx, WithCtesAndXmlNamespaces ctes, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel source)
        {
            if (source.QrySpec.FromClause == null)
            {
                // SELECT without FROM
            }
            else
            {
                foreach (TableReference tableRef in source.QrySpec.FromClause.TableReferences)
                {
                    CollectTableRef(ctx, ctes, sourceFactory, source, tableRef, (ts) =>
                    {
                        ctx.RegisterQueryColumnSource(ts);
                        source.AddMqeColumnSource(ts);
                    });
                }
            }
        }

        private static void CollectTableRef(QueryLoadingContext ctx, WithCtesAndXmlNamespaces ctes, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel parent, TableReference tableRef, Action<QueryColumnSourceBase> collector)
        {
            if (tableRef is QualifiedJoin qJoin)
            {
                CollectTableRef(ctx, ctes, sourceFactory, parent, qJoin.FirstTableReference, collector);
                CollectTableRef(ctx, ctes, sourceFactory, parent, qJoin.SecondTableReference, collector);
            }
            else if (tableRef is UnqualifiedJoin uqJoin)
            {
                CollectTableRef(ctx, ctes, sourceFactory, parent, uqJoin.FirstTableReference, collector);
                CollectTableRef(ctx, ctes, sourceFactory, parent, uqJoin.SecondTableReference, collector);
            }
            else if (tableRef is NamedTableReference ntRef)
            {
                CollectNamedTableReference(ctx, ctes, sourceFactory, parent, ntRef, collector);
            }
            else if (tableRef is SchemaObjectFunctionTableReference udtfRef)
            {
                collector(sourceFactory.NewQueryColumnSourceUDTF(parent, udtfRef));
            }
            else if (tableRef is VariableTableReference varTableRef)
            {
                collector(sourceFactory.NewQueryColumnSourceVarTable(parent, varTableRef));
            }
            else if (tableRef is InlineDerivedTable derivedTable)
            {
                // VALUES
                CollectInlineDerivedTable(ctx, ctes, sourceFactory, parent, derivedTable, collector);
            }
            else if (tableRef is QueryDerivedTable qDerivedTable)
            {
                // SubQuery
                CollectQueryDerivedTable(ctx, ctes, sourceFactory, parent, qDerivedTable, collector);
            }
            else if (tableRef is FullTextTableReference fttRef)
            {
                collector(sourceFactory.NewFullTextTable(parent, fttRef));
            }
            else
            {
                throw new NotImplementedException(tableRef.WhatIsThis());
            }
        }

        private static void CollectQueryDerivedTable(QueryLoadingContext ctx, WithCtesAndXmlNamespaces ctes, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel parent, QueryDerivedTable derivedTable, Action<QueryColumnSourceBase> collector)
        {
            //NewSourceOnCte
            string cteName = "subquery";
            // continue the query context
            QueryLoadingContext q_ctx = new QueryLoadingContext(ctx);
            IQueryModel qmodel = QueryModelLoader.LoadModelCore(q_ctx, cteName, derivedTable.QueryExpression, ctes);

            string key = derivedTable.Alias.Dequote();
            var source = sourceFactory.NewSourceOnQueryDerivedTable(parent, key, qmodel);
            collector(source);
        }

        private static void CollectInlineDerivedTable(QueryLoadingContext ctx, WithCtesAndXmlNamespaces ctes, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel parent, InlineDerivedTable derivedTable, Action<QueryColumnSourceBase> collector)
        {
            var source = sourceFactory.NewQueryColumnSourceValues(parent, derivedTable);

            for (int ix = 0; ix < derivedTable.Columns.Count; ix++)
            {
                var col = derivedTable.Columns[ix];

                string valuesColumnName = col.Dequote();
                ColumnTypeMetadata columnDbType = null;
                foreach (RowValue row in derivedTable.RowValues)
                {
                    foreach (ScalarExpression rowValue in row.ColumnValues)
                    {
                        if (TryResolveScalarExpression(ctx, ctes, sourceFactory, parent, rowValue, valuesColumnName, out SourceColumn column))
                        {
                            if (column.ColumnDbType.HasValue)
                            {
                                columnDbType = new ColumnTypeMetadata(column.ColumnDbType.Value, column.AllowNull.Value);
                            }
                        }

                        if (columnDbType != null)
                        {
                            break;
                        }
                    }
                    if (columnDbType != null)
                    {
                        break;
                    }
                }

                source.AddValueColumn(valuesColumnName, columnDbType.ColumnDbType, columnDbType.AllowNull);
            }

            collector(source);
        }

        private static void CollectNamedTableReference(QueryLoadingContext ntref_ctx, WithCtesAndXmlNamespaces ctes, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel parent, NamedTableReference node, Action<QueryColumnSourceBase> collector)
        {
            if (node.SchemaObject.SchemaIdentifier == null &&
                ctes != null &&
                TryGetCTEByName(ctes, node.SchemaObject.BaseIdentifier.Dequote(), out CommonTableExpression cte))
            {
                QueryLoadingContext ctex_ctx = new QueryLoadingContext(ntref_ctx.BatchCtx);
                string cteName = cte.ExpressionName.Dequote();

                QueryExpression cte_QueryExpression;
                if (IsRecursiveCTE(cteName, ctes, cte.QueryExpression))
                {
                    BinaryQueryExpression bqExpr = (BinaryQueryExpression)cte.QueryExpression;
                    cte_QueryExpression = bqExpr.FirstQueryExpression;
                }
                else
                {
                    cte_QueryExpression = cte.QueryExpression;
                }

                string key = node.Alias != null ? node.Alias.Dequote() : node.SchemaObject.BaseIdentifier.Dequote();

                //QuerySourceOnCte cte_source = null;// ctx.IsRecursiveCTE(cteName);
                //if (cte_source != null) // if recursive => take the first one and ignore the 'recursive'/second query
                //{
                //    cte_source.SetAsRecursive();
                //
                //    var src = sourceFactory.NewSourceOnRecursiveCte(parent, key, cte_source);
                //    collector(src);
                //}
                //else
                {
                    QuerySourceOnCte cte_source = sourceFactory.NewSourceOnCte(parent, key);
                    //ctx.PushCte(cteName, cte_source);

                    IQueryModel cte_qmodel = QueryModelLoader.LoadModelCore(ctex_ctx, cteName, cte_QueryExpression, ctes);
                    //if (cte_source.IsRecursive())
                    //{
                    //    ((QueryUnionModel)cte_qmodel).SetAsRecursive();
                    //}
                    cte_source.SetCteQuery(cte.Columns, cte_qmodel);

                    collector(cte_source);
                }
            }
            else
            {
                collector(sourceFactory.NewQueryColumnSourceNT(parent, node));
            }
        }

        private static bool IsRecursiveCTE(string cteName, WithCtesAndXmlNamespaces ctes, QueryExpression qryExpr)
        {
            if (qryExpr is QuerySpecification qspec)
            {
                return IsRecursiveCTE_QuerySpecification(cteName, ctes, qspec);
            }
            else if (qryExpr is BinaryQueryExpression bqExpr)
            {
                if (IsRecursiveCTE(cteName, ctes, bqExpr.FirstQueryExpression))
                    return true;
                return IsRecursiveCTE(cteName, ctes, bqExpr.SecondQueryExpression);
            }
            else
            {
                throw new NotImplementedException(qryExpr.WhatIsThis());
            }
        }

        private static bool IsRecursiveCTE_QuerySpecification(string cteName, WithCtesAndXmlNamespaces ctes, QuerySpecification qrySpec)
        {
            if (qrySpec.FromClause == null)
            {
                // SELECT without FROM
            }
            else
            {
                foreach (TableReference tableRef in qrySpec.FromClause.TableReferences)
                {
                    if (IsRecursiveCTE_TableReference(cteName, ctes, tableRef))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsRecursiveCTE_TableReference(string cteName, WithCtesAndXmlNamespaces ctes, TableReference tableRef)
        {
            if (tableRef is NamedTableReference ntRef)
            {
                if (ntRef.SchemaObject.SchemaIdentifier == null &&
                               ctes != null &&
                               TryGetCTEByName(ctes, ntRef.SchemaObject.BaseIdentifier.Dequote(), out CommonTableExpression cte))
                {
                    string cteNameX = cte.ExpressionName.Dequote();
                    if (string.Equals(cteName, cteNameX, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }
            if (tableRef is QualifiedJoin qJoin)
            {
                if (IsRecursiveCTE_TableReference(cteName, ctes, qJoin.FirstTableReference))
                    return true;
                return IsRecursiveCTE_TableReference(cteName, ctes, qJoin.SecondTableReference);
            }
            else if (tableRef is UnqualifiedJoin uqJoin)
            {
                if (IsRecursiveCTE_TableReference(cteName, ctes, uqJoin.FirstTableReference))
                    return true;
                return IsRecursiveCTE_TableReference(cteName, ctes, uqJoin.SecondTableReference);
            }
            else if (tableRef is SchemaObjectFunctionTableReference udtfRef)
            {
                return false;
            }
            else if (tableRef is VariableTableReference varTableRef)
            {
                return false;
            }
            else if (tableRef is InlineDerivedTable derivedTable)
            {
                // VALUES
                return false;
            }
            else if (tableRef is QueryDerivedTable qDerivedTable)
            {
                // CROSS APPLY
                return false;
            }
            else
            {
                throw new NotImplementedException(tableRef.WhatIsThis());
            }
        }

        private static bool TryGetCTEByName(WithCtesAndXmlNamespaces ctes, string sourceName, out CommonTableExpression result)
        {
            foreach (CommonTableExpression cte in ctes.CommonTableExpressions)
            {
                string cteName = cte.ExpressionName.Dequote();

                //if (ShouldTrySourceOrAlias(sourceNameOrAlias, cte.ExpressionName))
                if (string.Equals(sourceName, cteName, StringComparison.OrdinalIgnoreCase))
                {
                    result = cte;
                    return true;
                }
            }

            result = null;
            return false;
        }

        private static void ResolveSelectedElements(QueryLoadingContext ctx, WithCtesAndXmlNamespaces ctes, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel mqe)
        {
            foreach (SelectElement se in mqe.QrySpec.SelectElements)
            {
                if (se is SelectScalarExpression sscalarExpr)
                {

                    if (StatementOutputColumnTypeResolverV2.TryGetOutputColumnName(sscalarExpr, out string outputColumnName))
                    {
                    }

                    if (TryResolveScalarExpression(ctx, ctes, sourceFactory, mqe, sscalarExpr.Expression, outputColumnName, out SourceColumn col))
                    {
                        string outputColumnNameSafe = outputColumnName ?? col.SourceColumnName;
                        //outputColumnNameSafe = outputColumnNameSafe ?? sourceFactory.NewNameForColumn(mqe, )
                        if (col.ColumnDbType.HasValue)
                        {
                            mqe.AddOutputColumn(new QueryColumnE(col.Source, outputColumnNameSafe, col.SourceColumnName, col.ColumnDbType.Value, col.AllowNull.Value));
                        }
                        else
                        {
                            mqe.AddOutputColumn(new QueryColumnE(col.Source, outputColumnNameSafe, col.SourceColumnName, null, null));
                        }
                    }
                    else
                    {
                        // NullLiteral ?!?
                        if (!string.IsNullOrEmpty(outputColumnName))
                        {
                            // column or its type cannot be resolved;
                            mqe.AddOutputColumn(new QueryColumnE(col.Source, outputColumnName, outputColumnName, null, null));
                        }
                        else
                        {
                            // no output column name, no source column, or type!!!
                            throw new NotImplementedException(se.WhatIsThis());
                        }
                    }
                }
                else
                {
                    throw new NotImplementedException(se.WhatIsThis());
                }
            }
        }

        private static bool TryResolveUnionNullColumns(QueryLoadingContext ctx, QueryUnionModel unionModel, QuerySpecificationModel mqe)
        {
            QueryColumnBase[] cols = mqe.CollectResolvedOutputColumnWithoutType();
            //if (mqe.union_queries.Count == 0)
            //{
            //    return false;
            //}
            //else
            {
                foreach (QueryColumnBase col in cols)
                {
                    foreach (IQueryModel qry in unionModel.union_queries)
                    {
                        if (qry.TryGetQueryOutputColumnByName(ctx.BatchResolver, col.OutputColumnName, out QueryColumnBase outputColumnX))
                        {
                            if (outputColumnX.ColumnDbType.HasValue && !col.ColumnDbType.HasValue)
                            {
                                col.SetColumnDbType(outputColumnX.ColumnDbType.Value);
                            }
                        }
                        if (col.ColumnDbType.HasValue)
                        {
                            break;
                        }
                    }

                    if (col.ColumnDbType.HasValue)
                    {
                        // cool!
                    }
                    else
                    {
                        // still not resolved => default type INT?
                        //throw new NotSupportedException("Output column type not resolved:" + col.OutputColumnName);
                    }
                }
            }

            if (mqe.QrySpec.SelectElements.Count != mqe.ResolvedOutputColumnsCount)
            {
                foreach (SelectElement se in mqe.QrySpec.SelectElements)
                {
                }
                throw new NotImplementedException("Not all selected output column has been resolved. Selected:" + mqe.QrySpec.SelectElements.Count + ", Resolved:" + mqe.ResolvedOutputColumnsCount);
            }

            QueryColumnBase colsX = mqe.HasResolvedOutputColumnWithoutType();
            if (colsX != null)
            {
                //this query has 'NULL as col'
                return false;
            }

            return true;
        }

        private static bool TryResolveScalarExpression(QueryLoadingContext ctx, WithCtesAndXmlNamespaces ctes, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel mqe, ScalarExpression scalarExpr, string outputColumnName, out SourceColumn column)
        {
            if (scalarExpr == null)
            {
                throw new ArgumentNullException(nameof(scalarExpr));
            }

            if (scalarExpr is ColumnReferenceExpression colRef)
            {
                if (TryResolveColumnReferenceExpression(ctx, mqe, outputColumnName, colRef, out SourceColumn scol))
                {
                    //column = new SourceColumn(col.Source, col.SourceColumnName, col.ColumnDbType.Value);
                    column = scol;
                    return true;
                }
                else
                {
                    // source column not there or column type cannot be resolved!
                    column = null;
                    return false;
                }
            }
            else if (scalarExpr is NullLiteral nullLit)
            {
                return TryResolveNullLiteral(ctx, ctes, sourceFactory, mqe, outputColumnName, nullLit, out column);
            }
            else if (scalarExpr is FunctionCall fCall)
            {
                return TryResolveScalarFunctionCall(ctx, ctes, sourceFactory, mqe, fCall, outputColumnName, out column);
            }
            else if (scalarExpr is IntegerLiteral intLit)
            {
                string outputColumnNameSafe = outputColumnName ?? sourceFactory.NewNameForColumnLiteral(mqe, intLit);
                var source = sourceFactory.NewConstantSource(mqe, outputColumnNameSafe, DbType.Int32, false);
                column = new SourceColumn(source, outputColumnNameSafe, DbType.Int32, false);
                return true;
            }
            else if (scalarExpr is StringLiteral strLit)
            {
                string outputColumnNameSafe = outputColumnName ?? sourceFactory.NewNameForColumnLiteral(mqe, strLit);
                var source = sourceFactory.NewConstantSource(mqe, outputColumnNameSafe, DbType.String, true);
                column = new SourceColumn(source, outputColumnNameSafe, DbType.String, true);
                return true;
            }
            else if (scalarExpr is CastCall castExpr)
            {
                if (TryResolveScalarExpression(ctx, ctes, sourceFactory, mqe, castExpr.Parameter, outputColumnName, out SourceColumn scol))
                {
                    var dbType = ProcedureGenerator.ResolveToDbDataType(castExpr.DataType);
                    column = new SourceColumn(scol.Source, scol.SourceColumnName, dbType, scol.AllowNull.GetValueOrDefault(true));
                    return true;
                }
                else
                {
                    throw new NotImplementedException(scalarExpr.WhatIsThis());
                }
            }
            else if (scalarExpr is SearchedCaseExpression searchedCase)
            {
                return TryResolveSearchedCaseExpression(ctx, ctes, sourceFactory, mqe, outputColumnName, searchedCase, out column);
            }
            else if (scalarExpr is NullIfExpression nullIf)
            {
                SourceColumn functionOutputType = null;
                if (TryResolveScalarExpression(ctx, ctes, sourceFactory, mqe, nullIf.FirstExpression, outputColumnName, out SourceColumn outputColumnF))
                {
                    functionOutputType = outputColumnF;
                }

                if (TryResolveScalarExpression(ctx, ctes, sourceFactory, mqe, nullIf.SecondExpression, outputColumnName, out SourceColumn outputColumnS))
                {
                    functionOutputType = outputColumnS;
                }

                if (functionOutputType != null)
                {
                    column = functionOutputType;
                    return true;
                }

                throw new NotImplementedException(nullIf.FirstExpression.WhatIsThis());
                throw new NotImplementedException(nullIf.SecondExpression.WhatIsThis());
            }
            else if (scalarExpr is VariableReference varRef)
            {
                var varType = ctx.BatchResolver.TryGetScalarVariableType(varRef.Name);
                if (varType != null)
                {
                    string outputColumnNameSafe = outputColumnName ??
                        varRef.Name.Substring(1);
                    //sourceFactory.NewNameForColumnLiteral(mqe, binLit);
                    var source = sourceFactory.NewVariableSource(mqe, varRef, varType.ColumnDbType);
                    column = new SourceColumn(source, outputColumnNameSafe, varType.ColumnDbType, varType.AllowNull);
                    return true;
                }
                throw new NotImplementedException(varRef.WhatIsThis());
            }
            else if (scalarExpr is IIfCall iif)
            {
                SourceColumn functionOutputType = null;
                if (TryResolveScalarExpression(ctx, ctes, sourceFactory, mqe, iif.ThenExpression, outputColumnName, out SourceColumn outputColumnF))
                {
                    functionOutputType = outputColumnF;
                }

                if (TryResolveScalarExpression(ctx, ctes, sourceFactory, mqe, iif.ElseExpression, outputColumnName, out SourceColumn outputColumnS))
                {
                    if (functionOutputType != null && functionOutputType.ColumnDbType.HasValue)
                    {
                        //already resolved
                    }
                    else
                    {
                        functionOutputType = outputColumnS;
                    }
                }

                if (functionOutputType != null)
                {
                    column = functionOutputType;
                    return true;
                }

                throw new NotImplementedException(iif.ThenExpression.WhatIsThis());
                throw new NotImplementedException(iif.ElseExpression.WhatIsThis());
            }
            else if (scalarExpr is SimpleCaseExpression simpleCaseExpr)
            {
                return TryResolveSimpleCaseExpression(ctx, ctes, sourceFactory, mqe, outputColumnName, simpleCaseExpr, out column);
            }
            else if (scalarExpr is CoalesceExpression coalesceExpr)
            {
                return TryResolveCoalesceExpression(ctx, ctes, sourceFactory, mqe, outputColumnName, coalesceExpr, out column);
            }
            else if (scalarExpr is ConvertCall convertExpr)
            {
                return TryResolveConvertCall(ctx, ctes, sourceFactory, mqe, outputColumnName, convertExpr, out column);
            }
            else if (scalarExpr is BinaryExpression binaryExpr)
            {
                return TryResolveBinaryExpression(ctx, ctes, sourceFactory, mqe, outputColumnName, binaryExpr, out column);
            }
            else if (scalarExpr is BinaryLiteral binLit)
            {
                if (binLit.Value.StartsWith("0x") && binLit.Value.Length <= 10)
                {
                    uint value = Convert.ToUInt32(binLit.Value, 16);  //Using ToUInt32 not ToUInt64, as per OP comment

                    string outputColumnNameSafe = outputColumnName ?? sourceFactory.NewNameForColumnLiteral(mqe, binLit);
                    var source = sourceFactory.NewConstantSource(mqe, outputColumnNameSafe, DbType.UInt32, false);
                    column = new SourceColumn(source, outputColumnNameSafe, DbType.UInt32, false);
                    return true;
                }
                else
                {
                    throw new NotImplementedException(binLit.AsText());
                }
            }
            else if (scalarExpr is ParenthesisExpression ptsExpr)
            {
                return TryResolveScalarExpression(ctx, ctes, sourceFactory, mqe, ptsExpr.Expression, outputColumnName, out column);
            }
            else if (scalarExpr is UnaryExpression unaryExpr)
            {
                return TryResolveScalarExpression(ctx, ctes, sourceFactory, mqe, unaryExpr.Expression, outputColumnName, out column);
            }
            else if (scalarExpr is LeftFunctionCall leftCall)
            {
                return TryResolveLeftFunctionCall(ctx, ctes, sourceFactory, mqe, leftCall, outputColumnName, out column);
            }
            else if (scalarExpr is ScalarSubquery scalarQry)
            {
                return TryResolveScalarSubquery(ctx, ctes, sourceFactory, mqe, scalarQry, outputColumnName, out column);
            }
            else
            {
                throw new NotImplementedException(scalarExpr.WhatIsThis());
            }
        }

        private static bool TryResolveScalarSubquery(QueryLoadingContext ctx, WithCtesAndXmlNamespaces ctes, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel mqe, ScalarSubquery scalarQry, string outputColumnName, out SourceColumn column)
        {
            string qName = "subquery:" + outputColumnName;
            // continue the query context
            QueryLoadingContext q_ctx = new QueryLoadingContext(ctx);
            IQueryModel qmodel = QueryModelLoader.LoadModelCore(q_ctx, qName, scalarQry.QueryExpression, ctes);

            if (qmodel.TryGetQueryOutputColumnByName(ctx.BatchResolver, outputColumnName, out QueryColumnBase col))
            {
                column = new SourceColumn(col.Source, outputColumnName, col.ColumnDbType.Value, col.AllowNull.Value);
                return true;
            }

            // the query must return single column => use the type from it!!
            if (qmodel.TryGetQuerySingleOutputColumn(ctx.BatchResolver, out col))
            {
                column = new SourceColumn(col.Source, outputColumnName, col.ColumnDbType.Value, col.AllowNull.Value);
                return true;
            }

            throw new NotImplementedException(scalarQry.WhatIsThis());
        }

        private static bool TryResolveBinaryExpression(QueryLoadingContext ctx, WithCtesAndXmlNamespaces ctes, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel mqe, string outputColumnName, BinaryExpression binaryExpr, out SourceColumn column)
        {

            if ((binaryExpr.BinaryExpressionType == BinaryExpressionType.Add) // Add: 0 + 0x0100*MAX(IIF(aoa_g.am & 0x0100 = 0x0100, 1, 0))
             || (binaryExpr.BinaryExpressionType == BinaryExpressionType.Multiply) //Multiply: 0x0100*MAX(IIF(aoa_g.am & 0x0100 = 0x0100, 1, 0))
             || (binaryExpr.BinaryExpressionType == BinaryExpressionType.Subtract) //Subtract: settingid - 100
             || (binaryExpr.BinaryExpressionType == BinaryExpressionType.BitwiseAnd) //BitwiseAnd: OAA.accessmask & 512
             || (binaryExpr.BinaryExpressionType == BinaryExpressionType.Divide) //Divide: DATALENGTH(@delimiter) / 2
                )
            {
                SourceColumn result = null;
                if (TryResolveScalarExpression(ctx, ctes, sourceFactory, mqe, binaryExpr.FirstExpression, outputColumnName, out SourceColumn colF))
                {
                    result = colF;
                }

                if (TryResolveScalarExpression(ctx, ctes, sourceFactory, mqe, binaryExpr.SecondExpression, outputColumnName, out SourceColumn colS))
                {
                    result = colS;
                }
                if (result != null)
                {
                    column = result;
                    return true;
                }
                throw new NotImplementedException(" (" + binaryExpr.BinaryExpressionType + ") =>" + binaryExpr.WhatIsThis());
            }
            throw new NotImplementedException(" (" + binaryExpr.BinaryExpressionType + ") =>" + binaryExpr.WhatIsThis());
        }

        private static bool TryResolveConvertCall(QueryLoadingContext ctx, WithCtesAndXmlNamespaces ctes, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel mqe, string outputColumnName, ConvertCall convertExpr, out SourceColumn column)
        {
            if (TryResolveScalarExpression(ctx, ctes, sourceFactory, mqe, convertExpr.Parameter, outputColumnName, out SourceColumn scol))
            {
                DbType dbType = ProcedureGenerator.ResolveToDbDataType(convertExpr.DataType);
                if (scol.AllowNull.HasValue)
                    column = new SourceColumn(scol.Source, scol.SourceColumnName, dbType, scol.AllowNull.Value);
                else
                    column = new SourceColumn(scol.Source, scol.SourceColumnName, dbType, true);
                return true;
            }
            else
            {
                throw new NotImplementedException(convertExpr.WhatIsThis());
            }
        }

        private static bool TryResolveCoalesceExpression(QueryLoadingContext ctx, WithCtesAndXmlNamespaces ctes, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel mqe, string outputColumnName, CoalesceExpression coalesceExpr, out SourceColumn column)
        {
            SourceColumn result_column = null;
            foreach (ScalarExpression scalarExpr in coalesceExpr.Expressions)
            {
                if (TryResolveScalarExpression(ctx, ctes, sourceFactory, mqe, scalarExpr, outputColumnName, out SourceColumn col))
                {
                    result_column = col;
                }
                else
                {
                    // next one?
                }
            }

            if (result_column != null)
            {
                column = result_column;
                return true;
            }

            throw new NotImplementedException(coalesceExpr.WhatIsThis());
        }

        private static bool TryResolveSimpleCaseExpression(QueryLoadingContext ctx, WithCtesAndXmlNamespaces ctes, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel mqe, string outputColumnName, SimpleCaseExpression simpleCase, out SourceColumn column)
        {
            foreach (var whenExpr in simpleCase.WhenClauses)
            {
                if (TryResolveScalarExpression(ctx, ctes, sourceFactory, mqe, whenExpr.ThenExpression, outputColumnName, out column))
                {
                    //outputColumn.SetColumnDbType(outputColumnDbType);
                    return true;
                }
            }

            if (TryResolveScalarExpression(ctx, ctes, sourceFactory, mqe, simpleCase.ElseExpression, outputColumnName, out column))
            {
                //outputColumn.SetColumnDbType(functionOutputDbType);
                return true;
            }

            throw new NotImplementedException(simpleCase.WhatIsThis());
        }

        private static bool TryResolveSearchedCaseExpression(QueryLoadingContext ctx, WithCtesAndXmlNamespaces ctes, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel mqe, string outputColumnName, SearchedCaseExpression searchedCase, out SourceColumn column)
        {
            SourceColumn result_column = null;
            bool? allowNull = null;
            foreach (var whenExpr in searchedCase.WhenClauses)
            {
                if (TryResolveScalarExpression(ctx, ctes, sourceFactory, mqe, whenExpr.ThenExpression, outputColumnName, out SourceColumn columnW))
                {
                    //outputColumn.SetColumnDbType(outputColumnDbType);
                    result_column = columnW;
                    if (columnW.AllowNull.HasValue)
                    {
                        if (allowNull.HasValue && !allowNull.Value)
                            allowNull = columnW.AllowNull.Value; // try to change it to NULLable
                        else
                            allowNull = columnW.AllowNull.Value;
                    }
                    if (allowNull.HasValue && allowNull.Value)
                        break;
                    //                    return true;
                }
            }

            if (searchedCase.ElseExpression != null && TryResolveScalarExpression(ctx, ctes, sourceFactory, mqe, searchedCase.ElseExpression, outputColumnName, out SourceColumn columnE))
            {
                //outputColumn.SetColumnDbType(functionOutputDbType);
                result_column = columnE;
                if (columnE.AllowNull.HasValue)
                {
                    if (allowNull.HasValue && !allowNull.Value)
                        allowNull = columnE.AllowNull.Value; // try to change it to NULLable
                    else
                        allowNull = columnE.AllowNull.Value;
                }
                //return true;
            }

            if (result_column != null)
            {
                bool columnAllowNull = result_column.AllowNull.GetValueOrDefault(true)
                    || allowNull.GetValueOrDefault(true);

                if (result_column.AllowNull.HasValue && result_column.AllowNull.Value != columnAllowNull)
                {
                    // NOT NULL => NULL
                    column = new SourceColumn(result_column, true);
                }
                else
                {
                    if (!result_column.AllowNull.HasValue)
                    {
                        // no type =>????
                    }
                    column = result_column;
                }
                return true;
            }

            throw new NotImplementedException(searchedCase.WhatIsThis());
        }

        private static bool TryResolveNullLiteral(QueryLoadingContext ctx, WithCtesAndXmlNamespaces ctes, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel mqe, string outputColumnName, NullLiteral nullLit, out SourceColumn column)
        {
            //add as resolved?
            if (!string.IsNullOrEmpty(outputColumnName))
            {
                var source = sourceFactory.NewNullSource(mqe, outputColumnName);
                column = new SourceColumn(source, outputColumnName);
                return true;
            }
            else
            {
                // null is null
                column = null;
                return false;
            }
        }

        private static bool TryResolveColumnReferenceExpression(QueryLoadingContext ctx, QuerySpecificationModel mqe, string outputColumnName, ColumnReferenceExpression colRef, out SourceColumn column)
        {
            if (colRef.MultiPartIdentifier.Count == 2)
            {
                string sourceNameOrAlias = colRef.MultiPartIdentifier[0].Dequote();
                string sourceColumnName = colRef.MultiPartIdentifier[1].Dequote();

                if (mqe.TryFindSource(sourceNameOrAlias, out QueryColumnSourceBase source))
                {
                    if (TryResolveReferencedSourceColumn(ctx, mqe, source, outputColumnName, sourceColumnName, out column))
                    {
                        return true;
                    }
                    else
                    {
                        // source column not there or column type cannot be resolved!
                        return false;
                    }
                }
                else
                {
                    // source not in this query soecification... we are in the APPLY and all previous source should be there
                    if (ctx.TryFindSource(sourceNameOrAlias, out source))
                    {
                        if (TryResolveReferencedSourceColumn(ctx, mqe, source, outputColumnName, sourceColumnName, out column))
                        {
                            return true;
                        }
                        else
                        {
                            // source column not there or column type cannot be resolved!
                            return false;
                        }
                    }
                    else
                    {
                        throw new NotImplementedException(colRef.WhatIsThis());
                    }
                }
            }
            else if (colRef.MultiPartIdentifier.Count == 1)
            {
                // no source only column name => traverse all source and find t
                string sourceColumnName = colRef.MultiPartIdentifier[0].Dequote();
                //source.TryResolveSelectedColumn(columnName);
                if (mqe.SourceCount == 1)
                {
                    var source = mqe.SourceSingle;
                    if (TryResolveReferencedSourceColumn(ctx, mqe, source, outputColumnName, sourceColumnName, out column))
                    {
                        // coool!
                        return true;
                    }
                    else
                    {
                        // column not there?!?! wrong source
                        throw new NotImplementedException(colRef.WhatIsThis());
                    }
                }
                else
                {
                    // multiple sources
                    foreach (var source in mqe.sources.Values)
                    {
                        if (TryResolveReferencedSourceColumn(ctx, mqe, source, outputColumnName, sourceColumnName, out column))
                        {
                            // coool!
                            return true;
                        }
                        else
                        {
                            // column not there?!?! wrong source
                            //throw new NotImplementedException(colRef.WhatIsThis());
                        }
                    }
                    throw new NotImplementedException(colRef.WhatIsThis());
                }
            }
            else
            {
                throw new NotImplementedException(colRef.WhatIsThis());
            }
        }

        private static bool TryResolveReferencedSourceColumn(QueryLoadingContext ctx, QuerySpecificationModel mqe, QueryColumnSourceBase source, string outputColumnName, string sourceColumnName, out SourceColumn outputColumn)
        {
            if (source.TryResolveSourceColumnType(ctx.BatchResolver, sourceColumnName, out SourceColumnType columnDbType))
            {
                if (columnDbType.ColumnDbType.HasValue)
                {
                    // coool!
                    outputColumn = new SourceColumn(source, sourceColumnName, columnDbType.ColumnDbType.Value, columnDbType.AllowNull.Value);
                    return true;
                }
                else
                {
                    // almost there
                    outputColumn = new SourceColumn(source, sourceColumnName);
                    return true;
                }
            }
            else
            {
                // column not there?!?! wrong source
                outputColumn = null;
                return false;
            }
        }

        private static bool TryResolveScalarFunctionCall(QueryLoadingContext ctx, WithCtesAndXmlNamespaces ctes, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel mqe, FunctionCall fCall, string outputColumnName, out SourceColumn outputColumn)
        {
            string functionName = fCall.FunctionName.Dequote();
            if (string.Equals(functionName, "ISNULL", StringComparison.OrdinalIgnoreCase)
             || string.Equals(functionName, "MAX", StringComparison.OrdinalIgnoreCase)
             || string.Equals(functionName, "SUM", StringComparison.OrdinalIgnoreCase)
             || string.Equals(functionName, "MIN", StringComparison.OrdinalIgnoreCase)
                )
            {
                SourceColumn functionOutputType = null;
                for (int ix = 0; ix < fCall.Parameters.Count; ix++)
                {
                    ScalarExpression prm = fCall.Parameters[0];

                    if (TryResolveScalarExpression(ctx, ctes, sourceFactory, mqe, prm, outputColumnName, out SourceColumn outputColumn1))
                    {
                        if (functionOutputType == null)
                        {
                            functionOutputType = outputColumn1;
                        }
                    }
                    else
                    {
                        if (fCall.Parameters.Count == 1)
                        {
                            // last parameter : oops
                        }
                    }

                    if ((ix + 1) == fCall.Parameters.Count)
                    {
                        // last parameter
                        if (functionOutputType == null)
                        {
                            throw new NotImplementedException(fCall.WhatIsThis());
                        }
                    }
                }

                if (functionOutputType != null)
                {
                    outputColumn = functionOutputType;
                    return true;
                }

                throw new NotImplementedException(fCall.WhatIsThis());
            }

            if (string.Equals(functionName, "CONCAT", StringComparison.OrdinalIgnoreCase)
                )
            {
                SourceColumn functionOutputType = null;
                for (int ix = 0; ix < fCall.Parameters.Count; ix++)
                {
                    var prm = fCall.Parameters[0];

                    if (TryResolveScalarExpression(ctx, ctes, sourceFactory, mqe, prm, outputColumnName, out SourceColumn outputColumn1))
                    {
                        if (functionOutputType == null)
                        {
                            functionOutputType = outputColumn1;
                        }
                    }

                    if ((ix + 1) == fCall.Parameters.Count)
                    {
                        // last parameter
                        if (functionOutputType == null)
                        {
                            throw new NotImplementedException(fCall.WhatIsThis());
                        }
                    }
                }

                if (functionOutputType != null)
                {
                    if (functionOutputType.ColumnDbType != DbType.String)
                    {
                        outputColumn = new SourceColumn(functionOutputType.Source, functionOutputType.SourceColumnName, DbType.String, functionOutputType.AllowNull.Value);
                        return true;
                    }
                    else
                    {
                        outputColumn = functionOutputType;
                        return true;
                    }
                }

                throw new NotImplementedException(fCall.WhatIsThis());
            }

            if (string.Equals(functionName, "ROW_NUMBER", StringComparison.OrdinalIgnoreCase)
                )
            {
                string outputColumnNameSafe = outputColumnName ?? sourceFactory.NewNameForColumnInt64(mqe, 0);
                var source = sourceFactory.NewConstantSource(mqe, outputColumnNameSafe, DbType.Int64, true);
                outputColumn = new SourceColumn(source, outputColumnNameSafe, DbType.Int64, false);
                return true;
            }

            if (string.Equals(functionName, "DENSE_RANK", StringComparison.OrdinalIgnoreCase))
            {
                string outputColumnNameSafe = outputColumnName ?? sourceFactory.NewNameForColumnInt64(mqe, 0);
                var source = sourceFactory.NewConstantSource(mqe, outputColumnNameSafe, DbType.Int64, true);
                outputColumn = new SourceColumn(source, outputColumnNameSafe, DbType.Int64, false);
                return true;
            }

            if (string.Equals(functionName, "COUNT", StringComparison.OrdinalIgnoreCase))
            {
                string outputColumnNameSafe = outputColumnName ?? sourceFactory.NewNameForColumnInt32(mqe, 0);
                var source = sourceFactory.NewConstantSource(mqe, outputColumnNameSafe, DbType.Int32, true);
                outputColumn = new SourceColumn(source, outputColumnNameSafe, DbType.Int32, true);
                return true;
            }
            if (string.Equals(functionName, "HASHBYTES", StringComparison.OrdinalIgnoreCase))
            {
                string outputColumnNameSafe = outputColumnName ?? sourceFactory.NewNameForColumnBytes(mqe);
                var source = sourceFactory.NewConstantSource(mqe, outputColumnNameSafe, DbType.Byte, true);
                outputColumn = new SourceColumn(source, outputColumnNameSafe, DbType.Byte, true);
                return true;
            }
            if (string.Equals(functionName, "SUBSTRING", StringComparison.OrdinalIgnoreCase))
            {
                string outputColumnNameSafe = outputColumnName ?? sourceFactory.NewNameForColumnString(mqe);
                var source = sourceFactory.NewConstantSource(mqe, outputColumnNameSafe, DbType.String, true);
                outputColumn = new SourceColumn(source, outputColumnNameSafe, DbType.String, true);
                return true;
            }
            if (string.Equals(functionName, "REPLACE", StringComparison.OrdinalIgnoreCase))
            {
                string outputColumnNameSafe = outputColumnName ?? sourceFactory.NewNameForColumnString(mqe);
                var source = sourceFactory.NewConstantSource(mqe, outputColumnNameSafe, DbType.String, true);
                outputColumn = new SourceColumn(source, outputColumnNameSafe, DbType.String, true);
                return true;
            }
            if (string.Equals(functionName, "POWER", StringComparison.OrdinalIgnoreCase))
            {
                var prm = fCall.Parameters[0];
                if (TryResolveScalarExpression(ctx, ctes, sourceFactory, mqe, prm, outputColumnName, out outputColumn))
                {
                    if (outputColumn.ColumnDbType.HasValue)
                    {
                        // float, real => float 
                        // bit, char, nchar, varchar, nvarchar =>   float
                        if (outputColumn.ColumnDbType.Value == DbType.Boolean
                            || outputColumn.ColumnDbType.Value == DbType.String
                            || outputColumn.ColumnDbType.Value == DbType.StringFixedLength)
                        {
                            outputColumn = new SourceColumn(outputColumn, DbType.Double, true);
                        }

                        //int, smallint, tinyint  => int
                        if (outputColumn.ColumnDbType.Value == DbType.Byte
                            || outputColumn.ColumnDbType.Value == DbType.Int16)
                        {
                            outputColumn = new SourceColumn(outputColumn, DbType.Int32, true);
                        }
                    }
                    return true;
                }
                else
                {
                    throw new NotImplementedException(fCall.WhatIsThis());
                }
            }
            if (string.Equals(functionName, "AVG", StringComparison.OrdinalIgnoreCase))
            {
                ScalarExpression prm = fCall.Parameters[0];
                if (TryResolveScalarExpression(ctx, ctes, sourceFactory, mqe, prm, outputColumnName, out outputColumn))
                {
                    if (outputColumn.ColumnDbType.HasValue)
                    {
                        // float, real => float 
                        //int, smallint, tinyint  => int
                        if (outputColumn.ColumnDbType.Value == DbType.Byte
                            || outputColumn.ColumnDbType.Value == DbType.Int16)
                        {
                            outputColumn = new SourceColumn(outputColumn, DbType.Int32, true);
                        }
                    }
                    return true;
                }
                else
                {
                    throw new NotImplementedException(fCall.WhatIsThis());
                }
            }

            if (string.Equals(functionName, "DATEDIFF", StringComparison.OrdinalIgnoreCase))
            {
                // DATEDIFF + SECOND (3:Parameters)
                string datatepartName = ((ColumnReferenceExpression)fCall.Parameters[0]).MultiPartIdentifier.Identifiers[0].Dequote();

                ScalarExpression prm = fCall.Parameters[1];
                if (TryResolveScalarExpression(ctx, ctes, sourceFactory, mqe, prm, outputColumnName, out SourceColumn startdate))
                {
                    outputColumn = new SourceColumn(startdate, DbType.Int32, startdate.AllowNull.GetValueOrDefault(true));
                    return true;
                }
                throw new NotImplementedException(fCall.WhatIsThis());
            }

            if (string.Equals(functionName, "DATEADD", StringComparison.OrdinalIgnoreCase))
            {
                // DATEDIFF + SECOND (3:Parameters)
                string datatepartName = ((ColumnReferenceExpression)fCall.Parameters[0]).MultiPartIdentifier.Identifiers[0].Dequote();

                bool? number_allowNull = null;
                ScalarExpression prm1 = fCall.Parameters[1];
                if (TryResolveScalarExpression(ctx, ctes, sourceFactory, mqe, prm1, outputColumnName, out SourceColumn nmbr))
                {
                    number_allowNull = nmbr.AllowNull;
                }

                ScalarExpression prm2 = fCall.Parameters[1];
                if (TryResolveScalarExpression(ctx, ctes, sourceFactory, mqe, prm2, outputColumnName, out SourceColumn startdate))
                {
                    bool allowNull = number_allowNull.GetValueOrDefault(true)
                        || startdate.AllowNull.GetValueOrDefault(true)
                        || false;
                    outputColumn = new SourceColumn(startdate, DbType.Int32, allowNull);
                    return true;
                }

                throw new NotImplementedException(fCall.WhatIsThis());
            }

            if (string.Equals(functionName, "RANK", StringComparison.OrdinalIgnoreCase))
            {
                string outputColumnNameSafe = outputColumnName ?? sourceFactory.NewNameForColumnInt64(mqe, 0);
                var source = sourceFactory.NewConstantSource(mqe, outputColumnNameSafe, DbType.Int64, true);
                outputColumn = new SourceColumn(source, outputColumnNameSafe, DbType.Int64, false);
                return true;
            }
            if (string.Equals(functionName, "PATHNAME", StringComparison.OrdinalIgnoreCase))
            {
                string outputColumnNameSafe = outputColumnName ?? sourceFactory.NewNameForColumnString(mqe);
                var source = sourceFactory.NewConstantSource(mqe, outputColumnNameSafe, DbType.String, true);
                outputColumn = new SourceColumn(source, outputColumnNameSafe, DbType.String, true);
                return true;
            }
            if (string.Equals(functionName, "GET_FILESTREAM_TRANSACTION_CONTEXT", StringComparison.OrdinalIgnoreCase))
            {
                string outputColumnNameSafe = outputColumnName ?? sourceFactory.NewNameForColumnBytes(mqe);
                var source = sourceFactory.NewConstantSource(mqe, outputColumnNameSafe, DbType.Binary, true);
                outputColumn = new SourceColumn(source, outputColumnNameSafe, DbType.Binary, true);
                return true;
            }
            if (string.Equals(functionName, "CHARINDEX", StringComparison.OrdinalIgnoreCase))
            {
                // CHARINDEX ( expressionToFind , expressionToSearch [ , start_location ] )  
                string outputColumnNameSafe = outputColumnName ?? sourceFactory.NewNameForColumnInt64(mqe, 0);
                var source = sourceFactory.NewConstantSource(mqe, outputColumnNameSafe, DbType.Int64, true);
                outputColumn = new SourceColumn(source, outputColumnNameSafe, DbType.Int64, true);
                return true;
            }
            if (string.Equals(functionName, "value", StringComparison.OrdinalIgnoreCase))
            {
                //value(XQuery, SQLType)
                var prm_SQLType = (StringLiteral)fCall.Parameters[1];
                if (string.Equals(prm_SQLType.Value, "INT", StringComparison.OrdinalIgnoreCase))
                {
                    string outputColumnNameSafe = outputColumnName ?? sourceFactory.NewNameForColumnInt32(mqe, 0);
                    var source = sourceFactory.NewConstantSource(mqe, outputColumnNameSafe, DbType.Int32, true);
                    outputColumn = new SourceColumn(source, outputColumnNameSafe, DbType.Int32, true);
                    return true;
                }
                throw new NotImplementedException("xQyery value(,'" + prm_SQLType.Value + "')." + fCall.WhatIsThis());
            }

            if (string.Equals(functionName, "DATALENGTH", StringComparison.OrdinalIgnoreCase))
            {
                // MSDN:
                // DATALENGTH ( expression )   
                // bigint if expression has an nvarchar(max), varbinary(max), or varchar(max) data type; otherwise int.
                ScalarExpression prm = fCall.Parameters[0];
                if (TryResolveScalarExpression(ctx, ctes, sourceFactory, mqe, prm, outputColumnName, out SourceColumn startdate))
                {
                }

                string outputColumnNameSafe = outputColumnName ?? sourceFactory.NewNameForColumnInt64(mqe, 0);
                var source = sourceFactory.NewConstantSource(mqe, outputColumnNameSafe, DbType.Int64, true);
                outputColumn = new SourceColumn(source, outputColumnNameSafe, DbType.Int64, true);
                return true;
            }
            if (string.Equals(functionName, "GETUTCDATE", StringComparison.OrdinalIgnoreCase))
            {
                string outputColumnNameSafe = outputColumnName ?? sourceFactory.NewNameForColumnDateTime(mqe);
                var source = sourceFactory.NewConstantSource(mqe, outputColumnNameSafe, DbType.DateTime, true);
                outputColumn = new SourceColumn(source, outputColumnNameSafe, DbType.DateTime, true);
                return true;
            }
            if (fCall.CallTarget != null && fCall.CallTarget is MultiPartIdentifierCallTarget mpiCallTarget && mpiCallTarget.MultiPartIdentifier != null && mpiCallTarget.MultiPartIdentifier.Count == 1 && mpiCallTarget.MultiPartIdentifier[0].Value == "dbo")
            {
                // dbo.hlsysagent_query_language(@agentid, @primaryclientlanguage)
                if (functionName == "hlsysagent_query_language" && fCall.Parameters != null && fCall.Parameters.Count == 2)
                {
                    var prm_agentid = (VariableReference)fCall.Parameters[0];
                    var prm_languag = (VariableReference)fCall.Parameters[1];

                    string outputColumnNameSafe = outputColumnName ?? sourceFactory.NewNameForColumnInt32(mqe, 0);
                    var source = sourceFactory.NewConstantSource(mqe, outputColumnNameSafe, DbType.Int32, true);
                    outputColumn = new SourceColumn(source, outputColumnNameSafe, DbType.Int32, true);
                    return true;
                }
            }

            throw new NotImplementedException(fCall.WhatIsThis());
        }
        private static bool TryResolveLeftFunctionCall(QueryLoadingContext ctx, WithCtesAndXmlNamespaces ctes, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel mqe, LeftFunctionCall fCall, string outputColumnName, out SourceColumn outputColumn)
        {
            //string functionName = fCall.FunctionName.Dequote();
            {
                SourceColumn functionOutputType = null;
                for (int ix = 0; ix < fCall.Parameters.Count; ix++)
                {
                    var prm = fCall.Parameters[0];

                    if (TryResolveScalarExpression(ctx, ctes, sourceFactory, mqe, prm, outputColumnName, out SourceColumn outputColumn1))
                    {
                        if (functionOutputType == null)
                        {
                            functionOutputType = outputColumn1;
                        }
                    }

                    if ((ix + 1) == fCall.Parameters.Count)
                    {
                        // last parameter
                        if (functionOutputType == null)
                        {
                            throw new NotImplementedException(fCall.WhatIsThis());
                        }
                    }
                }

                if (functionOutputType != null)
                {
                    if (functionOutputType.ColumnDbType != DbType.String)
                    {
                        outputColumn = new SourceColumn(functionOutputType.Source, functionOutputType.SourceColumnName, DbType.String, true);
                        return true;
                    }
                    else
                    {
                        outputColumn = functionOutputType;
                        return true;
                    }
                }

                throw new NotImplementedException(fCall.WhatIsThis());
            }

            throw new NotImplementedException(fCall.WhatIsThis());
        }
    }
}