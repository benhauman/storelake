using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StoreLake.Sdk.SqlDom
{
    public static class ProcedureGenerator
    {
        public static ProcedureMetadata ParseProcedureBody(string procedure_name, string procedure_body)
        {
            TSqlFragment sqlF = ScriptDomFacade.Parse(procedure_body);
            return new ProcedureMetadata(procedure_name, sqlF);
        }
        public static ProcedureOutputSet[] IsQueryProcedure(bool resolveColumnType, ISchemaMetadataProvider schemaMetadata, ProcedureMetadata procedure_metadata)
        {
            BatchOutputColumnTypeResolver columnTypeResolver = new BatchOutputColumnTypeResolver(schemaMetadata, procedure_metadata.BodyFragment);
            StatementVisitor vstor = new StatementVisitor(resolveColumnType, columnTypeResolver, procedure_metadata.BodyFragment);
            procedure_metadata.BodyFragment.Accept(vstor);
            return vstor.resultHasOutputResultSet.ToArray();
        }

        class StatementVisitor : DumpFragmentVisitor
        {
            private readonly TSqlFragment _toAnalyze;
            private readonly BatchOutputColumnTypeResolver columnTypeResolver;
            private readonly bool resolveColumnType;

            internal readonly List<ProcedureOutputSet> resultHasOutputResultSet = new List<ProcedureOutputSet>();

            internal StatementVisitor(bool resolveColumnType, BatchOutputColumnTypeResolver columnTypeResolver, TSqlFragment toAnalyze) : base(false)
            {
                this.resolveColumnType = resolveColumnType;
                this.columnTypeResolver = columnTypeResolver;
                _toAnalyze = toAnalyze;
            }

            private void DoHasOutputResultSet(TSqlStatement toAnalyze)
            {
                StatementVisitor vstor = new StatementVisitor(resolveColumnType, columnTypeResolver, toAnalyze);
                toAnalyze.Accept(vstor);
                int cnt = vstor.resultHasOutputResultSet.Count;
                if (cnt > 0)
                {
                    this.resultHasOutputResultSet.AddRange(vstor.resultHasOutputResultSet);
                }
            }

            private void DoHasOutputResultSet(StatementList toAnalyze) // try, catch
            {
                StatementVisitor vstor = new StatementVisitor(resolveColumnType, columnTypeResolver, toAnalyze);
                toAnalyze.Accept(vstor);
                int cnt = vstor.resultHasOutputResultSet.Count;
                if (cnt > 0)
                {
                    this.resultHasOutputResultSet.AddRange(vstor.resultHasOutputResultSet);
                }
            }

            public override void ExplicitVisit(UnqualifiedJoin node)
            {
                base.ExplicitVisit(node); // ? CROSS APPLY
            }

            public override void ExplicitVisit(IfStatement node)
            {
                DoHasOutputResultSet(node.ThenStatement);

                if (node.ElseStatement != null)
                {
                    // skip 'else'  but it can be use for column type discovery
                    if (resultHasOutputResultSet.Count == 0)
                    {
                        // no SELECT in ThenStatement list : maybe THROW? or RAISEERROR
                        DoHasOutputResultSet(node.ElseStatement);
                    }
                    else
                    {
                        // apply column types from ELSE
                    }
                }
            }

            public override void ExplicitVisit(TryCatchStatement node)
            {
                DoHasOutputResultSet(node.TryStatements);
                if (node.CatchStatements != null)
                {
                    // skip 'catch' but it can be use for column type discovery
                    if (resultHasOutputResultSet.Count == 0)
                    {
                        // no SELECT in TryStatements list : maybe THROW? or RAISEERROR
                        DoHasOutputResultSet(node.CatchStatements);
                    }
                    else
                    {
                        // apply column types from CATCH
                    }
                }
            }

            public override void ExplicitVisit(SelectStatement node)
            {
                if (node.Into != null)
                    return;

                if (node.QueryExpression is BinaryQueryExpression bqe) // UNION?
                {
                    // premature optimization : without visitor
                    var vstorF = new SelectElementVisitor(resolveColumnType, columnTypeResolver, node);
                    QuerySpecification qspecF = (QuerySpecification)bqe.FirstQueryExpression;
                    foreach (var selectEl in qspecF.SelectElements)
                    {
                        selectEl.Accept(vstorF);
                    }
                    if (vstorF.HasOutput)
                    {
                        if (vstorF.ResultOutput.HasMissingColumnInfo())
                        {
                            var vstorS = new SelectElementVisitor(resolveColumnType, columnTypeResolver, node);
                            QuerySpecification qspecS = (QuerySpecification)bqe.SecondQueryExpression;
                            foreach (var selectEl in qspecS.SelectElements)
                            {
                                selectEl.Accept(vstorS);
                            }

                            vstorF.ResultOutput.ApplyMissingInformation(vstorS.ResultOutput);
                        }

                        resultHasOutputResultSet.Add(vstorF.ResultOutput);
                    }

                }
                else
                {
                    var vstor = new SelectElementVisitor(resolveColumnType, columnTypeResolver, node);
                    QuerySpecification qspec = (QuerySpecification)node.QueryExpression;
                    foreach (var selectEl in qspec.SelectElements)
                    {
                        selectEl.Accept(vstor);
                    }
                    if (vstor.HasOutput)
                    {
                        resultHasOutputResultSet.Add(vstor.ResultOutput);
                    }
                }


            }


            public override void ExplicitVisit(UpdateStatement node)
            {
                if (node.UpdateSpecification != null && node.UpdateSpecification.OutputClause != null)
                {
                    var vstor = new SelectElementVisitor(resolveColumnType, columnTypeResolver, node);
                    node.UpdateSpecification.OutputClause.Accept(vstor);
                    if (vstor.HasOutput)
                    {
                        resultHasOutputResultSet.Add(vstor.ResultOutput);
                    }
                }
            }

            public override void ExplicitVisit(InsertStatement node)
            {
                if (node.InsertSpecification != null && node.InsertSpecification.OutputClause != null)
                {
                    var vstor = new SelectElementVisitor(resolveColumnType, columnTypeResolver, node);
                    node.InsertSpecification.OutputClause.Accept(vstor);
                    if (vstor.HasOutput)
                    {
                        resultHasOutputResultSet.Add(vstor.ResultOutput);
                    }
                }
            }

            public override void ExplicitVisit(DeleteStatement node)
            {
                if (node.DeleteSpecification != null && node.DeleteSpecification.OutputClause != null)
                {
                    var vstor = new SelectElementVisitor(resolveColumnType, columnTypeResolver, node);
                    node.DeleteSpecification.OutputClause.Accept(vstor);
                    if (vstor.HasOutput)
                    {
                        resultHasOutputResultSet.Add(vstor.ResultOutput);
                    }
                }
            }
        }

        class SelectElementVisitor : DumpFragmentVisitor
        {
            private bool hasSetVariable;
            private readonly ProcedureOutputSet outputSet;
            private readonly StatementOutputColumnTypeResolver columnTypeResolver;
            private readonly StatementWithCtesAndXmlNamespaces statement;
            private readonly bool resolveColumnType;

            public SelectElementVisitor(bool resolveColumnType, BatchOutputColumnTypeResolver columnTypeResolver, StatementWithCtesAndXmlNamespaces statement) : base(false)
            {
                this.resolveColumnType = resolveColumnType;
                this.columnTypeResolver = new StatementOutputColumnTypeResolver(columnTypeResolver, statement);
                this.outputSet = new ProcedureOutputSet(statement);
                this.statement = statement;
            }

            internal bool HasOutput
            {
                get
                {
                    if (hasSetVariable)
                    {
                        outputSet.Clear();
                    }

                    return outputSet.ColumnCount > 0;
                }
            }
            internal ProcedureOutputSet ResultOutput
            {
                get
                {
                    if (!HasOutput)
                    {
                        throw new NotSupportedException("No output");
                    }

                    return outputSet;
                }
            }

            public override void ExplicitVisit(SelectSetVariable node)
            {
                // no! do not call the base implementation : stop the visiting of the child fragments!
                hasSetVariable = true; // => no outputs
                outputSet.Clear();
            }

            public override void ExplicitVisit(QualifiedJoin node)
            {

            }

            public override void ExplicitVisit(ColumnReferenceExpression node)
            {
                if (hasSetVariable)
                {
                    outputSet.Clear();
                }
                else
                {
                    OutputColumnDescriptor columnDbType = resolveColumnType
                        ? columnTypeResolver.ResolveColumnReference(node)
                        : null;
                    if (columnDbType == null)
                    {
                        // put breakpoint here and try again
                    }
                    outputSet.AddColumn(new ProcedureOutputColumn(node, columnDbType)); // ColumnReferenceExpression : [a].[attributeid]
                }
            }
            public override void ExplicitVisit(SelectScalarExpression node)
            {
                // IIF(@limitreached = 1, 1, 0)
                if (hasSetVariable)
                {
                    outputSet.Clear();
                }
                else
                {
                    OutputColumnDescriptor columnDbType = resolveColumnType
                        ? columnTypeResolver.ResolveScalarExpression(node)
                        : null;
                    outputSet.AddColumn(new ProcedureOutputColumn(node, columnDbType)); // SelectScalarExpression
                }
            }
            public override void ExplicitVisit(IdentifierOrValueExpression node)
            {
                // column from constant
                if (hasSetVariable)
                {
                    outputSet.Clear();
                }
                else
                {
                    throw new NotSupportedException(node.AsText());
                    //outputSet.resultFragments.Add(node); // IdentifierOrValueExpression
                }
            }
        }

        internal static bool? HasReturnStatement(ProcedureMetadata procedure_metadata)
        {
            ReturnStatementVisitor vstor = new ReturnStatementVisitor();
            procedure_metadata.BodyFragment.Accept(vstor);
            return vstor.result;
        }

        class ReturnStatementVisitor : TSqlFragmentVisitor
        {
            internal bool result;
            public override void ExplicitVisit(ReturnStatement node)
            {
                result = true;
            }
        }
    }
}
