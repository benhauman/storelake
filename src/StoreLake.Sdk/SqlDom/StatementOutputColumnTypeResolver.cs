using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Data;

namespace StoreLake.Sdk.SqlDom
{
    internal class StatementOutputColumnTypeResolver
    {
        internal readonly ISchemaMetadataProvider SchemaMetadata;
        BatchOutputColumnTypeResolver batchResolver;
        StatementWithCtesAndXmlNamespaces statement;
        public StatementOutputColumnTypeResolver(BatchOutputColumnTypeResolver batchResolver, StatementWithCtesAndXmlNamespaces statement)
        {
            SchemaMetadata = batchResolver.SchemaMetadata;
            this.batchResolver = batchResolver;
            this.statement = statement;
        }
        internal DbType? ResolveColumnReference(ColumnReferenceExpression node)
        {
            return ResolveColumnReferenceCore(node);
        }
        private DbType? ResolveColumnReferenceCore(ColumnReferenceExpression node)
        {
            if (node.ColumnType != ColumnType.Regular)
                throw new NotImplementedException(node.AsText());

            if (node.MultiPartIdentifier.Count == 2)
            {
                // source (table/view) name without schema or alias
                string sourceNameOrAlias = node.MultiPartIdentifier[0].Dequote();
                string columnName = node.MultiPartIdentifier[1].Dequote();

                return TryResolveColumnReferenceCoreSN(node, sourceNameOrAlias, columnName);
            }
            else if (node.MultiPartIdentifier.Count == 1)
            {
                // no source only column name => traverse all source and find t
                string columnName = node.MultiPartIdentifier[0].Dequote();
                return TryResolveColumnReferenceCoreSN(node, null, columnName);
            }
            else
            {
                // 3 or 4
                throw new NotImplementedException(node.AsText() + "   ## " + statement.WhatIsThis());
            }


            //return null;
        }

        private DbType? TryResolveColumnReferenceCoreSN(TSqlFragment node, string sourceNameOrAlias, string columnName)
        {
            DbType resultColumnType;
            if (statement is SelectStatement stmt_sel)
            {
                if (TryQuerySpecification(true, stmt_sel.QueryExpression as QuerySpecification, sourceNameOrAlias, columnName, out resultColumnType))
                {
                    return resultColumnType;
                }

                throw new NotImplementedException(stmt_sel.QueryExpression.WhatIsThis());
            }

            DataModificationSpecification source_specification = null;
            if (statement is InsertStatement stmt_ins && string.Equals(sourceNameOrAlias, "INSERTED", StringComparison.OrdinalIgnoreCase))
                source_specification = stmt_ins.InsertSpecification;
            else if (statement is UpdateStatement stmt_upd)
                source_specification = stmt_upd.UpdateSpecification;
            else if (statement is DeleteStatement stmt_del)
                source_specification = stmt_del.DeleteSpecification;
            else if (statement is MergeStatement stmt_mrg)
                source_specification = stmt_mrg.MergeSpecification;

            if (source_specification != null && source_specification.OutputClause != null)
            {
                if (TryTableReference(true, true, source_specification.Target, null, columnName, out resultColumnType))
                {
                    return resultColumnType;
                }

                throw new NotImplementedException("Target could not be found." + statement.WhatIsThis());
            }
            throw new NotImplementedException(node.WhatIsThis());
            //return null;
        }

        private bool TrySelectStatement(bool throwOnSourceNotFound, SelectStatement selectStatement, string sourceNameOrAlias, string columnName, out DbType resultColumnType)
        {
            if (selectStatement != null && selectStatement.QueryExpression != null)
            {
                if (TryQuerySpecification(throwOnSourceNotFound, selectStatement.QueryExpression as QuerySpecification, sourceNameOrAlias, columnName, out resultColumnType))
                {
                    return true;
                }

                throw new NotImplementedException(selectStatement.QueryExpression.WhatIsThis());
            }

            resultColumnType = DbType.Object;
            return false;
        }

        private bool TryQuerySpecification(bool throwOnSourceNotFound, QuerySpecification querySpecification, string sourceNameOrAlias, string columnName, out DbType resultColumnType)
        {
            if (querySpecification != null && querySpecification.FromClause != null)
            {
                if (TryFromClause(throwOnSourceNotFound, querySpecification.FromClause, sourceNameOrAlias, columnName, out resultColumnType))
                {
                    return true;
                }

                throw new NotImplementedException(querySpecification.FromClause.WhatIsThis());
            }
            resultColumnType = DbType.Object;
            return false;
        }

        private bool TryFromClause(bool throwOnSourceNotFound, FromClause fromClause, string sourceNameOrAlias, string columnName, out DbType resultColumnType)
        {
            foreach (TableReference tableRef in fromClause.TableReferences)
            {
                if (TryTableReference(false, false, tableRef, sourceNameOrAlias, columnName, out resultColumnType))
                {
                    return true;
                }
            }

            if (throwOnSourceNotFound)
            {
                throw new NotImplementedException(columnName + " ## " + fromClause.WhatIsThis());
            }

            resultColumnType = DbType.Object;
            return false;
        }


        private bool TryTableReference(bool throwOnSourceNotFound, bool throwOnColumnNotFound, TableReference tableRef, string sourceNameOrAlias, string columnName, out DbType resultColumnType)
        {
            if (tableRef is NamedTableReference ntRef)
            {
                bool useIt;
                if (!string.IsNullOrEmpty(sourceNameOrAlias))
                {
                    if ((ntRef.Alias != null) && string.Equals(sourceNameOrAlias, ntRef.Alias.Dequote(), StringComparison.OrdinalIgnoreCase))
                    {
                        // this is the alias
                        useIt = true;
                    }
                    else
                    {
                        //var lastId = ntRef.iden
                        if (string.Equals(sourceNameOrAlias, ntRef.SchemaObject.BaseIdentifier.Dequote(), StringComparison.OrdinalIgnoreCase))
                        {
                            // this is the source
                            useIt = true;
                        }
                        else
                        {
                            // not the requested source/alias
                            useIt = false;
                        }
                    }
                }
                else
                {
                    useIt = true;
                }

                if (useIt)
                {
                    if (TryNamedTable(throwOnColumnNotFound, ntRef, columnName, out resultColumnType))
                    {
                        return true;
                    }

                    throw new NotImplementedException(columnName + " ## " + tableRef.WhatIsThis());
                }
                else
                {
                    // dont use it 
                    if (throwOnSourceNotFound)
                    {
                        throw new NotImplementedException(columnName + " ## " + tableRef.WhatIsThis());
                    }
                    resultColumnType = DbType.Object;
                    return false;
                }
            }
            else if (tableRef is VariableTableReference varTable)
            {
                if (TryVariableTableReference(true, varTable, columnName, out resultColumnType))
                {
                    return true;
                }
                if (throwOnSourceNotFound)
                {
                    throw new NotImplementedException(columnName + " ## " + tableRef.WhatIsThis());
                }
                resultColumnType = DbType.Object;
                return false;
            }
            else
            {
                throw new NotImplementedException(columnName + " ## " + tableRef.WhatIsThis());
            }

        }

        private bool TryNamedTable(bool throwOnColumnNotFound, NamedTableReference ntRef, string columnName, out DbType resultColumnType)
        {
            IColumnSourceMetadata sourceMetadata = SchemaMetadata.TryGetColumnSourceMetadata(ntRef.SchemaObject.SchemaIdentifier.Dequote(), ntRef.SchemaObject.BaseIdentifier.Dequote());
            if (sourceMetadata != null)
            {
                var coltype = sourceMetadata.TryGetColumnTypeByName(columnName);
                if (coltype.HasValue)
                {
                    resultColumnType = coltype.Value;
                    return true;
                }
                if (throwOnColumnNotFound)
                {
                    throw new NotImplementedException(ntRef.WhatIsThis());
                }
                resultColumnType = DbType.Object;
                return false;
            }
            //ntRef.SchemaObject
            throw new NotImplementedException(ntRef.WhatIsThis());
        }

        private bool TryVariableTableReference(bool throwOnColumnNotFound, VariableTableReference ntRef, string columnName, out DbType resultColumnType)
        {
            IColumnSourceMetadata sourceMetadata = batchResolver.TryGetTableVariable(ntRef.Variable.Name);
            if (sourceMetadata != null)
            {
                var coltype = sourceMetadata.TryGetColumnTypeByName(columnName);
                if (coltype.HasValue)
                {
                    resultColumnType = coltype.Value;
                    return true;
                }
                if (throwOnColumnNotFound)
                {
                    throw new NotImplementedException(ntRef.WhatIsThis());
                }
                resultColumnType = DbType.Object;
                return false;
            }
            //ntRef.SchemaObject
            throw new NotImplementedException(ntRef.WhatIsThis());
        }

        internal DbType? ResolveScalarExpression(SelectScalarExpression node)
        {
            if (TryResolveScalarExpression(node.Expression, out DbType? columnDbType))
                return columnDbType;
            return null; // nullliteral?
        }

        private bool TryResolveScalarExpression(ScalarExpression scalarExpr, out DbType? columnDbType)
        {
            if (scalarExpr is ColumnReferenceExpression columnRef)
            {
                columnDbType = ResolveColumnReferenceCore(columnRef);
                return columnDbType.HasValue;
            }
            if (scalarExpr is NullLiteral nullLit)
            {
                columnDbType = null;
                return false;
            }
            if (scalarExpr is IntegerLiteral intLit)
            {
                columnDbType = DbType.Int32;
                return true;
            }
            if (scalarExpr is IIfCall iif)
                return TryIIfCall(iif, out columnDbType);

            throw new NotImplementedException(scalarExpr.WhatIsThis() + "   ## " + statement.WhatIsThis());
        }

        private bool TryIIfCall(IIfCall iif, out DbType? columnDbType)
        {
            if (TryResolveScalarExpression(iif.ThenExpression, out columnDbType))
                return true;
            if (TryResolveScalarExpression(iif.ElseExpression, out columnDbType))
                return true;

            throw new NotImplementedException(iif.WhatIsThis() + "   ## " + statement.WhatIsThis());
        }
    }
}