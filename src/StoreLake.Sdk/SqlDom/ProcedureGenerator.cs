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
        public static int? IsQueryProcedure(ProcedureMetadata procedure_metadata)
        {
            StatementVisitor vstor = new StatementVisitor(procedure_metadata.BodyFragment);
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

            internal readonly List<OutputSet> resultHasOutputResultSet = new List<OutputSet>();

            internal StatementVisitor(TSqlFragment toAnalyze = null) : base(false)
            {
                _toAnalyze = toAnalyze;
            }

            private void DoHasOutputResultSet(TSqlStatement toAnalyze)
            {
                StatementVisitor vstor = new StatementVisitor(toAnalyze);
                toAnalyze.Accept(vstor);
                int cnt = vstor.resultHasOutputResultSet.Count;
                if (cnt > 0)
                {
                    this.resultHasOutputResultSet.AddRange(vstor.resultHasOutputResultSet);
                }
            }

            private void DoHasOutputResultSet(StatementList toAnalyze)
            {
                StatementVisitor vstor = new StatementVisitor(toAnalyze);
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
                    // skip else  but it can be use for column type discovery
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
                    DoHasOutputResultSet(node.CatchStatements);
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
                var vstor = new SelectElementVisitor(node);
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
                    var vstor = new SelectElementVisitor(node);
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
                    var vstor = new SelectElementVisitor(node);
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
                    var vstor = new SelectElementVisitor(node);
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

            public SelectElementVisitor(StatementWithCtesAndXmlNamespaces statement) : base(false)
            {
                this.outputSet = new OutputSet(statement);
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
                    outputSet.resultFragments.Add(node); // ColumnReferenceExpression : [a].[attributeid]
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

            public override void ExplicitVisit(SelectScalarExpression node)
            {
                // IIF(@limitreached = 1, 1, 0)
                if (hasSetVariable)
                {
                    outputSet.resultFragments.Clear();
                }
                else
                {
                    outputSet.resultFragments.Add(node); // SelectScalarExpression
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
