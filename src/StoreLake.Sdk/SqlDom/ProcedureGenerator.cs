using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.Data;
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
        public static int? IsQueryProcedure(ISchemaMetadataProvider schemaMetadata, ProcedureMetadata procedure_metadata)
        {
            BatchOutputColumnTypeResolver columnTypeResolver = new BatchOutputColumnTypeResolver(schemaMetadata, procedure_metadata.BodyFragment);
            StatementVisitor vstor = new StatementVisitor(columnTypeResolver, procedure_metadata.BodyFragment);
            procedure_metadata.BodyFragment.Accept(vstor);
            return vstor.resultHasOutputResultSet.Count;
        }

        class OutputSet
        {
            private readonly TSqlFragment initiator;
            public OutputSet(StatementWithCtesAndXmlNamespaces initiator)
            {
                this.initiator = initiator;
            }
            internal readonly List<TSqlFragment> resultFragments = new List<TSqlFragment>();
        }

        class StatementVisitor : DumpFragmentVisitor
        {
            private readonly TSqlFragment _toAnalyze;
            private readonly BatchOutputColumnTypeResolver columnTypeResolver;

            internal readonly List<OutputSet> resultHasOutputResultSet = new List<OutputSet>();

            internal StatementVisitor(BatchOutputColumnTypeResolver columnTypeResolver, TSqlFragment toAnalyze) : base(false)
            {
                this.columnTypeResolver = columnTypeResolver;
                _toAnalyze = toAnalyze;
            }

            private void DoHasOutputResultSet(TSqlStatement toAnalyze)
            {
                StatementVisitor vstor = new StatementVisitor(columnTypeResolver, toAnalyze);
                toAnalyze.Accept(vstor);
                int cnt = vstor.resultHasOutputResultSet.Count;
                if (cnt > 0)
                {
                    this.resultHasOutputResultSet.AddRange(vstor.resultHasOutputResultSet);
                }
            }

            private void DoHasOutputResultSet(StatementList toAnalyze) // try, catch
            {
                StatementVisitor vstor = new StatementVisitor(columnTypeResolver, toAnalyze);
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
                }
            }

            public override void ExplicitVisit(SelectStatement node)
            {
                if (node.Into != null)
                    return;

                QuerySpecification qspec;
                if (node.QueryExpression is BinaryQueryExpression bqe) // UNION?
                {
                    // premature optimization : without visitor
                    qspec = (QuerySpecification)bqe.FirstQueryExpression;
                }
                else
                {
                    qspec = (QuerySpecification)node.QueryExpression;
                }
                var vstor = new SelectElementVisitor(columnTypeResolver, node);
                qspec.Accept(vstor);
                if (vstor.HasOutput)
                {
                    resultHasOutputResultSet.Add(new OutputSet(node));
                }
            }

            public override void ExplicitVisit(UpdateStatement node)
            {
                if (node.UpdateSpecification != null && node.UpdateSpecification.OutputClause != null)
                {
                    var vstor = new SelectElementVisitor(columnTypeResolver, node);
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
                    var vstor = new SelectElementVisitor(columnTypeResolver, node);
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
                    var vstor = new SelectElementVisitor(columnTypeResolver, node);
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
            private readonly OutputSet outputSet;
            private readonly StatementOutputColumnTypeResolver columnTypeResolver;
            private readonly StatementWithCtesAndXmlNamespaces statement;

            public SelectElementVisitor(BatchOutputColumnTypeResolver columnTypeResolver, StatementWithCtesAndXmlNamespaces statement) : base(false)
            {
                this.columnTypeResolver = new StatementOutputColumnTypeResolver(columnTypeResolver, statement);
                this.outputSet = new OutputSet(statement);
                this.statement = statement;
            }

            internal bool HasOutput
            {
                get
                {
                    if (hasSetVariable)
                    {
                        outputSet.resultFragments.Clear();
                    }

                    return outputSet.resultFragments.Count > 0;
                }
            }
            internal OutputSet ResultOutput
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
                outputSet.resultFragments.Clear();
            }

            public override void ExplicitVisit(ColumnReferenceExpression node)
            {
                if (hasSetVariable)
                {
                    outputSet.resultFragments.Clear();
                }
                else
                {
                    //System.Data.DbType? columnDbType = columnTypeResolver.ResolveColumnReference(node);
                    outputSet.resultFragments.Add(node); // ColumnReferenceExpression : [a].[attributeid]
                }
            }
            public override void ExplicitVisit(SelectScalarExpression node)
            {
                // IIF(@limitreached = 1, 1, 0)
                if (hasSetVariable)
                {
                    outputSet.resultFragments.Clear();
                }
                else
                {
                    //System.Data.DbType? columnDbType = columnTypeResolver.ResolveScalarExpression(node);
                    outputSet.resultFragments.Add(node); // SelectScalarExpression
                }
            }
            public override void ExplicitVisit(IdentifierOrValueExpression node)
            {
                // column from constant
                if (hasSetVariable)
                {
                    outputSet.resultFragments.Clear();
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

        internal static int? IsQueryProcedure(object schemaMetadata, ProcedureMetadata procedure_metadata)
        {
            throw new NotImplementedException();
        }
    }
}
