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


        internal static IQueryModel LoadModificationOutputModel(BatchOutputColumnTypeResolver batchResolver, string queryName, DataModificationSpecification spec, WithCtesAndXmlNamespaces ctes)
        {
            QueryColumnSourceFactory sourceFactory = new QueryColumnSourceFactory();
            return LoadQueryOnModificationOutputModel(batchResolver, sourceFactory, queryName, spec, ctes);
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

        private static IQueryModel LoadQueryOnModificationOutputModel(BatchOutputColumnTypeResolver batchResolver, QueryColumnSourceFactory sourceFactory, string queryName, DataModificationSpecification mspec, WithCtesAndXmlNamespaces ctes)
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
                        if (source.TryResolveSourceColumnType(batchResolver, sourceColumnName, out DbType columnDbType))
                        {
                            qmodel.AddOutputColumn(new QueryColumnE(source, sourceColumnName, sourceColumnName, columnDbType));
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
                            if (source.TryResolveSourceColumnType(batchResolver, sourceColumnName, out DbType columnDbType))
                            {
                                qmodel.AddOutputColumn(new QueryColumnE(source, sourceColumnName, sourceColumnName, columnDbType));
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
            if (source.QrySpec.FromClause == null)
            {
                // SELECT without FROM
            }
            else
            {
                foreach (TableReference tableRef in source.QrySpec.FromClause.TableReferences)
                {
                    CollectTableRef(batchResolver, sourceFactory, source, tableRef, ctes, (ts) =>
                    {
                        source.AddMqeColumnSource(ts);
                    });
                }
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
                // VALUES
                CollectInlineDerivedTable(batchResolver, sourceFactory, parent, derivedTable, ctes, collector);
            }
            else if (tableRef is QueryDerivedTable qDerivedTable)
            {
                // SubQuery
                CollectQueryDerivedTable(batchResolver, sourceFactory, parent, qDerivedTable, ctes, collector);
            }
            else
            {
                throw new NotImplementedException(tableRef.WhatIsThis());
            }
        }

        private static void CollectQueryDerivedTable(BatchOutputColumnTypeResolver batchResolver, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel parent, QueryDerivedTable derivedTable, WithCtesAndXmlNamespaces ctes, Action<QueryColumnSourceBase> collector)
        {
            //NewSourceOnCte
            string cteName = "subquery";
            IQueryModel qmodel = QueryModelLoader.LoadModel(batchResolver, cteName, derivedTable.QueryExpression, ctes);

            string key = derivedTable.Alias.Dequote();
            var source = sourceFactory.NewSourceOnQueryDerivedTable(parent, key, qmodel);
            collector(source);
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
            foreach (SelectElement se in mqe.QrySpec.SelectElements)
            {
                if (se is SelectScalarExpression sscalarExpr)
                {

                    if (StatementOutputColumnTypeResolverV2.TryGetOutputColumnName(sscalarExpr, out string outputColumnName))
                    {
                    }

                    if (TryResolveScalarExpression(batchResolver, sourceFactory, mqe, sscalarExpr.Expression, outputColumnName, out SourceColumn col))
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
                        throw new NotImplementedException(se.WhatIsThis());
                    }
                }
                else
                {
                    throw new NotImplementedException(se.WhatIsThis());
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
                string outputColumnNameSafe = outputColumnName ?? sourceFactory.NewNameForColumn(mqe, intLit);
                var source = sourceFactory.NewConstantSource(mqe, outputColumnNameSafe, DbType.Int32);
                column = new SourceColumn(source, outputColumnNameSafe, DbType.Int32);
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
            else if (scalarExpr is IIfCall iif)
            {
                SourceColumn functionOutputType = null;
                if (TryResolveScalarExpression(batchResolver, sourceFactory, mqe, iif.ThenExpression, outputColumnName, out SourceColumn outputColumnF))
                {
                    functionOutputType = outputColumnF;
                }

                if (TryResolveScalarExpression(batchResolver, sourceFactory, mqe, iif.ElseExpression, outputColumnName, out SourceColumn outputColumnS))
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
                return TryResolveSimpleCaseExpression(batchResolver, sourceFactory, mqe, outputColumnName, simpleCaseExpr, out column);
            }
            else if (scalarExpr is CoalesceExpression coalesceExpr)
            {
                return TryResolveCoalesceExpression(batchResolver, sourceFactory, mqe, outputColumnName, coalesceExpr, out column);
            }
            else if (scalarExpr is ConvertCall convertExpr)
            {
                return TryResolveConvertCall(batchResolver, sourceFactory, mqe, outputColumnName, convertExpr, out column);
            }
            else if (scalarExpr is BinaryExpression binaryExpr)
            {
                return TryResolveBinaryExpression(batchResolver, sourceFactory, mqe, outputColumnName, binaryExpr, out column);
            }
            else if (scalarExpr is BinaryLiteral binLit)
            {
                if (binLit.Value.StartsWith("0x") && binLit.Value.Length <= 10)
                {
                    uint value = Convert.ToUInt32(binLit.Value, 16);  //Using ToUInt32 not ToUInt64, as per OP comment

                    string outputColumnNameSafe = outputColumnName ?? sourceFactory.NewNameForColumn(mqe, binLit);
                    var source = sourceFactory.NewConstantSource(mqe, outputColumnNameSafe, DbType.UInt32);
                    column = new SourceColumn(source, outputColumnNameSafe, DbType.UInt32);
                    return true;
                }
                else
                {
                    throw new NotImplementedException(binLit.AsText());
                }
            }
            else
            {
                throw new NotImplementedException(scalarExpr.WhatIsThis());
            }
        }

        private static bool TryResolveBinaryExpression(BatchOutputColumnTypeResolver batchResolver, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel mqe, string outputColumnName, BinaryExpression binaryExpr, out SourceColumn column)
        {

            if ((binaryExpr.BinaryExpressionType == BinaryExpressionType.Add) // Add: 0 + 0x0100*MAX(IIF(aoa_g.am & 0x0100 = 0x0100, 1, 0))
             || (binaryExpr.BinaryExpressionType == BinaryExpressionType.Multiply) //Multiply: 0x0100*MAX(IIF(aoa_g.am & 0x0100 = 0x0100, 1, 0))
                )
            {
                SourceColumn result = null;
                if (TryResolveScalarExpression(batchResolver, sourceFactory, mqe, binaryExpr.FirstExpression, outputColumnName, out SourceColumn colF))
                {
                    result = colF;
                }

                if (TryResolveScalarExpression(batchResolver, sourceFactory, mqe, binaryExpr.SecondExpression, outputColumnName, out SourceColumn colS))
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

        private static bool TryResolveConvertCall(BatchOutputColumnTypeResolver batchResolver, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel mqe, string outputColumnName, ConvertCall convertExpr, out SourceColumn column)
        {
            if (TryResolveScalarExpression(batchResolver, sourceFactory, mqe, convertExpr.Parameter, outputColumnName, out SourceColumn scol))
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

        private static bool TryResolveCoalesceExpression(BatchOutputColumnTypeResolver batchResolver, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel mqe, string outputColumnName, CoalesceExpression coalesceExpr, out SourceColumn column)
        {
            SourceColumn result_column = null;
            foreach (ScalarExpression scalarExpr in coalesceExpr.Expressions)
            {
                if (TryResolveScalarExpression(batchResolver, sourceFactory, mqe, scalarExpr, outputColumnName, out SourceColumn col))
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

        private static bool TryResolveSimpleCaseExpression(BatchOutputColumnTypeResolver batchResolver, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel mqe, string outputColumnName, SimpleCaseExpression simpleCase, out SourceColumn column)
        {
            foreach (var whenExpr in simpleCase.WhenClauses)
            {
                if (TryResolveScalarExpression(batchResolver, sourceFactory, mqe, whenExpr.ThenExpression, outputColumnName, out column))
                {
                    //outputColumn.SetColumnDbType(outputColumnDbType);
                    return true;
                }
            }

            if (TryResolveScalarExpression(batchResolver, sourceFactory, mqe, simpleCase.ElseExpression, outputColumnName, out column))
            {
                //outputColumn.SetColumnDbType(functionOutputDbType);
                return true;
            }

            throw new NotImplementedException(simpleCase.WhatIsThis());
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
            if (source.TryResolveSourceColumnType(batchResolver, sourceColumnName, out DbType columnDbType))
            {
                // coool!
                outputColumn = new SourceColumn(source, sourceColumnName, columnDbType);
                return true;
            }
            else
            {
                // column not there?!?! wrong source
                outputColumn = null;
                return false;
            }
        }

        private static bool TryResolveFunctionCall(BatchOutputColumnTypeResolver batchResolver, IQueryColumnSourceFactory sourceFactory, QuerySpecificationModel mqe, FunctionCall fCall, string outputColumnName, out SourceColumn outputColumn)
        {
            string functionName = fCall.FunctionName.Dequote();
            if (string.Equals(functionName, "ISNULL", StringComparison.OrdinalIgnoreCase)
             || string.Equals(functionName, "MAX", StringComparison.OrdinalIgnoreCase)
                )
            {
                SourceColumn functionOutputType = null;
                for (int ix = 0; ix < fCall.Parameters.Count; ix++)
                {
                    var prm = fCall.Parameters[0];

                    if (TryResolveScalarExpression(batchResolver, sourceFactory, mqe, prm, outputColumnName, out SourceColumn outputColumn1))
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
            throw new NotImplementedException(fCall.WhatIsThis());
        }

        private sealed class SourceColumn
        {
            internal readonly QueryColumnSourceBase Source;
            internal readonly DbType? ColumnDbType;
            internal readonly string SourceColumnName;
            public SourceColumn(QueryColumnSourceBase source, string sourceColumnName, DbType columnDbType)
            {
                if (source == null)
                    throw new ArgumentNullException(nameof(source));
                if (string.IsNullOrEmpty(sourceColumnName))
                    throw new ArgumentNullException(nameof(sourceColumnName));
                Source = source;
                SourceColumnName = sourceColumnName;
                ColumnDbType = columnDbType;
            }
            public SourceColumn(QueryColumnSourceBase source, string sourceColumnName)
            {
                if (source == null)
                    throw new ArgumentNullException(nameof(source));
                if (string.IsNullOrEmpty(sourceColumnName))
                    throw new ArgumentNullException(nameof(sourceColumnName));

                Source = source;
                SourceColumnName = sourceColumnName;
                ColumnDbType = null;
            }

        }
    }

}