using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.Diagnostics;

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

        internal OutputColumnDescriptor ResolveColumnReference(ColumnReferenceExpression node)
        {
            StatementModel model = EnsureModel();
            throw new NotImplementedException();
        }

        internal OutputColumnDescriptor ResolveScalarExpression(SelectScalarExpression node)
        {
            StatementModel model = EnsureModel();
            throw new NotImplementedException(node.WhatIsThis());
        }

        private StatementModel EnsureModel()
        {
            if (_model == null)
            {
                if (statement is SelectStatement stmt_sel)
                {
                    _model = QueryModelLoader.LoadModel(stmt_sel.QueryExpression, stmt_sel.WithCtesAndXmlNamespaces);
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


    internal static class QueryModelLoader
    {
        internal static StatementModel LoadModel(QueryExpression qryExpr, WithCtesAndXmlNamespaces ctes)
        {
            StatementModel model = new StatementModel();
            List<QueryColumnSourceBase> root_sources = new List<QueryColumnSourceBase>();
            CollectQueryExpression(null, qryExpr, null, ctes, (ts) =>
            {
                root_sources.Add(ts);
            });

            if (root_sources.Count == 1)
            {
                model.Root = root_sources[0];
            }
            else
            {
                var source = new QueryColumnSourceMQE(null);
                root_sources.ForEach(ts => source.AddColumnSource(ts));

                model.Root = source;
            }
            return model;
        }

        private static void CollectQueryExpression(QueryColumnSourceBase unionFirst, QueryExpression qryExr, Identifier alias, WithCtesAndXmlNamespaces ctes, Action<QueryColumnSourceBase> collector)
        {
            if (qryExr is QuerySpecification qrySpec)
            {
                var source = new QueryColumnSourceMQE(alias);

                foreach (TableReference tableRef in qrySpec.FromClause.TableReferences)
                {
                    CollectTableRef(tableRef, ctes, (ts) =>
                    {
                        source.AddColumnSource(ts);
                    });
                }

                collector(source);

            }
            else if (qryExr is BinaryQueryExpression bqExpr)
            {
                if (unionFirst == null)
                {
                    var source = new QueryColumnSourceMQE(alias);
                    CollectQueryExpression(source, bqExpr.FirstQueryExpression, alias, ctes, ts =>
                    {
                        source.AddColumnSource(ts);
                    });

                    CollectQueryExpression(source, bqExpr.SecondQueryExpression, alias, ctes, ts =>
                    {
                        source.AddQuery(ts);
                    });
                    collector(source);
                }
                else
                {
                    CollectQueryExpression(unionFirst, bqExpr.FirstQueryExpression, alias, ctes, ts =>
                    {
                        unionFirst.AddQuery(ts);
                    });

                    CollectQueryExpression(unionFirst, bqExpr.SecondQueryExpression, alias, ctes, ts =>
                    {
                        unionFirst.AddQuery(ts);
                    });
                }
            }
            else
            {
                throw new NotImplementedException(qryExr.WhatIsThis());
            }
        }

        private static void CollectTableRef(TableReference tableRef, WithCtesAndXmlNamespaces ctes, Action<QueryColumnSourceBase> collector)
        {
            if (tableRef is QualifiedJoin qJoin)
            {
                CollectTableRef(qJoin.FirstTableReference, ctes, collector);
                CollectTableRef(qJoin.SecondTableReference, ctes, collector);
            }
            else if (tableRef is UnqualifiedJoin uqJoin)
            {
                CollectTableRef(uqJoin.FirstTableReference, ctes, collector);
                CollectTableRef(uqJoin.SecondTableReference, ctes, collector);
            }
            else if (tableRef is NamedTableReference ntRef)
            {
                CollectNamedTableReference(ntRef, ctes, collector);
            }
            else if (tableRef is SchemaObjectFunctionTableReference udtfRef)
            {
                collector(new QueryColumnSourceUDTF(udtfRef));
            }
            else if (tableRef is VariableTableReference varTableRef)
            {
                collector(new QueryColumnSourceVarTable(varTableRef));
            }
            else
            {
                throw new NotImplementedException(tableRef.WhatIsThis());
            }
        }

        private static void CollectNamedTableReference(NamedTableReference node, WithCtesAndXmlNamespaces ctes, Action<QueryColumnSourceBase> collector)
        {
            if (node.SchemaObject.SchemaIdentifier == null &&
                ctes != null &&
                TryGetCTEByName(ctes, node.SchemaObject.BaseIdentifier.Dequote(), out CommonTableExpression cte))
            {
                var cte_source = new QueryColumnSourceCTE(cte, node.Alias);

                CollectQueryExpression(null, cte.QueryExpression, null, ctes, ts =>
                {
                    cte_source.AddCteSource(ts);
                    //cte_source.AddColumnSource(ts);
                });

                //foreach (var src in cte_qry_source.sources)
                //    cte_source.AddColumnSource(src);
                //foreach (var qry in cte_qry_source.queries)
                //    cte_source.AddQuery(qry);

                //cte_source.SetColumns(cte_qry_source);
                //foreach (var col in cte.Columns)
                //{
                //    //cte_source.AddSelectElement();
                //}

                collector(cte_source);
            }
            else
            {
                collector(new QueryColumnSourceNT(node));
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
    }

}