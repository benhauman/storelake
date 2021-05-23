using Microsoft.SqlServer.TransactSql.ScriptDom;
using StoreLake.Sdk.CodeGeneration;
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
            public OutputSet(TSqlFragment resultFragment)
            {
                initiator = resultFragment;
            }
        }

        class SelectVisitor : DumpFragmentVisitor
        {
            private readonly TSqlFragment _toAnalyze;

            internal int? resultHasOutputResultSet;
            internal TSqlFragment resultFragment;

            private SelectVisitor(TSqlFragment toAnalyze = null) : base(false)
            {
                _toAnalyze = toAnalyze;
            }

            public static int? AnalyzeHasOutputResultSet(TSqlFragment toAnalyze)
            {
                SelectVisitor vstor = new SelectVisitor(toAnalyze);
                toAnalyze.Accept(vstor);
                return vstor.resultHasOutputResultSet;
            }
            private void DoHasOutputResultSet(TSqlFragment toAnalyze)
            {
                SelectVisitor vstor = new SelectVisitor(toAnalyze);
                toAnalyze.Accept(vstor);
                int cnt = vstor.resultHasOutputResultSet.GetValueOrDefault();
                if (cnt > 0)
                {
                    this.resultHasOutputResultSet = cnt + this.resultHasOutputResultSet.GetValueOrDefault();
                    this.resultFragment = vstor.resultFragment;
                }

                //return this.resultHasOutputResultSet;
            }
            private void AddOutput(TSqlFragment node)
            {
                resultHasOutputResultSet = 1 + resultHasOutputResultSet.GetValueOrDefault();
                resultFragment = node; //vstor.resultFragment;

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
                    if (resultHasOutputResultSet.GetValueOrDefault() == 0)
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
                    AddOutput(qspec);
                }
                
                //foreach (var se in qspec.SelectElements)
                //{
                //    if (se is SelectSetVariable setVar)
                //    {
                //        // premature optimization : without visitor
                //    }
                //    else
                //    {
                //        SelectScalarExpression scalarExpr = (SelectScalarExpression)se;
                //        resultHasOutputResultSet = 1 + resultHasOutputResultSet.GetValueOrDefault();
                //        resultFragment = scalarExpr; //vstor.resultFragment;
                            
                //        if (vstor.resultFragments.Count == 0)
                //        {
                //            throw new NotImplementedException();
                //        }
                //    }
                //}
            }

            public override void ExplicitVisit(InsertStatement node)
            {
                var vstor = new InsertSpecificationVisitor();
                node.InsertSpecification.Accept(vstor);
                if (vstor.hasOutputColumns.GetValueOrDefault())
                {
                    AddOutput(node);
                }
            }




        }

        class InsertSpecificationVisitor : TSqlFragmentVisitor
        {
            internal bool? hasOutputColumns;
            public override void ExplicitVisit(OutputClause node)
            {
                hasOutputColumns = true;
                base.ExplicitVisit(node);
            }
        }

        class SelectElementVisitor : DumpFragmentVisitor
        {
            //internal bool? resultHasOutputResultSet;
            //internal TSqlFragment resultFragment;
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
                //resultHasOutputResultSet = false;
                hasSetVariable = true; // => no outputs
                resultFragments.Clear();
            }

            public override void ExplicitVisit(ColumnReferenceExpression node)
            {
                //resultHasOutputResultSet = true;
                //resultFragment = node;
                if (hasSetVariable)
                {
                    resultFragments.Clear();
                }
                else
                {
                    resultFragments.Add(node);
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
                    resultFragments.Add(node);
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
                    resultFragments.Add(node);
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
