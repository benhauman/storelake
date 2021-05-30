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


        internal static IQueryModel LoadModel(BatchOutputColumnTypeResolver batchResolver, string queryName, QueryExpression qryExpr, WithCtesAndXmlNamespaces ctes)
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
                    var qrymodel = LoadQuerySpecificationModel(batchResolver, sourceFactory, qspec, name, ctes);
                    union.AddUnionQuery(qrymodel);
                }

                // in-union column resolving
                UnionResolve(batchResolver, union);

                return union;
            }
            else
            {
                return LoadQuerySpecificationModel(batchResolver, sourceFactory, (QuerySpecification)qryExpr, queryName, ctes);
            }
        }

        private static QuerySpecificationModel LoadQuerySpecificationModel(BatchOutputColumnTypeResolver batchResolver, QueryColumnSourceFactory sourceFactory, QuerySpecification qrySpec, string queryName, WithCtesAndXmlNamespaces ctes)
        {
            QuerySpecificationModel qmodel = sourceFactory.NewRootSpecification(qrySpec, queryName);

            // sources
            CollectQuerySpecificationSources(batchResolver, sourceFactory, qmodel, ctes);

            // in-select column resolving
            ResolveSelectedElements(batchResolver, sourceFactory, qmodel);

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

        private static void UnionResolve(BatchOutputColumnTypeResolver batchResolver, QueryUnionModel unionModel)
        {
            foreach (QuerySpecificationModel mqeN in unionModel.union_queries)
            {
                if (mqeN.IsQueryOutputResolved && (mqeN.HasResolvedOutputColumnWithoutType() == null))
                {
                    // cte?
                }
                else
                {
                    if (TryResolveUnionNullColumns(batchResolver, unionModel, mqeN))
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

        private static void CollectQuerySpecificationSources(BatchOutputColumnTypeResolver batchResolver, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel source, WithCtesAndXmlNamespaces ctes)
        {
            //QueryColumnSourceMQE source = new QueryColumnSourceMQE(false, key, "qspec");
            foreach (TableReference tableRef in source.QrySpec.FromClause.TableReferences)
            {
                CollectTableRef(batchResolver, sourceFactory, source, tableRef, ctes, (ts) =>
                {
                    source.AddMqeColumnSource(ts);
                });
            }
        }

        private static void CollectTableRef(BatchOutputColumnTypeResolver batchResolver, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel parent, TableReference tableRef, WithCtesAndXmlNamespaces ctes, Action<QueryColumnSourceBase> collector)
        {
            if (tableRef is QualifiedJoin qJoin)
            {
                CollectTableRef(batchResolver, sourceFactory, parent, qJoin.FirstTableReference, ctes, collector);
                CollectTableRef(batchResolver, sourceFactory, parent, qJoin.SecondTableReference, ctes, collector);
            }
            else if (tableRef is UnqualifiedJoin uqJoin)
            {
                CollectTableRef(batchResolver, sourceFactory, parent, uqJoin.FirstTableReference, ctes, collector);
                CollectTableRef(batchResolver, sourceFactory, parent, uqJoin.SecondTableReference, ctes, collector);
            }
            else if (tableRef is NamedTableReference ntRef)
            {
                CollectNamedTableReference(batchResolver, sourceFactory, parent, ntRef, ctes, collector);
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
                CollectInlineDerivedTable(batchResolver, sourceFactory, parent, derivedTable, ctes, collector);
            }
            else
            {
                throw new NotImplementedException(tableRef.WhatIsThis());
            }
        }

        private static void CollectInlineDerivedTable(BatchOutputColumnTypeResolver batchResolver, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel parent, InlineDerivedTable derivedTable, WithCtesAndXmlNamespaces ctes, Action<QueryColumnSourceBase> collector)
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
                        if (TryResolveScalarExpression(batchResolver, sourceFactory, parent, rowValue, valuesColumnName, out SourceColumn column))
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

        private static void CollectNamedTableReference(BatchOutputColumnTypeResolver batchResolver, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel parent, NamedTableReference node, WithCtesAndXmlNamespaces ctes, Action<QueryColumnSourceBase> collector)
        {
            if (node.SchemaObject.SchemaIdentifier == null &&
                ctes != null &&
                TryGetCTEByName(ctes, node.SchemaObject.BaseIdentifier.Dequote(), out CommonTableExpression cte))
            {
                string cteName = cte.ExpressionName.Dequote();
                IQueryModel cte_qmodel = QueryModelLoader.LoadModel(batchResolver, cteName, cte.QueryExpression, ctes);

                string key = node.Alias != null ? node.Alias.Dequote() : node.SchemaObject.BaseIdentifier.Dequote();
                var cte_source = sourceFactory.NewSourceOnCte(parent, key, cte_qmodel);
                collector(cte_source);
                //QueryColumnSourceBase cte_source = new QueryColumnSourceCTE(cte, node.Alias);
                //QueryColumnSourceBase cte_source = null;

                /*var qspecs = new List<QuerySpecification>();
                CollectQuerySpecifications(qspecs, cte.QueryExpression);

                if (qspecs.Count == 0)
                {
                    throw new NotImplementedException(node.WhatIsThis());
                }

                QuerySpecificationModel first_query = null;
                for (int ix = 0; ix < qspecs.Count; ix++)
                {
                    QuerySpecification qspec = qspecs[ix];
                    if (ix == 0)
                    {
                        string key = node.Alias != null ? node.Alias.Dequote() : node.SchemaObject.BaseIdentifier.Dequote();
                        QuerySpecificationModel cte_query = sourceFactory.NewQueryColumnSourceCTE(parent, qspec, key);
                        first_query = cte_query;
                        CollectQuerySpecification(sourceFactory, cte_query, ctes);
                    }
                    else
                    {
                        // UNION query
                        QuerySpecificationModel cte_query = sourceFactory.NewQueryColumnSourceCTE(parent, qspec, "q" + (ix + 1));
                        CollectQuerySpecification(sourceFactory, cte_query, ctes);
                        first_query.AddUnionQuery(cte_query);
                    }
                }

                collector(first_query);
                */
            }
            else
            {
                collector(sourceFactory.NewQueryColumnSourceNT(parent, node));
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

        private static void ResolveSelectedElements(BatchOutputColumnTypeResolver batchResolver, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel mqe)
        {
            foreach (SelectElement node in mqe.QrySpec.SelectElements)
            {
                if (StatementOutputColumnTypeResolverV2.TryGetOutputColumnName(node, out string outputColumnName))
                {
                }

                if (TryResolveSelectedElement(batchResolver, sourceFactory, mqe, node, outputColumnName, out SourceColumn col))
                {
                    string outputColumnNameSafe = outputColumnName ?? col.SourceColumnName;
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
                    throw new NotImplementedException(node.WhatIsThis());
                }
            }
        }


        private static bool TryResolveUnionNullColumns(BatchOutputColumnTypeResolver batchResolver, QueryUnionModel unionModel, QuerySpecificationModel mqe)
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
                        if (qry.TryGetQueryOutputColumn(batchResolver, col.OutputColumnName, out QueryColumnBase outputColumnX))
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
            //foreach (SelectElement node in mqe.QrySpec.SelectElements)
            {
                //QueryColumnBase outputColumn = null;
                //if (TryResolveSelectedElement(batchResolver, mqe, node, ref outputColumn))
                //{
                //    if (!outputColumn.ColumnDbType.HasValue)
                //    {
                //        throw new NotSupportedException("Output column type not resolved.");
                //    }
                //}
                //else
                //{
                //    throw new NotSupportedException("Output column type not resolved.");
                //}
            }

            if (mqe.QrySpec.SelectElements.Count != mqe.ResolvedOutputColumnsCount)
            {
                foreach (SelectElement node in mqe.QrySpec.SelectElements)
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


        private static bool TryResolveSelectedElement(BatchOutputColumnTypeResolver batchResolver, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel mqe, SelectElement node, string outputColumnName, out SourceColumn column)
        {
            if (node is SelectScalarExpression scalarExpr)
            {
                if (TryResolveScalarExpression(batchResolver, sourceFactory, mqe, scalarExpr.Expression, outputColumnName, out column))
                {
                    return true;
                }
                else
                {
                    return false;// (NullLiteral?]) maybe an UNION query after this one can resolve it 
                }
            }
            else
            {
                throw new NotImplementedException(node.WhatIsThis());
            }
        }

        //???private static bool TryResolveSelectScalarExpression(BatchOutputColumnTypeResolver batchResolver, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel mqe, SelectScalarExpression scalarExpr, string outputColumnName, out QueryColumnBase outputColumn)
        //???{
        //???    if (scalarExpr.Expression is ColumnReferenceExpression colRef)
        //???    {
        //???        if (TryResolveSelectedColumnReferenceExpression(batchResolver, mqe, scalarExpr, colRef, out QueryColumnBase col))
        //???        {
        //???            column = new SourceColumn(col.Source, col.SourceColumnName, col.ColumnDbType.Value);
        //???        }
        //???        else
        //???        {
        //???            throw new NotImplementedException(scalarExpr.Expression.WhatIsThis());
        //???        }
        //???    }
        //???    else if (scalarExpr.Expression is NullLiteral nullLit)
        //???    {
        //???        return TryResolveSelectedColumnNullLiteral(batchResolver, mqe, scalarExpr, outputColumnName, nullLit, out column);
        //???    }
        //???    else if (scalarExpr.Expression is FunctionCall fCall)
        //???    {
        //???        return TryResolveSelectedColumnFunctionCall(batchResolver, mqe, scalarExpr, fCall, out outputColumn);
        //???    }
        //???    else if (scalarExpr.Expression is IntegerLiteral intLit)
        //???    {
        //???        var source = sourceFactory.NewConstantSource(mqe, outputColumnName, DbType.Int32);
        //???        column = mqe.AddOutputColumn(new QueryColumnE(source, outputColumnName, null, DbType.Int32));
        //???        return true;
        //???    }
        //???    else if (scalarExpr.Expression is CastCall castExpr)
        //???    {
        //???        if (TryResolveScalarExpression(batchResolver, sourceFactory, mqe, castExpr.Parameter, out column))
        //???        {
        //???
        //???        }
        //???        outputColumn.SetColumnDbType(ProcedureGenerator.ResolveToDbDataType(castExpr.DataType));
        //???        return true;
        //???    }
        //???    else if (scalarExpr.Expression is SearchedCaseExpression searchedCase)
        //???    {
        //???        return TryResolveSelectedColumnSearchedCaseExpression(batchResolver, mqe, scalarExpr, outputColumnName, searchedCase, ref outputColumn);
        //???    }
        //???    else
        //???    {
        //???        throw new NotImplementedException(scalarExpr.Expression.WhatIsThis());
        //???    }
        //???}

        private static bool TryResolveScalarExpression(BatchOutputColumnTypeResolver batchResolver, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel mqe, ScalarExpression scalarExpr, string outputColumnName, out SourceColumn column)
        {
            if (scalarExpr is ColumnReferenceExpression colRef)
            {
                if (TryResolveColumnReferenceExpression(batchResolver, mqe, outputColumnName, colRef, out SourceColumn scol))
                {
                    //column = new SourceColumn(col.Source, col.SourceColumnName, col.ColumnDbType.Value);
                    column = scol;
                    return true;
                }
                else
                {
                    throw new NotImplementedException(scalarExpr.WhatIsThis());
                }
            }
            else if (scalarExpr is NullLiteral nullLit)
            {
                return TryResolveNullLiteral(batchResolver, sourceFactory, mqe, outputColumnName, nullLit, out column);
            }
            else if (scalarExpr is FunctionCall fCall)
            {
                return TryResolveFunctionCall(batchResolver, sourceFactory, mqe, fCall, outputColumnName, out column);
            }
            else if (scalarExpr is IntegerLiteral intLit)
            {
                var source = sourceFactory.NewConstantSource(mqe, outputColumnName, DbType.Int32);
                //column = mqe.AddOutputColumn(new QueryColumnE(source, outputColumnName, null, DbType.Int32));
                column = new SourceColumn(source, outputColumnName, DbType.Int32);
                return true;
            }
            else if (scalarExpr is StringLiteral strLit)
            {
                var source = sourceFactory.NewConstantSource(mqe, outputColumnName, DbType.String);
                column = new SourceColumn(source, outputColumnName, DbType.String);
                return true;
            }
            else if (scalarExpr is CastCall castExpr)
            {
                if (TryResolveScalarExpression(batchResolver, sourceFactory, mqe, castExpr.Parameter, outputColumnName, out SourceColumn scol))
                {
                    //outputColumn.SetColumnDbType(ProcedureGenerator.ResolveToDbDataType(castExpr.DataType));
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
                return TryResolveSearchedCaseExpression(batchResolver, sourceFactory, mqe, outputColumnName, searchedCase, out column);
            }
            else if (scalarExpr is NullIfExpression nullIf)
            {
                SourceColumn functionOutputType = null;
                if (TryResolveScalarExpression(batchResolver, sourceFactory, mqe, nullIf.FirstExpression, outputColumnName, out SourceColumn outputColumnF))
                {
                    functionOutputType = outputColumnF;
                }

                if (TryResolveScalarExpression(batchResolver, sourceFactory, mqe, nullIf.SecondExpression, outputColumnName, out SourceColumn outputColumnS))
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
                DbType? varDbType = batchResolver.TryGetScalarVariableType(varRef.Name);
                if (varDbType.HasValue)
                {
                    var source = sourceFactory.NewVariableSource(mqe, varRef, varDbType.Value);
                    column = new SourceColumn(source, outputColumnName, varDbType.Value);
                    return true;
                }
                throw new NotImplementedException(varRef.WhatIsThis());
            }
            else
            {
                throw new NotImplementedException(scalarExpr.WhatIsThis());
            }
        }

        private static bool TryResolveSearchedCaseExpression(BatchOutputColumnTypeResolver batchResolver, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel mqe, string outputColumnName, SearchedCaseExpression searchedCase, out SourceColumn column)
        {
            foreach (var whenExpr in searchedCase.WhenClauses)
            {
                if (TryResolveScalarExpression(batchResolver, sourceFactory, mqe, whenExpr.ThenExpression, outputColumnName, out column))
                {
                    //outputColumn.SetColumnDbType(outputColumnDbType);
                    return true;
                }
            }

            if (TryResolveScalarExpression(batchResolver, sourceFactory, mqe, searchedCase.ElseExpression, outputColumnName, out column))
            {
                //outputColumn.SetColumnDbType(functionOutputDbType);
                return true;
            }

            throw new NotImplementedException(searchedCase.WhatIsThis());
        }

        private static bool TryResolveNullLiteral(BatchOutputColumnTypeResolver batchResolver, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel mqe, string outputColumnName, NullLiteral nullLit, out SourceColumn column)
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
            //else
            //    {
            //        if (!string.IsNullOrEmpty(outputColumnName))
            //        {
            //            foreach (var source in mqe.sources.Values)
            //            {
            //                if (source is QuerySourceOnQuery sub_mqe)
            //                {
            //                    // try all unioin_queries to find the outcolumn if possible
            //                    if (!string.IsNullOrEmpty(outputColumnName))
            //                    {
            //                        //?if (sub_mqe.TryGetResolvedOutputColumn(outputColumnName, out QueryColumnBase cm))
            //                        //?{
            //                        //?    outputColumn = cm;
            //                        //?    return true;
            //                        //?
            //                        //?}
            //                    }
            //                }
            //                else
            //                {
            //                }
            //            }
            //        }
            //
            //        // null is null
            //        column = null;
            //        return false;
            //    }
        }

        private static bool TryResolveColumnReferenceExpression(BatchOutputColumnTypeResolver batchResolver, QuerySpecificationModel mqe, string outputColumnName, ColumnReferenceExpression colRef, out SourceColumn column)
        {
            if (colRef.MultiPartIdentifier.Count == 2)
            {
                string sourceNameOrAlias = colRef.MultiPartIdentifier[0].Dequote();
                string sourceColumnName = colRef.MultiPartIdentifier[1].Dequote();

                if (mqe.TryFindSource(sourceNameOrAlias, out QueryColumnSourceBase source))
                {
                    if (TryResolveReferencedSourceColumn(batchResolver, mqe, source, outputColumnName, sourceColumnName, out column))
                    {
                        return true;
                    }
                    else
                    {
                        // source column not there?!?!
                        throw new NotImplementedException(colRef.WhatIsThis());
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
                    if (TryResolveReferencedSourceColumn(batchResolver, mqe, source, outputColumnName, sourceColumnName, out column))
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
                        if (TryResolveReferencedSourceColumn(batchResolver, mqe, source, outputColumnName, sourceColumnName, out column))
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

        private static bool TryResolveReferencedSourceColumn(BatchOutputColumnTypeResolver batchResolver, QuerySpecificationModel mqe, QueryColumnSourceBase source, string outputColumnName, string sourceColumnName, out SourceColumn outputColumn)
        {
            if (source is QuerySourceOnQuery sub_mqe)
            {
                //string outputColumnNameSafe = outputColumnName ?? sourceColumnName;
                //?if (sub_mqe.TryGetResolvedOutputColumn(outputColumnNameSafe, out outputColumn))
                //?{
                //?    return true;
                //?}
                //??? TryGetOutputColumnByName
                //    //??? TryResolveSelectedElement(batchResolver, sub_mqe, );
                //    return false;
            }
            //else
            {
                //var col = source.TryResolveSelectedColumn(batchResolver, outputColumnName, sourceColumnName);
                if (source.TryResolveSourceColumnType(batchResolver, sourceColumnName, out DbType columnDbType))
                //if (col != null)
                {
                    // coool!
                    //string outputColumnNameSafe = outputColumnName ?? sourceColumnName;
                    //outputColumn = mqe.AddOutputColumn(new QueryColumnE(source, outputColumnNameSafe, sourceColumnName, columnDbType));
                    outputColumn = new SourceColumn(source, sourceColumnName, columnDbType);
                    return true;
                }
                else
                {
                    // column not there?!?! wrong source
                    //throw new NotImplementedException(colRef.WhatIsThis());
                    outputColumn = null;
                    return false;
                }
            }

        }

        private static bool TryResolveFunctionCall(BatchOutputColumnTypeResolver batchResolver, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel mqe, FunctionCall fCall, string outputColumnName, out SourceColumn outputColumn)
        {
            string functionName = fCall.FunctionName.Dequote();
            if (string.Equals(functionName, "ISNULL", StringComparison.OrdinalIgnoreCase))
            {
                var prm1 = fCall.Parameters[0];
                var prm2 = fCall.Parameters[1];

                SourceColumn functionOutputType = null;
                if (TryResolveScalarExpression(batchResolver, sourceFactory, mqe, prm1, outputColumnName, out SourceColumn outputColumn1))
                {
                    functionOutputType = outputColumn1;
                }
                if (TryResolveScalarExpression(batchResolver, sourceFactory, mqe, prm2, outputColumnName, out SourceColumn outputColumn2))
                {
                    functionOutputType = outputColumn2;
                }

                if (functionOutputType != null)
                {
                    outputColumn = functionOutputType;
                    return true;
                }

                throw new NotImplementedException(prm1.WhatIsThis());
                throw new NotImplementedException(prm2.WhatIsThis());
            }
            throw new NotImplementedException(fCall.WhatIsThis());
        }

        private sealed class SourceColumn
        {
            internal readonly QueryColumnSourceBase Source;
            internal readonly DbType? ColumnDbType;
            internal readonly string SourceColumnName;
            public SourceColumn(QueryColumnSourceBase source, string sourceColumnName, DbType columnDbType)
            {
                Source = source;
                SourceColumnName = sourceColumnName;
                ColumnDbType = columnDbType;
            }
            public SourceColumn(QueryColumnSourceBase source, string sourceColumnName)
            {
                Source = source;
                SourceColumnName = sourceColumnName;
                ColumnDbType = null;
            }

        }
        private static bool TryResolveExpressionColumnReference(BatchOutputColumnTypeResolver batchResolver, QuerySpecificationModel mqe, ColumnReferenceExpression colRef, out SourceColumn column)
        {
            if (colRef.MultiPartIdentifier.Count == 2)
            {
                string sourceNameOrAlias = colRef.MultiPartIdentifier[0].Dequote();
                string sourceColumnName = colRef.MultiPartIdentifier[1].Dequote();

                if (mqe.TryFindSource(sourceNameOrAlias, out QueryColumnSourceBase source))
                {
                    //if (source is QueryColumnSourceMQE sub_mqe)
                    //{
                    //    //??? TryResolveSelectedElement(batchResolver, sub_mqe, );
                    //    //return false;
                    //}
                    //else
                    {
                        if (source.TryResolveSourceColumnType(batchResolver, sourceColumnName, out DbType columnType))
                        {
                            // coool!
                            column = new SourceColumn(source, sourceColumnName, columnType);
                            return true;
                        }
                        else
                        {
                            // column not there?!?! wrong source
                            throw new NotImplementedException(colRef.WhatIsThis());
                        }
                    }
                }
                else
                {
                    // source not there?!?!
                    throw new NotImplementedException(colRef.WhatIsThis());
                }
                //return model.Root.TryFindColumnSC(this.batchResolver, sourceNameOrAlias, columnName, cm);
            }
            else if (colRef.MultiPartIdentifier.Count == 1)
            {
                // no source only column name => traverse all source and find t
                string sourceColumnName = colRef.MultiPartIdentifier[0].Dequote();
                //source.TryResolveSelectedColumn(columnName);
                if (mqe.SourceCount == 1)
                {
                    var source = mqe.SourceSingle;
                    //if (source is QueryColumnSourceMQE sub_mqe)
                    //{
                    //    //???? ResolveSelectedElements(batchResolver, sub_mqe);
                    //    return false;
                    //}
                    //else
                    {
                        if (source.TryResolveSourceColumnType(batchResolver, sourceColumnName, out DbType columnType))
                        {
                            // coool!
                            column = new SourceColumn(source, sourceColumnName, columnType);
                            return true;
                        }
                        else
                        {
                            // column not there?!?! wrong source
                            throw new NotImplementedException(colRef.WhatIsThis());
                        }
                    }
                }
                else
                {
                    throw new NotImplementedException(colRef.WhatIsThis());
                }
            }
            else
            {
                throw new NotImplementedException(colRef.WhatIsThis());
            }
        }
    }

}