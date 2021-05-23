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
            int? res = SelectVisitor.AnalyzeHasOutputResultSet(procedure_metadata.BodyFragment);
            return res;
        }

        class OutputSet
        {
            private readonly TSqlFragment initiator;
            public OutputSet(SelectStatement resultFragment)
            {
                initiator = resultFragment;
            }

            public OutputSet(OutputClause resultFragment)
            {
                initiator = resultFragment;
            }
            
        }

        class SelectVisitor : DumpFragmentVisitor
        {
            private readonly TSqlFragment _toAnalyze;

            internal readonly List<OutputSet> resultHasOutputResultSet = new List<OutputSet>();

            private SelectVisitor(TSqlFragment toAnalyze = null) : base(false)
            {
                _toAnalyze = toAnalyze;
            }

            public static int? AnalyzeHasOutputResultSet(TSqlFragment toAnalyze)
            {
                SelectVisitor vstor = new SelectVisitor(toAnalyze);
                toAnalyze.Accept(vstor);
                return vstor.resultHasOutputResultSet.Count;
            }
            private void DoHasOutputResultSet(TSqlFragment toAnalyze)
            {
                SelectVisitor vstor = new SelectVisitor(toAnalyze);
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
                var vstor = new SelectElementVisitor();
                qspec.Accept(vstor);
                if (vstor.ResultFragments.Count > 0)
                {
                    resultHasOutputResultSet.Add(new OutputSet(node));
                }
            }

            public override void ExplicitVisit(OutputClause node)
            {
                var vstor = new SelectElementVisitor();
                node.Accept(vstor);
                if (vstor.ResultFragments.Count > 0)
                {
                    resultHasOutputResultSet.Add(new OutputSet(node));
                }
            }
        }

        class SelectElementVisitor : DumpFragmentVisitor
        {
            private readonly List<TSqlFragment> resultFragments = new List<TSqlFragment>();
            private bool hasSetVariable;

            public SelectElementVisitor() : base(false)
            {

            }

            internal IList<TSqlFragment> ResultFragments
            {
                get
                {
                    if (hasSetVariable)
                    {
                        resultFragments.Clear();
                    }

                    return resultFragments;
                }
            }

            public override void ExplicitVisit(SelectSetVariable node)
            {
                // no! do not call the base implementation : stop the visiting of the child fragments!
                hasSetVariable = true; // => no outputs
                resultFragments.Clear();
            }

            public override void ExplicitVisit(ColumnReferenceExpression node)
            {
                if (hasSetVariable)
                {
                    resultFragments.Clear();
                }
                else
                {
                    resultFragments.Add(node); // ColumnReferenceExpression : [a].[attributeid]
                }
            }

            public override void ExplicitVisit(IdentifierOrValueExpression node)
            {
                // column from constant
                if (hasSetVariable)
                {
                    resultFragments.Clear();
                }
                else
                {
                    resultFragments.Add(node); // IdentifierOrValueExpression
                }
            }

            public override void ExplicitVisit(SelectScalarExpression node)
            {
                // IIF(@limitreached = 1, 1, 0)
                if (hasSetVariable)
                {
                    resultFragments.Clear();
                }
                else
                {
                    resultFragments.Add(node); // SelectScalarExpression
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
