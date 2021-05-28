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
    internal sealed class StatementModel
    {
        // root: ColumnSource query = 
        // ColumnSource types(table-view, subquery, udtf:tablefunctions, tablevariablaes, parameters) (Alias, Named, ?)
        internal QueryColumnSourceBase Root;
    }

    internal class StatementOutputColumnTypeResolverV2
    {
        internal readonly ISchemaMetadataProvider SchemaMetadata;
        BatchOutputColumnTypeResolver batchResolver;
        StatementWithCtesAndXmlNamespaces statement;
        private StatementModel _model;
        public StatementOutputColumnTypeResolverV2(BatchOutputColumnTypeResolver batchResolver, StatementWithCtesAndXmlNamespaces statement)
        {
            SchemaMetadata = batchResolver.SchemaMetadata;
            this.batchResolver = batchResolver;
            this.statement = statement;
        }

        private static OutputColumnDescriptor ColumnModelToDescriptor(ColumnModel column)
        {
            return (column.IsOk)
                ? new OutputColumnDescriptor(column.ColumnName, column.ColumnDbType.Value)
                : null;
        }

        internal OutputColumnDescriptor ResolveColumnReference(ColumnReferenceExpression node)
        {
            StatementModel model = EnsureModel();

            ColumnModel cm = new ColumnModel();
            if (TryColumnReference(model, cm, node))
                return ColumnModelToDescriptor(cm);

            throw new NotImplementedException(node.WhatIsThis());
        }


        internal OutputColumnDescriptor ResolveScalarExpression(SelectScalarExpression node)
        {
            StatementModel model = EnsureModel();

            if (node.Expression is ColumnReferenceExpression colRef)
            {
                ColumnModel cm = new ColumnModel();
                if (TryGetOutputColumnName(node, out string columnName))
                {
                    cm.ColumnName = columnName;
                }
                if (TryColumnReference(model, cm, colRef))
                {
                    return ColumnModelToDescriptor(cm);
                }
            }

            throw new NotImplementedException(node.Expression.WhatIsThis());
        }

        internal static bool TryGetOutputColumnName(SelectScalarExpression node, out string columnName)
        {
            if (node.ColumnName != null)
            {
                if (node.ColumnName.ValueExpression != null)
                {
                    throw new NotImplementedException(node.ColumnName.ValueExpression.WhatIsThis());
                }
                else if (node.ColumnName.Identifier != null)
                {
                    columnName = node.ColumnName.Identifier.Dequote();
                    return true;
                }
                else
                {
                    if (!string.IsNullOrEmpty(node.ColumnName.Value))
                    {
                        columnName = node.ColumnName.Value;
                        return true;
                    }

                    columnName = null;
                    return false;
                }
            }
            else
            {
                columnName = null;
                return false;
            }

        }

        private bool TryColumnReference(StatementModel model, ColumnModel cm, ColumnReferenceExpression node)
        {
            if (node.ColumnType != ColumnType.Regular)
                throw new NotImplementedException(node.AsText());

            if (node.MultiPartIdentifier.Count == 2)
            {
                // source (table/view) name without schema or alias
                string sourceNameOrAlias = node.MultiPartIdentifier[0].Dequote();
                string columnName = node.MultiPartIdentifier[1].Dequote();

                return model.Root.TryFindColumnSC(this.batchResolver, sourceNameOrAlias, columnName, cm);
            }
            else if (node.MultiPartIdentifier.Count == 1)
            {
                // no source only column name => traverse all source and find t
                string columnName = node.MultiPartIdentifier[0].Dequote();
                return model.Root.TryFindColumnC(model, columnName, cm);
            }
            else
            {
                // 3 or 4
                throw new NotImplementedException(node.AsText() + "   ## " + statement.WhatIsThis());
            }
        }


        private StatementModel EnsureModel()
        {
            if (_model == null)
            {
                if (statement is SelectStatement stmt_sel)
                {
                    _model = QueryModelLoader.LoadModel(batchResolver, stmt_sel.QueryExpression, stmt_sel.WithCtesAndXmlNamespaces);
                }
                else
                {
                    DataModificationSpecification source_specification = null;
                    DataModificationStatement stmt_mod = (DataModificationStatement)statement;
                    if (statement is InsertStatement stmt_ins)
                    {
                        source_specification = stmt_ins.InsertSpecification;
                    }
                    else if (statement is UpdateStatement stmt_upd)
                    {
                        source_specification = stmt_upd.UpdateSpecification;
                    }
                    else if (statement is DeleteStatement stmt_del)
                    {
                        source_specification = stmt_del.DeleteSpecification;
                    }
                    else if (statement is MergeStatement stmt_mrg)
                    {
                        source_specification = stmt_mrg.MergeSpecification;
                    }

                    //_model = QueryModelLoader.LoadModel(source_specification.OutputClause, stmt_mod.WithCtesAndXmlNamespaces);
                    throw new NotImplementedException(statement.WhatIsThis());
                }

            }
            return _model;
        }


    }


    internal class QueryOrUnion
    {
        internal readonly List<QueryColumnSourceMQE> queries = new List<QueryColumnSourceMQE>();
        private readonly IDictionary<string, QueryColumnSourceBase> keys = new SortedDictionary<string, QueryColumnSourceBase>();
        internal void AddQuery(QueryColumnSourceMQE source)
        {
            keys.Add(source.Key, source);
            queries.Add(source);
        }
        internal int Count
        {
            get
            {
                return queries.Count;
            }
        }
        internal QueryColumnSourceBase SingleOne
        {
            get
            {
                return queries.Single();
            }
        }
    }


    internal static class QueryModelLoader
    {
        internal static StatementModel LoadModel(BatchOutputColumnTypeResolver batchResolver, QueryExpression qryExpr, WithCtesAndXmlNamespaces ctes)
        {
            StatementModel model = new StatementModel();

            //Func<QueryColumnSourceMQE> sourceCreator = () => new QueryColumnSourceMQE(false, "$$$_root_$$$", "rooooot");

            IQueryColumnSourceFactory sourceFactory = new QueryColumnSourceFactory();
            QueryOrUnion qu = new QueryOrUnion();
            var qspecs = new List<QuerySpecification>();
            CollectQuerySpecifications(qspecs, qryExpr);
            foreach (var qspec in qspecs)
            {
                QueryColumnSourceMQE source = sourceFactory.NewQueryColumnSourceMQE(qspec, "q" + (qu.Count + 1));
                CollectQuerySpecification(sourceFactory, source, ctes);
                qu.AddQuery(source);
            }

            foreach (var source in qu.queries)
            {
                ResolveSelectedElements(batchResolver, source);

                foreach (var qry in source.union_queries)
                {
                    ResolveSelectedElements(batchResolver, source);
                }
            }


            if (qu.Count == 1)
            {
                model.Root = qu.SingleOne;
            }
            else
            {
                throw new NotImplementedException();
                //var source = new QueryColumnSourceMQE(false, "$$$_root_$$$", "rooooot");
                //root_sources.ForEach(ts => source.AddMqeColumnSource(ts));
                //
                //model.Root = source;
            }
            return model;
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

        private static void CollectQuerySpecification(IQueryColumnSourceFactory sourceFactory, QueryColumnSourceMQE source, WithCtesAndXmlNamespaces ctes)
        {
            //QueryColumnSourceMQE source = new QueryColumnSourceMQE(false, key, "qspec");
            foreach (TableReference tableRef in source.QrySpec.FromClause.TableReferences)
            {
                CollectTableRef(sourceFactory, tableRef, ctes, (ts) =>
                {
                    source.AddMqeColumnSource(ts);
                });
            }
        }

        private static void CollectTableRef(IQueryColumnSourceFactory sourceFactory, TableReference tableRef, WithCtesAndXmlNamespaces ctes, Action<QueryColumnSourceBase> collector)
        {
            if (tableRef is QualifiedJoin qJoin)
            {
                CollectTableRef(sourceFactory, qJoin.FirstTableReference, ctes, collector);
                CollectTableRef(sourceFactory, qJoin.SecondTableReference, ctes, collector);
            }
            else if (tableRef is UnqualifiedJoin uqJoin)
            {
                CollectTableRef(sourceFactory, uqJoin.FirstTableReference, ctes, collector);
                CollectTableRef(sourceFactory, uqJoin.SecondTableReference, ctes, collector);
            }
            else if (tableRef is NamedTableReference ntRef)
            {
                CollectNamedTableReference(sourceFactory, ntRef, ctes, collector);
            }
            else if (tableRef is SchemaObjectFunctionTableReference udtfRef)
            {
                collector(sourceFactory.NewQueryColumnSourceUDTF(udtfRef));
            }
            else if (tableRef is VariableTableReference varTableRef)
            {
                collector(sourceFactory.NewQueryColumnSourceVarTable(varTableRef));
            }
            else
            {
                throw new NotImplementedException(tableRef.WhatIsThis());
            }
        }

        private static void CollectNamedTableReference(IQueryColumnSourceFactory sourceFactory, NamedTableReference node, WithCtesAndXmlNamespaces ctes, Action<QueryColumnSourceBase> collector)
        {
            if (node.SchemaObject.SchemaIdentifier == null &&
                ctes != null &&
                TryGetCTEByName(ctes, node.SchemaObject.BaseIdentifier.Dequote(), out CommonTableExpression cte))
            {
                //QueryColumnSourceBase cte_source = new QueryColumnSourceCTE(cte, node.Alias);
                //QueryColumnSourceBase cte_source = null;

                var qspecs = new List<QuerySpecification>();
                CollectQuerySpecifications(qspecs, cte.QueryExpression);

                if (qspecs.Count == 0)
                {
                    throw new NotImplementedException(node.WhatIsThis());
                }

                QueryColumnSourceMQE first_query = null;
                for (int ix = 0; ix < qspecs.Count; ix++)
                {
                    QuerySpecification qspec = qspecs[ix];
                    if (ix == 0)
                    {
                        string key = node.Alias != null ? node.Alias.Dequote() : node.SchemaObject.BaseIdentifier.Dequote();
                        QueryColumnSourceMQE cte_query = sourceFactory.NewQueryColumnSourceMQE(qspec, key);
                        first_query = cte_query;
                        CollectQuerySpecification(sourceFactory, cte_query, ctes);
                    }
                    else
                    {
                        // UNION query
                        QueryColumnSourceMQE cte_query = sourceFactory.NewQueryColumnSourceMQE(qspec, "q" + (ix + 1));
                        CollectQuerySpecification(sourceFactory, cte_query, ctes);
                        first_query.AddUnionQuery(cte_query);
                    }
                }

                collector(first_query);

            }
            else
            {
                collector(sourceFactory.NewQueryColumnSourceNT(node));
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

        private static void ResolveSelectedElements(BatchOutputColumnTypeResolver batchResolver, QueryColumnSourceMQE mqe)
        {
            foreach (SelectElement node in mqe.QrySpec.SelectElements)
            {
                QueryColumnBase outputColumn = null;
                if (TryResolveSelectedElement(batchResolver, mqe, node, ref outputColumn))
                {
                    mqe.AddOutputColumn(outputColumn);
                }
            }
        }

        private static bool TryResolveSelectedElement(BatchOutputColumnTypeResolver batchResolver, QueryColumnSourceMQE mqe, SelectElement node, ref QueryColumnBase outputColumn)
        {
            if (node is SelectScalarExpression scalarExpr)
            {
                if (StatementOutputColumnTypeResolverV2.TryGetOutputColumnName(scalarExpr, out string outputColumnName))
                {
                    outputColumn = mqe.AddOutputColumn(outputColumnName, null, null);
                }

                return TryResolveScalarExpression(batchResolver, mqe, scalarExpr, outputColumnName, ref outputColumn);
            }
            else
            {
                throw new NotImplementedException(node.WhatIsThis());
            }
        }

        private static bool TryResolveScalarExpression(BatchOutputColumnTypeResolver batchResolver, QueryColumnSourceMQE mqe, SelectScalarExpression scalarExpr, string outputColumnName, ref QueryColumnBase outputColumn)
        {
            if (scalarExpr.Expression is ColumnReferenceExpression colRef)
            {
                return TryResolveSelectedColumnReferenceExpression(batchResolver, mqe, scalarExpr, outputColumnName, colRef, ref outputColumn);
            }
            else if (scalarExpr.Expression is NullLiteral nullLit)
            {
                return TryResolveSelectedColumnNullLiteral(batchResolver, mqe, scalarExpr, nullLit, ref outputColumn);
            }
            else if (scalarExpr.Expression is FunctionCall fCall)
            {
                return TryResolveSelectedColumnFunctionCall(batchResolver, mqe, scalarExpr, fCall, ref outputColumn);
            }
            else if (scalarExpr.Expression is IntegerLiteral intLit)
            {
                outputColumn = mqe.AddOutputColumn(outputColumnName, null, DbType.Int32);
                return true;
            }
            else
            {
                throw new NotImplementedException(scalarExpr.Expression.WhatIsThis());
            }
        }

        private static bool TryResolveSelectedColumnNullLiteral(BatchOutputColumnTypeResolver batchResolver, QueryColumnSourceMQE mqe, SelectScalarExpression scalarExpr, NullLiteral nullLit, ref QueryColumnBase outputColumn)
        {
            //add as resolved?
            if (mqe.SourceCount == 1)
            {
                var source = mqe.SourceSingle;
                if (source is QueryColumnSourceMQE sub_mqe)
                {
                    //ResolveSelectedElements(batchResolver, sub_mqe);
                    return false;
                }
                else
                {
                    // null is null
                    return false;
                }
            }
            else
            {
                throw new NotImplementedException(scalarExpr.Expression.WhatIsThis());
            }
        }

        private static bool TryResolveSelectedColumnReferenceExpression(BatchOutputColumnTypeResolver batchResolver, QueryColumnSourceMQE mqe, SelectScalarExpression scalarExpr, string outputColumnName, ColumnReferenceExpression colRef, ref QueryColumnBase outputColumn)
        {
            if (colRef.MultiPartIdentifier.Count == 2)
            {
                string sourceNameOrAlias = colRef.MultiPartIdentifier[0].Dequote();
                string sourceColumnName = colRef.MultiPartIdentifier[1].Dequote();

                if (mqe.TryFindSource(sourceNameOrAlias, out QueryColumnSourceBase source))
                {
                    if (source is QueryColumnSourceMQE sub_mqe)
                    {
                        //??? TryResolveSelectedElement(batchResolver, sub_mqe, );
                        return false;
                    }
                    else
                    {
                        var col = source.TryResolveSelectedColumn(batchResolver, outputColumnName, sourceColumnName);
                        if (col != null)
                        {
                            // coool!
                            outputColumn = col;
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
                    if (source is QueryColumnSourceMQE sub_mqe)
                    {
                        //???? ResolveSelectedElements(batchResolver, sub_mqe);
                        return false;
                    }
                    else
                    {
                        var col = source.TryResolveSelectedColumn(batchResolver, outputColumnName, sourceColumnName);
                        if (col != null)
                        {
                            // coool!
                            outputColumn = col;
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

        private static bool TryResolveSelectedColumnFunctionCall(BatchOutputColumnTypeResolver batchResolver, QueryColumnSourceMQE mqe, SelectScalarExpression scalarExpr, FunctionCall fCall, ref QueryColumnBase outputColumn)
        {
            string functionName = fCall.FunctionName.Dequote();
            if (string.Equals(functionName, "ISNULL", StringComparison.OrdinalIgnoreCase))
            {
                var prm1 = fCall.Parameters[0];
                var prm2 = fCall.Parameters[1];

                DbType? functionOutputType = null;
                if (TryResolveExpression(batchResolver, mqe, prm1, out DbType outputColumn1))
                {
                    functionOutputType = outputColumn1;
                }
                if (TryResolveExpression(batchResolver, mqe, prm2, out DbType outputColumn2))
                {
                    functionOutputType = outputColumn2;
                }

                if (functionOutputType.HasValue)
                {
                    outputColumn.SetColumnDbType(functionOutputType.Value);
                    return true;
                }

                throw new NotImplementedException(prm1.WhatIsThis());
                throw new NotImplementedException(prm2.WhatIsThis());
            }
            throw new NotImplementedException(fCall.WhatIsThis());
        }

        private static bool TryResolveExpression(BatchOutputColumnTypeResolver batchResolver, QueryColumnSourceMQE mqe, ScalarExpression node, out DbType columnDbType)
        {
            if (node is NullIfExpression nullIf)
            {
                DbType? functionOutputType = null;
                if (TryResolveExpression(batchResolver, mqe, nullIf.FirstExpression, out DbType outputColumnF))
                {
                    functionOutputType = outputColumnF;
                }

                if (TryResolveExpression(batchResolver, mqe, nullIf.SecondExpression, out DbType outputColumnS))
                {
                    functionOutputType = outputColumnS;
                }

                if (functionOutputType.HasValue)
                {
                    columnDbType = functionOutputType.Value;
                    return true;
                }

                throw new NotImplementedException(nullIf.FirstExpression.WhatIsThis());
                throw new NotImplementedException(nullIf.SecondExpression.WhatIsThis());
            }
            else if (node is ColumnReferenceExpression colRef)
            {
                return TryResolveExpressionColumnReference(batchResolver , mqe, colRef, out columnDbType);
            }
            else if (node is StringLiteral strLit)
            {
                columnDbType = DbType.String;
                return true;
            }
            else
            {
                throw new NotImplementedException(node.WhatIsThis());
            }
        }

        private static bool TryResolveExpressionColumnReference(BatchOutputColumnTypeResolver batchResolver, QueryColumnSourceMQE mqe, ColumnReferenceExpression colRef, out DbType columnType)
        {
            if (colRef.MultiPartIdentifier.Count == 2)
            {
                string sourceNameOrAlias = colRef.MultiPartIdentifier[0].Dequote();
                string sourceColumnName = colRef.MultiPartIdentifier[1].Dequote();

                if (mqe.TryFindSource(sourceNameOrAlias, out QueryColumnSourceBase source))
                {
                    //if (source is QueryColumnSourceMQE sub_mqe)
                    {
                        //??? TryResolveSelectedElement(batchResolver, sub_mqe, );
                        //return false;
                    }
                    //else
                    {
                        if (source.TryResolveSourceColumnType(batchResolver, sourceColumnName, out columnType))
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
                        if (source.TryResolveSourceColumnType(batchResolver, sourceColumnName, out columnType))
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