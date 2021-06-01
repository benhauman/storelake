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
    internal static class QueryModelLoader
    {
        private sealed class LoadingContext
        {
            internal readonly BatchOutputColumnTypeResolver BatchResolver;
            internal readonly WithCtesAndXmlNamespaces Ctes;
            public LoadingContext(BatchOutputColumnTypeResolver batchResolver, WithCtesAndXmlNamespaces ctes)
            {
                BatchResolver = batchResolver;
                Ctes = ctes;
            }

            private readonly IDictionary<string, QuerySourceOnQuery> loading_ctes = new SortedDictionary<string, QuerySourceOnQuery>();
            internal void PushCte(string name, QuerySourceOnQuery cte_source)
            {
                loading_ctes.Add(name.ToUpperInvariant(), cte_source);
            }

            internal QuerySourceOnQuery IsRecursiveCTE(string cteName)
            {
                return loading_ctes.TryGetValue(cteName.ToUpperInvariant(), out QuerySourceOnQuery cte)
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
            return LoadQueryOnModificationOutputModel(new LoadingContext(batchResolver, ctes), sourceFactory, queryName, spec, ctes);
        }

        internal static IQueryModel LoadModel(BatchOutputColumnTypeResolver batchResolver, string queryName, QueryExpression qryExpr, WithCtesAndXmlNamespaces ctes)
        {
            return LoadModelCore(new LoadingContext(batchResolver, ctes), queryName, qryExpr, ctes);
        }
        private static IQueryModel LoadModelCore(LoadingContext ctx, string queryName, QueryExpression qryExpr, WithCtesAndXmlNamespaces ctes)
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
                    var qrymodel = LoadQuerySpecificationModel(ctx, sourceFactory, qspec, name, ctes);
                    union.AddUnionQuery(qrymodel);
                }

                // in-union column resolving
                UnionResolve(ctx, union);

                return union;
            }
            else
            {
                return LoadQuerySpecificationModel(ctx, sourceFactory, (QuerySpecification)qryExpr, queryName, ctes);
            }
        }

        private static IQueryModel LoadQueryOnModificationOutputModel(LoadingContext ctx, QueryColumnSourceFactory sourceFactory, string queryName, DataModificationSpecification mspec, WithCtesAndXmlNamespaces ctes)
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
                            qmodel.AddOutputColumn(new QueryColumnE(source, sourceColumnName, sourceColumnName, columnDbType.ColumnDbType));
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
                                qmodel.AddOutputColumn(new QueryColumnE(source, sourceColumnName, sourceColumnName, columnDbType.ColumnDbType));
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

        private static QuerySpecificationModel LoadQuerySpecificationModel(LoadingContext ctx, QueryColumnSourceFactory sourceFactory, QuerySpecification qrySpec, string queryName, WithCtesAndXmlNamespaces ctes)
        {
            QuerySpecificationModel qmodel = sourceFactory.NewRootSpecification(qrySpec, queryName);

            // sources
            CollectQuerySpecificationSources(ctx, sourceFactory, qmodel, ctes);

            // in-select column resolving
            ResolveSelectedElements(ctx, sourceFactory, qmodel);

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

        private static void UnionResolve(LoadingContext ctx, QueryUnionModel unionModel)
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

        private static void CollectQuerySpecificationSources(LoadingContext ctx, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel source, WithCtesAndXmlNamespaces ctes)
        {
            if (source.QrySpec.FromClause == null)
            {
                // SELECT without FROM
            }
            else
            {
                foreach (TableReference tableRef in source.QrySpec.FromClause.TableReferences)
                {
                    CollectTableRef(ctx, sourceFactory, source, tableRef, ctes, (ts) =>
                    {
                        source.AddMqeColumnSource(ts);
                    });
                }
            }
        }

        private static void CollectTableRef(LoadingContext ctx, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel parent, TableReference tableRef, WithCtesAndXmlNamespaces ctes, Action<QueryColumnSourceBase> collector)
        {
            if (tableRef is QualifiedJoin qJoin)
            {
                CollectTableRef(ctx, sourceFactory, parent, qJoin.FirstTableReference, ctes, collector);
                CollectTableRef(ctx, sourceFactory, parent, qJoin.SecondTableReference, ctes, collector);
            }
            else if (tableRef is UnqualifiedJoin uqJoin)
            {
                CollectTableRef(ctx, sourceFactory, parent, uqJoin.FirstTableReference, ctes, collector);
                CollectTableRef(ctx, sourceFactory, parent, uqJoin.SecondTableReference, ctes, collector);
            }
            else if (tableRef is NamedTableReference ntRef)
            {
                CollectNamedTableReference(ctx, sourceFactory, parent, ntRef, ctes, collector);
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
                CollectInlineDerivedTable(ctx, sourceFactory, parent, derivedTable, ctes, collector);
            }
            else if (tableRef is QueryDerivedTable qDerivedTable)
            {
                // SubQuery
                CollectQueryDerivedTable(ctx, sourceFactory, parent, qDerivedTable, ctes, collector);
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

        private static void CollectQueryDerivedTable(LoadingContext ctx, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel parent, QueryDerivedTable derivedTable, WithCtesAndXmlNamespaces ctes, Action<QueryColumnSourceBase> collector)
        {
            //NewSourceOnCte
            string cteName = "subquery";
            IQueryModel qmodel = QueryModelLoader.LoadModelCore(ctx, cteName, derivedTable.QueryExpression, ctes);

            string key = derivedTable.Alias.Dequote();
            var source = sourceFactory.NewSourceOnQueryDerivedTable(parent, key, qmodel);
            collector(source);
        }

        private static void CollectInlineDerivedTable(LoadingContext ctx, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel parent, InlineDerivedTable derivedTable, WithCtesAndXmlNamespaces ctes, Action<QueryColumnSourceBase> collector)
        {
            var source = sourceFactory.NewQueryColumnSourceValues(parent, derivedTable);

            for (int ix = 0; ix < derivedTable.Columns.Count; ix++)
            {
                var col = derivedTable.Columns[ix];

                string valuesColumnName = col.Dequote();
                DbType? columnDbType = null;
                foreach (RowValue row in derivedTable.RowValues)
                {
                    foreach (ScalarExpression rowValue in row.ColumnValues)
                    {
                        if (TryResolveScalarExpression(ctx, sourceFactory, parent, rowValue, valuesColumnName, out SourceColumn column))
                        {
                            if (column.ColumnDbType.HasValue)
                            {
                                columnDbType = column.ColumnDbType.Value;
                            }
                        }

                        if (columnDbType.HasValue)
                        {
                            break;
                        }
                    }
                    if (columnDbType.HasValue)
                    {
                        break;
                    }
                }

                source.AddValueColumn(valuesColumnName, columnDbType.Value);
            }

            collector(source);
        }

        private static void CollectNamedTableReference(LoadingContext ctx, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel parent, NamedTableReference node, WithCtesAndXmlNamespaces ctes, Action<QueryColumnSourceBase> collector)
        {
            if (node.SchemaObject.SchemaIdentifier == null &&
                ctes != null &&
                TryGetCTEByName(ctes, node.SchemaObject.BaseIdentifier.Dequote(), out CommonTableExpression cte))
            {
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

                QuerySourceOnQuery cte_source = null;// ctx.IsRecursiveCTE(cteName);
                //if (cte_source != null) // if recursive => take the first one and ignore the 'recursive'/second query
                //{
                //    cte_source.SetAsRecursive();
                //
                //    var src = sourceFactory.NewSourceOnRecursiveCte(parent, key, cte_source);
                //    collector(src);
                //}
                //else
                {
                    cte_source = sourceFactory.NewSourceOnCte(parent, key);
                    //ctx.PushCte(cteName, cte_source);

                    IQueryModel cte_qmodel = QueryModelLoader.LoadModelCore(ctx, cteName, cte_QueryExpression, ctes);
                    //if (cte_source.IsRecursive())
                    //{
                    //    ((QueryUnionModel)cte_qmodel).SetAsRecursive();
                    //}
                    cte_source.SetQuery(cte_qmodel);

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
            //else if (tableRef is QueryDerivedTable qDerivedTable)
            //{
            //    // SubQuery
            //    return false;
            //}

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

        private static void ResolveSelectedElements(LoadingContext ctx, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel mqe)
        {
            foreach (SelectElement se in mqe.QrySpec.SelectElements)
            {
                if (se is SelectScalarExpression sscalarExpr)
                {

                    if (StatementOutputColumnTypeResolverV2.TryGetOutputColumnName(sscalarExpr, out string outputColumnName))
                    {
                    }

                    if (TryResolveScalarExpression(ctx, sourceFactory, mqe, sscalarExpr.Expression, outputColumnName, out SourceColumn col))
                    {
                        string outputColumnNameSafe = outputColumnName ?? col.SourceColumnName;
                        //outputColumnNameSafe = outputColumnNameSafe ?? sourceFactory.NewNameForColumn(mqe, )
                        if (col.ColumnDbType.HasValue)
                        {
                            mqe.AddOutputColumn(new QueryColumnE(col.Source, outputColumnNameSafe, col.SourceColumnName, col.ColumnDbType.Value));
                        }
                        else
                        {
                            mqe.AddOutputColumn(new QueryColumnE(col.Source, outputColumnNameSafe, col.SourceColumnName, null));
                        }
                    }
                    else
                    {
                        // NullLiteral ?!?
                        if (!string.IsNullOrEmpty(outputColumnName))
                        {
                            // column or its type cannot be resolved;
                            mqe.AddOutputColumn(new QueryColumnE(col.Source, outputColumnName, outputColumnName, null));
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


        private static bool TryResolveUnionNullColumns(LoadingContext ctx, QueryUnionModel unionModel, QuerySpecificationModel mqe)
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
                        if (qry.TryGetQueryOutputColumn(ctx.BatchResolver, col.OutputColumnName, out QueryColumnBase outputColumnX))
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

        private static bool TryResolveScalarExpression(LoadingContext ctx, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel mqe, ScalarExpression scalarExpr, string outputColumnName, out SourceColumn column)
        {
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
                return TryResolveNullLiteral(ctx, sourceFactory, mqe, outputColumnName, nullLit, out column);
            }
            else if (scalarExpr is FunctionCall fCall)
            {
                return TryResolveFunctionCall(ctx, sourceFactory, mqe, fCall, outputColumnName, out column);
            }
            else if (scalarExpr is IntegerLiteral intLit)
            {
                string outputColumnNameSafe = outputColumnName ?? sourceFactory.NewNameForColumnLiteral(mqe, intLit);
                var source = sourceFactory.NewConstantSource(mqe, outputColumnNameSafe, DbType.Int32);
                column = new SourceColumn(source, outputColumnNameSafe, DbType.Int32);
                return true;
            }
            else if (scalarExpr is StringLiteral strLit)
            {
                string outputColumnNameSafe = outputColumnName ?? sourceFactory.NewNameForColumnLiteral(mqe, strLit);
                var source = sourceFactory.NewConstantSource(mqe, outputColumnNameSafe, DbType.String);
                column = new SourceColumn(source, outputColumnNameSafe, DbType.String);
                return true;
            }
            else if (scalarExpr is CastCall castExpr)
            {
                if (TryResolveScalarExpression(ctx, sourceFactory, mqe, castExpr.Parameter, outputColumnName, out SourceColumn scol))
                {
                    var dbType = ProcedureGenerator.ResolveToDbDataType(castExpr.DataType);
                    column = new SourceColumn(scol.Source, scol.SourceColumnName, dbType);
                    return true;
                }
                else
                {
                    throw new NotImplementedException(scalarExpr.WhatIsThis());
                }
            }
            else if (scalarExpr is SearchedCaseExpression searchedCase)
            {
                return TryResolveSearchedCaseExpression(ctx, sourceFactory, mqe, outputColumnName, searchedCase, out column);
            }
            else if (scalarExpr is NullIfExpression nullIf)
            {
                SourceColumn functionOutputType = null;
                if (TryResolveScalarExpression(ctx, sourceFactory, mqe, nullIf.FirstExpression, outputColumnName, out SourceColumn outputColumnF))
                {
                    functionOutputType = outputColumnF;
                }

                if (TryResolveScalarExpression(ctx, sourceFactory, mqe, nullIf.SecondExpression, outputColumnName, out SourceColumn outputColumnS))
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
                DbType? varDbType = ctx.BatchResolver.TryGetScalarVariableType(varRef.Name);
                if (varDbType.HasValue)
                {
                    string outputColumnNameSafe = outputColumnName ??
                        varRef.Name.Substring(1);
                    //sourceFactory.NewNameForColumnLiteral(mqe, binLit);
                    var source = sourceFactory.NewVariableSource(mqe, varRef, varDbType.Value);
                    column = new SourceColumn(source, outputColumnNameSafe, varDbType.Value);
                    return true;
                }
                throw new NotImplementedException(varRef.WhatIsThis());
            }
            else if (scalarExpr is IIfCall iif)
            {
                SourceColumn functionOutputType = null;
                if (TryResolveScalarExpression(ctx, sourceFactory, mqe, iif.ThenExpression, outputColumnName, out SourceColumn outputColumnF))
                {
                    functionOutputType = outputColumnF;
                }

                if (TryResolveScalarExpression(ctx, sourceFactory, mqe, iif.ElseExpression, outputColumnName, out SourceColumn outputColumnS))
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
                return TryResolveSimpleCaseExpression(ctx, sourceFactory, mqe, outputColumnName, simpleCaseExpr, out column);
            }
            else if (scalarExpr is CoalesceExpression coalesceExpr)
            {
                return TryResolveCoalesceExpression(ctx, sourceFactory, mqe, outputColumnName, coalesceExpr, out column);
            }
            else if (scalarExpr is ConvertCall convertExpr)
            {
                return TryResolveConvertCall(ctx, sourceFactory, mqe, outputColumnName, convertExpr, out column);
            }
            else if (scalarExpr is BinaryExpression binaryExpr)
            {
                return TryResolveBinaryExpression(ctx, sourceFactory, mqe, outputColumnName, binaryExpr, out column);
            }
            else if (scalarExpr is BinaryLiteral binLit)
            {
                if (binLit.Value.StartsWith("0x") && binLit.Value.Length <= 10)
                {
                    uint value = Convert.ToUInt32(binLit.Value, 16);  //Using ToUInt32 not ToUInt64, as per OP comment

                    string outputColumnNameSafe = outputColumnName ?? sourceFactory.NewNameForColumnLiteral(mqe, binLit);
                    var source = sourceFactory.NewConstantSource(mqe, outputColumnNameSafe, DbType.UInt32);
                    column = new SourceColumn(source, outputColumnNameSafe, DbType.UInt32);
                    return true;
                }
                else
                {
                    throw new NotImplementedException(binLit.AsText());
                }
            }
            else if (scalarExpr is ParenthesisExpression ptsExpr)
            {
                return TryResolveScalarExpression(ctx, sourceFactory, mqe, ptsExpr.Expression, outputColumnName, out column);
            }
            else if (scalarExpr is UnaryExpression unaryExpr)
            {
                return TryResolveScalarExpression(ctx, sourceFactory, mqe, unaryExpr.Expression, outputColumnName, out column);
            }
            else
            {
                throw new NotImplementedException(scalarExpr.WhatIsThis());
            }
        }

        private static bool TryResolveBinaryExpression(LoadingContext ctx, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel mqe, string outputColumnName, BinaryExpression binaryExpr, out SourceColumn column)
        {

            if ((binaryExpr.BinaryExpressionType == BinaryExpressionType.Add) // Add: 0 + 0x0100*MAX(IIF(aoa_g.am & 0x0100 = 0x0100, 1, 0))
             || (binaryExpr.BinaryExpressionType == BinaryExpressionType.Multiply) //Multiply: 0x0100*MAX(IIF(aoa_g.am & 0x0100 = 0x0100, 1, 0))
             || (binaryExpr.BinaryExpressionType == BinaryExpressionType.Subtract) //Subtract: settingid - 100
                )
            {
                SourceColumn result = null;
                if (TryResolveScalarExpression(ctx, sourceFactory, mqe, binaryExpr.FirstExpression, outputColumnName, out SourceColumn colF))
                {
                    result = colF;
                }

                if (TryResolveScalarExpression(ctx, sourceFactory, mqe, binaryExpr.SecondExpression, outputColumnName, out SourceColumn colS))
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

        private static bool TryResolveConvertCall(LoadingContext ctx, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel mqe, string outputColumnName, ConvertCall convertExpr, out SourceColumn column)
        {
            if (TryResolveScalarExpression(ctx, sourceFactory, mqe, convertExpr.Parameter, outputColumnName, out SourceColumn scol))
            {
                var dbType = ProcedureGenerator.ResolveToDbDataType(convertExpr.DataType);
                column = new SourceColumn(scol.Source, scol.SourceColumnName, dbType);
                return true;
            }
            else
            {
                throw new NotImplementedException(convertExpr.WhatIsThis());
            }
        }

        private static bool TryResolveCoalesceExpression(LoadingContext ctx, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel mqe, string outputColumnName, CoalesceExpression coalesceExpr, out SourceColumn column)
        {
            SourceColumn result_column = null;
            foreach (ScalarExpression scalarExpr in coalesceExpr.Expressions)
            {
                if (TryResolveScalarExpression(ctx, sourceFactory, mqe, scalarExpr, outputColumnName, out SourceColumn col))
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

        private static bool TryResolveSimpleCaseExpression(LoadingContext ctx, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel mqe, string outputColumnName, SimpleCaseExpression simpleCase, out SourceColumn column)
        {
            foreach (var whenExpr in simpleCase.WhenClauses)
            {
                if (TryResolveScalarExpression(ctx, sourceFactory, mqe, whenExpr.ThenExpression, outputColumnName, out column))
                {
                    //outputColumn.SetColumnDbType(outputColumnDbType);
                    return true;
                }
            }

            if (TryResolveScalarExpression(ctx, sourceFactory, mqe, simpleCase.ElseExpression, outputColumnName, out column))
            {
                //outputColumn.SetColumnDbType(functionOutputDbType);
                return true;
            }

            throw new NotImplementedException(simpleCase.WhatIsThis());
        }

        private static bool TryResolveSearchedCaseExpression(LoadingContext ctx, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel mqe, string outputColumnName, SearchedCaseExpression searchedCase, out SourceColumn column)
        {
            foreach (var whenExpr in searchedCase.WhenClauses)
            {
                if (TryResolveScalarExpression(ctx, sourceFactory, mqe, whenExpr.ThenExpression, outputColumnName, out column))
                {
                    //outputColumn.SetColumnDbType(outputColumnDbType);
                    return true;
                }
            }

            if (TryResolveScalarExpression(ctx, sourceFactory, mqe, searchedCase.ElseExpression, outputColumnName, out column))
            {
                //outputColumn.SetColumnDbType(functionOutputDbType);
                return true;
            }

            throw new NotImplementedException(searchedCase.WhatIsThis());
        }

        private static bool TryResolveNullLiteral(LoadingContext ctx, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel mqe, string outputColumnName, NullLiteral nullLit, out SourceColumn column)
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

        private static bool TryResolveColumnReferenceExpression(LoadingContext ctx, QuerySpecificationModel mqe, string outputColumnName, ColumnReferenceExpression colRef, out SourceColumn column)
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
                    // source not there?!?!
                    throw new NotImplementedException(colRef.WhatIsThis());
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

        private static bool TryResolveReferencedSourceColumn(LoadingContext ctx, QuerySpecificationModel mqe, QueryColumnSourceBase source, string outputColumnName, string sourceColumnName, out SourceColumn outputColumn)
        {
            if (source.TryResolveSourceColumnType(ctx.BatchResolver, sourceColumnName, out SourceColumnType columnDbType))
            {
                if (columnDbType.ColumnDbType.HasValue)
                {
                    // coool!
                    outputColumn = new SourceColumn(source, sourceColumnName, columnDbType.ColumnDbType.Value);
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

        private static bool TryResolveFunctionCall(LoadingContext ctx, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel mqe, FunctionCall fCall, string outputColumnName, out SourceColumn outputColumn)
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
                    var prm = fCall.Parameters[0];

                    if (TryResolveScalarExpression(ctx, sourceFactory, mqe, prm, outputColumnName, out SourceColumn outputColumn1))
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

                    if (TryResolveScalarExpression(ctx, sourceFactory, mqe, prm, outputColumnName, out SourceColumn outputColumn1))
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
                        outputColumn = new SourceColumn(functionOutputType.Source, functionOutputType.SourceColumnName, DbType.String);
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
                var source = sourceFactory.NewConstantSource(mqe, outputColumnNameSafe, DbType.Int64);
                outputColumn = new SourceColumn(source, outputColumnName, DbType.Int64);
                return true;
            }

            if (string.Equals(functionName, "DENSE_RANK", StringComparison.OrdinalIgnoreCase)
                            )
            {
                string outputColumnNameSafe = outputColumnName ?? sourceFactory.NewNameForColumnInt64(mqe, 0);
                var source = sourceFactory.NewConstantSource(mqe, outputColumnNameSafe, DbType.Int64);
                outputColumn = new SourceColumn(source, outputColumnName, DbType.Int64);
                return true;
            }

            if (string.Equals(functionName, "COUNT", StringComparison.OrdinalIgnoreCase)
                            )
            {
                string outputColumnNameSafe = outputColumnName ?? sourceFactory.NewNameForColumnInt32(mqe, 0);
                var source = sourceFactory.NewConstantSource(mqe, outputColumnNameSafe, DbType.Int32);
                outputColumn = new SourceColumn(source, outputColumnName, DbType.Int32);
                return true;
            }
            throw new NotImplementedException(fCall.WhatIsThis());
        }
    }

}