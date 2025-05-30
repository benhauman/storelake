﻿using System;
using System.Data;
using System.Diagnostics;
using Microsoft.SqlServer.TransactSql.ScriptDom;

[assembly: DebuggerDisplay(@"\{Bzz = {BaseIdentifier.Value}}", Target = typeof(SchemaObjectName))]
[assembly: DebuggerDisplay(@"\{Bzz = {StoreLake.Sdk.SqlDom.SqlDomExtensions.WhatIsThis(SchemaObject)}}", Target = typeof(NamedTableReference))]
[assembly: DebuggerDisplay(@"{StoreLake.Sdk.SqlDom.SqlDomExtensions.WhatIsThis(this)}", Target = typeof(TSqlFragment))]
// see [DebuggerTypeProxy(typeof(HashtableDebugView))]
namespace StoreLake.Sdk.SqlDom
{
    public sealed class StatementOutputColumnTypeResolverV2
    {
        internal readonly ISchemaMetadataProvider SchemaMetadata;
        private BatchOutputColumnTypeResolver batchResolver;
        private StatementWithCtesAndXmlNamespaces statement;
        private IQueryModel _model;
        public StatementOutputColumnTypeResolverV2(BatchOutputColumnTypeResolver batchResolver, StatementWithCtesAndXmlNamespaces statement)
        {
            SchemaMetadata = batchResolver.SchemaMetadata;
            this.batchResolver = batchResolver;
            this.statement = statement;
        }

        private static OutputColumnDescriptor ColumnModelToDescriptor(QueryColumnBase column)
        {
            return column.ColumnDbType.HasValue
                ? new OutputColumnDescriptor(column.OutputColumnName, new ColumnTypeMetadata(column.ColumnDbType.Value, column.AllowNull.GetValueOrDefault(true)))
                : new OutputColumnDescriptor(column.OutputColumnName);
        }
        private static OutputColumnDescriptor ColumnModelToDescriptor(DbType columnDbType, bool allowNull)
        {
            string outputColumnNameAnonymous = null;
            return new OutputColumnDescriptor(outputColumnNameAnonymous, new ColumnTypeMetadata(columnDbType, allowNull));
        }

        public OutputColumnDescriptor ResolveColumnReference(ColumnReferenceExpression node)
        {
            IQueryModel model = EnsureModel();

            if (TryColumnReference(model, node, out QueryColumnBase col))
                return ColumnModelToDescriptor(col);

            throw new NotImplementedException(node.WhatIsThis());
        }

        public OutputColumnDescriptor ResolveSelectScalarExpression(SelectScalarExpression node)
        {
            IQueryModel model = EnsureModel();
            if (TryGetOutputColumnName(node, out string outputColumnName))
            {
                if (model.TryGetQueryOutputColumnByName(batchResolver, outputColumnName, out QueryColumnBase col))
                {
                    return ColumnModelToDescriptor(col);
                }

                throw new NotImplementedException(node.Expression.WhatIsThis());
            }
            else if (node.Expression is ColumnReferenceExpression colRef)
            {
                if (TryColumnReference(model, colRef, out QueryColumnBase col))
                {
                    return ColumnModelToDescriptor(col);
                }

                throw new NotImplementedException(node.Expression.WhatIsThis());
            }
            else if (node.Expression is IIfCall iif)
            {
                // no output name! if single column output -> use it???
                if (TrtGetAnonymousColumnType_ScalarExpression(iif.ThenExpression, out DbType columnDbTypeT, out bool allowNull))
                {
                    return ColumnModelToDescriptor(columnDbTypeT, allowNull);
                }
                if (TrtGetAnonymousColumnType_ScalarExpression(iif.ThenExpression, out DbType columnDbTypeE, out allowNull))
                {
                    return ColumnModelToDescriptor(columnDbTypeE, allowNull);
                }
                throw new NotImplementedException(iif.ElseExpression.WhatIsThis());
            }
            else if (node.Expression is CastCall castExpr)
            {
                var dbType = ProcedureGenerator.ResolveToDbDataType(castExpr.DataType);
                return ColumnModelToDescriptor(dbType, true);
            }
            else if (node.Expression is VariableReference varRefExpr)
            {
                var varType = batchResolver.TryGetScalarVariableType(varRefExpr.Name);
                if (varType != null)
                    return ColumnModelToDescriptor(varType.ColumnDbType, varType.AllowNull);
                throw new NotImplementedException(varRefExpr.Name);
            }
            else if (node.Expression is FunctionCall fCall)
            {
                string functionName = fCall.FunctionName.Dequote();
                if (string.Equals(functionName, "COUNT", StringComparison.OrdinalIgnoreCase))
                {
                    return ColumnModelToDescriptor(DbType.Int32, true);
                }

                throw new NotImplementedException(fCall.WhatIsThis());
            }
            else
            {
                throw new NotImplementedException(node.Expression.WhatIsThis());
            }
        }

        private static bool TrtGetAnonymousColumnType_ScalarExpression(ScalarExpression scalarExpr, out DbType columnDbType, out bool allowNull)
        {
            if (scalarExpr is IntegerLiteral intLit)
            {
                columnDbType = DbType.Int32;
                allowNull = true;
                return true;
            }

            throw new NotImplementedException(scalarExpr.WhatIsThis());
        }

        internal static bool TryGetOutputColumnName(SelectScalarExpression sscalarExpr, out string columnName)
        {
            if (sscalarExpr.ColumnName != null)
            {
                if (sscalarExpr.ColumnName.ValueExpression != null)
                {
                    throw new NotImplementedException(sscalarExpr.ColumnName.ValueExpression.WhatIsThis());
                }
                else if (sscalarExpr.ColumnName.Identifier != null)
                {
                    columnName = sscalarExpr.ColumnName.Identifier.Dequote();
                    return true;
                }
                else
                {
                    if (!string.IsNullOrEmpty(sscalarExpr.ColumnName.Value))
                    {
                        columnName = sscalarExpr.ColumnName.Value;
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

        private bool TryColumnReference(IQueryModel model, ColumnReferenceExpression node, out QueryColumnBase col)
        {
            if (node.ColumnType != ColumnType.Regular)
                throw new NotImplementedException(node.AsText());

            if (node.MultiPartIdentifier.Count == 2)
            {
                // source (table/view) name without schema or alias
                string sourceNameOrAlias = node.MultiPartIdentifier[0].Dequote();
                string columnName = node.MultiPartIdentifier[1].Dequote();

                if (model.TryGetQueryOutputColumnByName(this.batchResolver, columnName, out col))
                {
                    return true;
                }
                else
                {
                    throw new NotImplementedException(node.WhatIsThis()); // not resolved?
                }
            }
            else if (node.MultiPartIdentifier.Count == 1)
            {
                // no source only column name => traverse all source and find t
                string columnName = node.MultiPartIdentifier[0].Dequote();
                if (model.TryGetQueryOutputColumnByName(this.batchResolver, columnName, out col))
                {
                    return true;
                }
                else
                {
                    throw new NotImplementedException(node.WhatIsThis()); // not resolved?
                }
            }
            else
            {
                // 3 or 4
                throw new NotImplementedException(node.AsText() + "   ## " + statement.WhatIsThis());
            }
        }

        private IQueryModel EnsureModel()
        {
            if (_model == null)
            {
                if (statement is SelectStatement stmt_sel)
                {
                    _model = QueryModelLoader.LoadModel(batchResolver, "roooot", stmt_sel.QueryExpression, stmt_sel.WithCtesAndXmlNamespaces);
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

                    _model = QueryModelLoader.LoadModificationOutputModel(batchResolver, "roooot", source_specification, stmt_mod.WithCtesAndXmlNamespaces);
                }
            }
            return _model;
        }
    }
}