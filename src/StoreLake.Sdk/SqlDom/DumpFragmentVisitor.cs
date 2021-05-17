using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StoreLake.Sdk.SqlDom
{
    public sealed class DumpFragmentVisitor : TSqlFragmentVisitor
    {
        int fragmentIndex = 0;
        string prefix = "";
        public DumpFragmentVisitor()
        {

        }
        public override void Visit(TSqlFragment node)
        {
            fragmentIndex++;
            Console.WriteLine(fragmentIndex + " " + prefix + node.GetType().Name + "  " + node.AsText());
            if ((node is BooleanParenthesisExpression)
             || (node is BooleanBinaryExpression)
             || (node is BooleanComparisonExpression)
             || (node is ParenthesisExpression)
             || (node is ColumnReferenceExpression)
             || (node is MultiPartIdentifier)
             || (node is Identifier)
             || (node is IntegerLiteral)
             || (node is BooleanIsNullExpression)
             || (node is StringLiteral)
             || (node is FunctionCall)
             || (node is BinaryLiteral)
             || (node is BooleanNotExpression)
             || (node is BinaryExpression)
             )
            {
                // ok
            }
            else
            {
                throw new NotImplementedException(node.GetType().Name + "  " + node.AsText());
            }
            base.Visit(node);
        }
    }
}
