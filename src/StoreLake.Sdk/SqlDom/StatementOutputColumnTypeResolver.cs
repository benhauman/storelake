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
        internal OutputColumnDescriptor ResolveColumnReference(ColumnReferenceExpression node)
        {
            return ResolveColumnReferenceCore(node);
        }
        private OutputColumnDescriptor ResolveColumnReferenceCore(ColumnReferenceExpression node)
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

        private OutputColumnDescriptor TryResolveColumnReferenceCoreSN(TSqlFragment node, string sourceNameOrAlias, string columnName)
        {
            OutputColumnDescriptor resultColumnType;
            if (statement is SelectStatement stmt_sel)
            {
                if (TryQueryExpression(stmt_sel.QueryExpression, sourceNameOrAlias, columnName, out resultColumnType))
                {
                    return resultColumnType;
                }
                return null;
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

        private bool TryQueryExpression(QueryExpression queryExpr, string sourceNameOrAlias, string columnName, out OutputColumnDescriptor resultColumnType)
        {
            if (queryExpr is QuerySpecification querySpecification)
            {
                if (querySpecification.FromClause != null)
                {
                    foreach (TableReference tableRef in querySpecification.FromClause.TableReferences)
                    {
                        if (TryTableReference(false, false, tableRef, sourceNameOrAlias, columnName, out resultColumnType))
                        {
                            return true;
                        }
                    }
                }

                resultColumnType = null;
                return false;
            }

            if (queryExpr is BinaryQueryExpression bqryExpr) // UNION (ALL)
            {
                if (TryQueryExpression(bqryExpr.FirstQueryExpression, sourceNameOrAlias, columnName, out resultColumnType))
                    return true;
                if (TryQueryExpression(bqryExpr.SecondQueryExpression, sourceNameOrAlias, columnName, out resultColumnType))
                    return true;
                resultColumnType = null;
                return false;
            }

            throw new NotImplementedException(queryExpr.WhatIsThis());
            //resultColumnType = null;
            //return false;
        }


        private bool TryTableReference(bool throwOnSourceNotFound, bool throwOnColumnNotFound, TableReference tableRef, string sourceNameOrAlias, string columnName, out OutputColumnDescriptor resultColumnType)
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

                    //throw new NotImplementedException(columnName + " ## " + tableRef.WhatIsThis());
                    return false;
                }
                else
                {
                    // dont use it 
                    if (throwOnSourceNotFound)
                    {
                        throw new NotImplementedException(columnName + " ## " + tableRef.WhatIsThis());
                    }
                    resultColumnType = null;
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
                resultColumnType = null;
                return false;
            }
            else if (tableRef is QualifiedJoin qJoin)
            {
                if (TryTableReference(false, false, qJoin.FirstTableReference, sourceNameOrAlias, columnName, out resultColumnType))
                {
                    return true;
                }

                if (TryTableReference(false, false, qJoin.SecondTableReference, sourceNameOrAlias, columnName, out resultColumnType))
                {
                    return true;
                }

                return false;
            }
            else if (tableRef is UnqualifiedJoin uqJoin) // OUTER APPLY
            {
                /*
                // => IBM: In qualified SQL, the creator is specified in front of the table or view name. In unqualified SQL, the creator is not specified.
                // => SQL 2016: For SQL Server 2016 upgrade – compatibility level to 130 – we need to replace Unqualified joins with Qualified joins.
                An example of “Unqualified join” is: select table1.col11, table2.col11 from table1, table2 where table1.col1 = table2.col1
                // => The thing is that standard use words unmodified, but we normally use words unqualified or unmodified.
                */
                // [dbo].[hlsysobjectdef] AS [o]
                if (TryTableReference(false, false, uqJoin.FirstTableReference, sourceNameOrAlias, columnName, out resultColumnType))
                {
                    return true;
                }

                if (TryTableReference(false, false, uqJoin.SecondTableReference, sourceNameOrAlias, columnName, out resultColumnType))
                {
                    return true;
                }

                return false;
            }
            else if (tableRef is QueryDerivedTable qdt) // 
            {
                if (TryQueryExpression(qdt.QueryExpression, sourceNameOrAlias, columnName, out resultColumnType))
                {
                    return true;
                }

                return false;
            }
            else if (tableRef is SchemaObjectFunctionTableReference udfTable)
            {
                SchemaMetadata.TryGetFunctionTableMetadata(udfTable.SchemaObject.SchemaIdentifier.Dequote(), udfTable.SchemaObject.BaseIdentifier.Dequote());
                resultColumnType = null; // use metadata
                return false;
            }
            else
            {
                throw new NotImplementedException(columnName + " ## " + tableRef.WhatIsThis());
            }

        }

        private bool TryNamedTable(bool throwOnColumnNotFound, NamedTableReference ntRef, string columnName, out OutputColumnDescriptor resultColumnType)
        {
            IColumnSourceMetadata sourceMetadata = SchemaMetadata.TryGetColumnSourceMetadata(ntRef.SchemaObject.SchemaIdentifier.Dequote(), ntRef.SchemaObject.BaseIdentifier.Dequote());
            if (sourceMetadata != null)
            {
                var coltype = sourceMetadata.TryGetColumnTypeByName(columnName);
                if (coltype.HasValue)
                {
                    resultColumnType = new OutputColumnDescriptor(columnName, coltype.Value);
                    return true;
                }
                if (throwOnColumnNotFound)
                {
                    string object_fullname = ntRef.SchemaObject.SchemaIdentifier.Value + "." + ntRef.SchemaObject.BaseIdentifier.Value;
                    throw new NotImplementedException("Invalid column name '" + columnName + "' for object:" + object_fullname);
                }
                resultColumnType = null;
                return false;
            }
            //ntRef.SchemaObject
            //throw new NotImplementedException(ntRef.WhatIsThis());
            resultColumnType = null;
            return false;
        }

        private bool TryVariableTableReference(bool throwOnColumnNotFound, VariableTableReference ntRef, string columnName, out OutputColumnDescriptor resultColumnType)
        {
            IColumnSourceMetadata sourceMetadata = batchResolver.TryGetTableVariable(ntRef.Variable.Name);
            if (sourceMetadata != null)
            {
                var coltype = sourceMetadata.TryGetColumnTypeByName(columnName);
                if (coltype.HasValue)
                {
                    resultColumnType = new OutputColumnDescriptor(columnName, coltype.Value);
                    return true;
                }
                if (throwOnColumnNotFound)
                {
                    throw new NotImplementedException(ntRef.WhatIsThis());
                }
                resultColumnType = null;
                return false;
            }
            //ntRef.SchemaObject
            throw new NotImplementedException(ntRef.WhatIsThis());
        }

        internal OutputColumnDescriptor ResolveScalarExpression(SelectScalarExpression node)
        {
            if (TryResolveScalarExpression(node.Expression, out OutputColumnDescriptor columnDbType))
                return columnDbType.SetOutputColumnName(node.ColumnName);
            return null; // nullliteral?
        }

        private bool TryResolveScalarExpression(ScalarExpression scalarExpr, out OutputColumnDescriptor columnDbType)
        {
            if (scalarExpr is ColumnReferenceExpression columnRef)
            {
                columnDbType = ResolveColumnReferenceCore(columnRef);
                return columnDbType != null;
            }
            if (scalarExpr is NullLiteral nullLit)
            {
                columnDbType = null;
                return false;
            }
            if (scalarExpr is IntegerLiteral intLit)
            {
                columnDbType = new OutputColumnDescriptor(DbType.Int32);
                return true;
            }
            if (scalarExpr is IIfCall iif)
                return TryIIfCall(iif, out columnDbType);

            if (scalarExpr is CoalesceExpression coalesceExpr)
            {
                return TryCoalesce(coalesceExpr, out columnDbType);
            }

            if (scalarExpr is NullIfExpression nullIf)
            {
                return TryNullIf(nullIf, out columnDbType);
            }

            if (scalarExpr is StringLiteral stringLit)
            {
                columnDbType = new OutputColumnDescriptor(DbType.String);
                return true;
            }

            if (scalarExpr is SimpleCaseExpression simpleCaseExpr)
            {
                return TrySimpleCaseExpr(simpleCaseExpr, out columnDbType);
            }

            if (scalarExpr is FunctionCall fCall) // ? ISNULL
            {
                return TryFunctionCall(fCall, out columnDbType);
            }
            if (scalarExpr is UnaryExpression unaryExpr) // ? ISNULL
            {
                return TryResolveScalarExpression(unaryExpr.Expression, out columnDbType);
            }
            throw new NotImplementedException(scalarExpr.WhatIsThis() + "   ## " + statement.WhatIsThis());
        }

        private bool TryFunctionCall(FunctionCall fCall, out OutputColumnDescriptor columnDbType)
        {
            if (string.Equals(fCall.FunctionName.Value, "ISNULL", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var parameter in fCall.Parameters)
                {
                    if (TryResolveScalarExpression(parameter, out columnDbType))
                        return true;
                }
                columnDbType = null;
                return false;
            }

            columnDbType = null;
            return false;
        }

        private bool TrySimpleCaseExpr(SimpleCaseExpression simpleCaseExpr, out OutputColumnDescriptor columnDbType)
        {

            foreach (SimpleWhenClause whenClause in simpleCaseExpr.WhenClauses)
            {
                if (TryResolveScalarExpression(whenClause.ThenExpression, out columnDbType))
                    return true;
            }
            if (simpleCaseExpr.ElseExpression != null)
            {
                if (TryResolveScalarExpression(simpleCaseExpr.ElseExpression, out columnDbType))
                    return true;
            }
            columnDbType = null;
            return false;
        }

        private bool TryNullIf(NullIfExpression nullIf, out OutputColumnDescriptor columnDbType)
        {
            if (TryResolveScalarExpression(nullIf.FirstExpression, out columnDbType))
                return true;
            return TryResolveScalarExpression(nullIf.SecondExpression, out columnDbType);
        }

        private bool TryCoalesce(CoalesceExpression coalesceExpr, out OutputColumnDescriptor columnDbType)
        {
            foreach (ScalarExpression scalarExpr in coalesceExpr.Expressions)
            {
                if (TryResolveScalarExpression(scalarExpr, out columnDbType))
                    return true;
            }
            columnDbType = null;
            return false;
        }

        private bool TryIIfCall(IIfCall iif, out OutputColumnDescriptor columnDbType)
        {
            if (TryResolveScalarExpression(iif.ThenExpression, out columnDbType))
                return true;
            if (TryResolveScalarExpression(iif.ElseExpression, out columnDbType))
                return true;

            throw new NotImplementedException(iif.WhatIsThis() + "   ## " + statement.WhatIsThis());
        }
    }
}