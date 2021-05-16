using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.CodeDom;


namespace StoreLake.Sdk.SqlDom
{
    internal static class BooleanExpressionGenerator
    {
        internal static CodeExpression BuildFromCheckConstraintDefinition(string check_name, string check_definition)
        {
            string select_statement = "SELECT colx = IIF((" + check_definition + "), 1, 0)";
            TSqlFragment sqlF = ScriptDomFacade.Parse(select_statement);
            IIFVisitor iif_visitor = new IIFVisitor();
            sqlF.Accept(iif_visitor);
            if (iif_visitor.result == null)
            {
                throw new NotSupportedException("Expression 'IIF' could not be found:" + sqlF.AsText());
            }

            BooleanExpression iif_predicate = iif_visitor.result.Predicate;

            if (iif_visitor != null)
            {
                //return null; // comment out if it needed
            }

            Console.WriteLine("");
            Console.WriteLine("==== " + check_name + " ============");
            Console.WriteLine(ScriptDomFacade.GenerateScript(iif_predicate));
            Console.WriteLine("-------------------------------------------");
            Console.WriteLine("");

            return BooleanExpressionGeneratorVisitor.BuildFromNode(iif_predicate);
        }
        private sealed class IIFVisitor : TSqlFragmentVisitor
        {
            internal IIfCall result;
            public override void ExplicitVisit(IIfCall node)
            {
                result = node;
            }
        }
    }
}
