using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using cs = System.CodeDom;

namespace StoreLake.Sdk.SqlDom
{
    internal sealed class BooleanExpressionGeneratorVisitor : TSqlFragmentVisitor
    {
        private readonly TSqlFragment source;
        private cs.CodeExpression lastExpression;
        string prefix;

        public BooleanExpressionGeneratorVisitor(TSqlFragment source, string prefix = "")
        {
            this.source = source;
            this.prefix = prefix;
        }

        private cs.CodeExpression BuildResult()
        {
            if (lastExpression == null)
            {
                throw new NotImplementedException("NoResult : " + source.AsText());
            }
            return lastExpression;
        }

        private IDisposable new_PrefixScope()
        {
            return new PrefixScope(this);
        }

        class PrefixScope : IDisposable
        {
            private string old;
            BooleanExpressionGeneratorVisitor visitor;
            public PrefixScope(BooleanExpressionGeneratorVisitor visitor)
            {
                this.visitor = visitor;
                old = visitor.prefix;
                visitor.prefix = visitor.prefix + "  ";
            }

            public void Dispose()
            {
                visitor.prefix = old;
            }
        }
        internal static cs.CodeExpression BuildFromNode(TSqlFragment node)
        {
            var vstor = new BooleanExpressionGeneratorVisitor(node);
            node.Accept(vstor);
            return vstor.BuildResult();
        }

        private static cs.CodeBinaryOperatorType ConvertToBinaryOperatorType(BooleanBinaryExpressionType binaryExpressionType)
        {
            if (binaryExpressionType == BooleanBinaryExpressionType.Or)
                return cs.CodeBinaryOperatorType.BooleanOr;
            if (binaryExpressionType == BooleanBinaryExpressionType.And)
                return cs.CodeBinaryOperatorType.BooleanAnd;
            throw new NotImplementedException("" + binaryExpressionType);
        }
        private static cs.CodeBinaryOperatorType ConvertToBinaryOperatorType(BooleanComparisonType comparisonType)
        {
            if (comparisonType == BooleanComparisonType.Equals)
                return cs.CodeBinaryOperatorType.ValueEquality;
            if (comparisonType == BooleanComparisonType.NotEqualToBrackets)
                return cs.CodeBinaryOperatorType.IdentityInequality;
            if (comparisonType == BooleanComparisonType.GreaterThan)
                return cs.CodeBinaryOperatorType.GreaterThan;
            if (comparisonType == BooleanComparisonType.GreaterThanOrEqualTo)
                return cs.CodeBinaryOperatorType.GreaterThanOrEqual;
            if (comparisonType == BooleanComparisonType.LessThan)
                return cs.CodeBinaryOperatorType.LessThan;
            if (comparisonType == BooleanComparisonType.LessThanOrEqualTo)
                return cs.CodeBinaryOperatorType.LessThanOrEqual;

            throw new NotImplementedException("" + comparisonType);
        }


        public override void ExplicitVisit(BooleanBinaryExpression node)
        {
            // BinaryExpressionType + First + Second

            cs.CodeBinaryOperatorExpression binary = new cs.CodeBinaryOperatorExpression();
            binary.Operator = ConvertToBinaryOperatorType(node.BinaryExpressionType);
            binary.Left = BuildFromNode(node.FirstExpression);
            binary.Right = BuildFromNode(node.SecondExpression);

            this.lastExpression = binary;
        }

        public override void ExplicitVisit(BooleanComparisonExpression node)
        {
            // ComparisonType + First + Second

            cs.CodeBinaryOperatorExpression binary = new cs.CodeBinaryOperatorExpression();
            binary.Operator = ConvertToBinaryOperatorType(node.ComparisonType);
            binary.Left = BuildFromNode(node.FirstExpression);
            binary.Right = BuildFromNode(node.SecondExpression);

            this.lastExpression = binary;
        }
        public override void ExplicitVisit(BooleanIsNullExpression node)
        {
            cs.CodeBinaryOperatorExpression binary = new cs.CodeBinaryOperatorExpression();
            binary.Operator = node.IsNot
                ? cs.CodeBinaryOperatorType.IdentityInequality
                : cs.CodeBinaryOperatorType.ValueEquality;
            binary.Left = BuildFromNode(node.Expression);
            binary.Right = new cs.CodePrimitiveExpression(null);

            this.lastExpression = binary;
        }

        public override void ExplicitVisit(BooleanParenthesisExpression node)
        {
            Console.WriteLine(prefix + "(");

            using (new_PrefixScope())
            {
                node.Expression.Accept(this);
            }

            Console.WriteLine(prefix + ")");
        }

        public override void ExplicitVisit(ParenthesisExpression node)
        {
            //Console.WriteLine(prefix + "(");
            //
            //using (new_PrefixScope())
            //{
            //    // the expression can result into: nothing, expression, literal or column
            //    //ParenthesisExpressionVisitor vstor = new ParenthesisExpressionVisitor();
            //    node.Expression.Accept(this);
            //    //if (vstor.result == null)
            //    //{
            //    //    throw new NotImplementedException(node.AsText());
            //    //}
            //}
            //
            //Console.WriteLine(prefix + ")");

            this.lastExpression = BuildFromNode(node.Expression);
        }

        public override void ExplicitVisit(ColumnReferenceExpression node)
        {
            string columnName;
            if (node.MultiPartIdentifier.Count == 1)
            {
                columnName = node.MultiPartIdentifier[0].Value;
            }
            else
            {
                throw new NotImplementedException(node.AsText());
            }

            cs.CodeExpression targetObject = new cs.CodeThisReferenceExpression();

            this.lastExpression = new cs.CodeFieldReferenceExpression(targetObject, columnName);
        }

        public override void ExplicitVisit(IntegerLiteral node)
        {
            int value = int.Parse(node.Value);
            lastExpression = new cs.CodePrimitiveExpression(value);
        }

        public override void ExplicitVisit(StringLiteral node)
        {
            lastExpression = new cs.CodePrimitiveExpression(node.Value);
        }

        public override void ExplicitVisit(FunctionCall node)
        {
            if (string.Equals(node.FunctionName.Value, "ISNULL", StringComparison.OrdinalIgnoreCase))
            {
                ExplicitVisit_FunctionCall_ISNULL(node);
            }
            else if (string.Equals(node.FunctionName.Value, "DATEPART", StringComparison.OrdinalIgnoreCase))
            {
                ExplicitVisit_FunctionCall_DATEPART(node);
            }
            else if (string.Equals(node.FunctionName.Value, "LEN", StringComparison.OrdinalIgnoreCase))
            {
                ExplicitVisit_FunctionCall_LEN(node);
            }
            else if (string.Equals(node.FunctionName.Value, "REPLACE", StringComparison.OrdinalIgnoreCase))
            {
                ExplicitVisit_FunctionCall_REPLACE(node);
            }
            else if (string.Equals(node.FunctionName.Value, "RTRIM", StringComparison.OrdinalIgnoreCase))
            {
                ExplicitVisit_FunctionCall_RTRIM(node);
            }
            else
            {
                throw new NotImplementedException(node.AsText());
            }
        }

        private void ExplicitVisit_FunctionCall_ISNULL(FunctionCall node)
        {
            if (node.Parameters.Count != 2)
            {
                throw new NotSupportedException(node.AsText());
            }

            var invoke_ISNULL = new cs.CodeMethodInvokeExpression(new cs.CodeMethodReferenceExpression()
            {
                MethodName = "ISNULL",
                TargetObject = new cs.CodeTypeReferenceExpression(new cs.CodeTypeReference("KnownSqlFunction"))
            });

            var prm_expression = BooleanExpressionGeneratorVisitor.BuildFromNode(node.Parameters[0]);
            var prm_replacement_value = BooleanExpressionGeneratorVisitor.BuildFromNode(node.Parameters[1]);
            invoke_ISNULL.Parameters.Add(prm_expression);
            invoke_ISNULL.Parameters.Add(prm_replacement_value);

            lastExpression = invoke_ISNULL;
        }

        private void ExplicitVisit_FunctionCall_DATEPART(FunctionCall node)
        {
            if (node.Parameters.Count != 2)
            {
                throw new NotSupportedException(node.AsText());
            }

            var invoke_ISNULL = new cs.CodeMethodInvokeExpression(new cs.CodeMethodReferenceExpression()
            {
                MethodName = "DATEPART",
                TargetObject = new cs.CodeTypeReferenceExpression(new cs.CodeTypeReference("KnownSqlFunction"))
            });

            var prm_interval = BooleanExpressionGeneratorVisitor.BuildFromNode(node.Parameters[0]);
            var prm_datetimeoffset = BooleanExpressionGeneratorVisitor.BuildFromNode(node.Parameters[1]);
            invoke_ISNULL.Parameters.Add(prm_interval);
            invoke_ISNULL.Parameters.Add(prm_datetimeoffset);

            lastExpression = invoke_ISNULL;
        }
        private void ExplicitVisit_FunctionCall_LEN(FunctionCall node)
        {
            if (node.Parameters.Count != 1)
            {
                throw new NotSupportedException(node.AsText());
            }

            var invoke_ISNULL = new cs.CodeMethodInvokeExpression(new cs.CodeMethodReferenceExpression()
            {
                MethodName = "LEN",
                TargetObject = new cs.CodeTypeReferenceExpression(new cs.CodeTypeReference("KnownSqlFunction"))
            });

            var prm_expression = BooleanExpressionGeneratorVisitor.BuildFromNode(node.Parameters[0]);
            invoke_ISNULL.Parameters.Add(prm_expression);

            lastExpression = invoke_ISNULL;
        }

        private void ExplicitVisit_FunctionCall_REPLACE(FunctionCall node)
        {
            if (node.Parameters.Count != 3)
            {
                throw new NotSupportedException(node.AsText());
            }

            var invoke_ISNULL = new cs.CodeMethodInvokeExpression(new cs.CodeMethodReferenceExpression()
            {
                MethodName = "LEN",
                TargetObject = new cs.CodeTypeReferenceExpression(new cs.CodeTypeReference("KnownSqlFunction"))
            });

            var prm_1 = BooleanExpressionGeneratorVisitor.BuildFromNode(node.Parameters[0]);
            var prm_2 = BooleanExpressionGeneratorVisitor.BuildFromNode(node.Parameters[1]);
            var prm_3 = BooleanExpressionGeneratorVisitor.BuildFromNode(node.Parameters[2]);
            invoke_ISNULL.Parameters.Add(prm_1);
            invoke_ISNULL.Parameters.Add(prm_2);
            invoke_ISNULL.Parameters.Add(prm_3);

            lastExpression = invoke_ISNULL;
        }

        private void ExplicitVisit_FunctionCall_RTRIM(FunctionCall node)
        {
            if (node.Parameters.Count != 1)
            {
                throw new NotSupportedException(node.AsText());
            }

            var invoke_ISNULL = new cs.CodeMethodInvokeExpression(new cs.CodeMethodReferenceExpression()
            {
                MethodName = "LEN",
                TargetObject = new cs.CodeTypeReferenceExpression(new cs.CodeTypeReference("KnownSqlFunction"))
            });

            var prm_expression = BooleanExpressionGeneratorVisitor.BuildFromNode(node.Parameters[0]);
            invoke_ISNULL.Parameters.Add(prm_expression);

            lastExpression = invoke_ISNULL;
        }
        public override void ExplicitVisit(BinaryLiteral node)
        {
            if (node.Value.StartsWith("0x") && node.Value.Length <= 10)
            {
                var value = Convert.ToUInt32(node.Value, 16);  //Using ToUInt32 not ToUInt64, as per OP comment
                lastExpression = new cs.CodePrimitiveExpression(value);
            }
            else
            {
                throw new NotImplementedException(node.AsText());
            }
        }

        public override void ExplicitVisit(BooleanNotExpression node)
        {
            lastExpression = new cs.CodeBinaryOperatorExpression()
            {
                Operator = cs.CodeBinaryOperatorType.IdentityInequality,
                Left = BooleanExpressionGeneratorVisitor.BuildFromNode(node.Expression),
                Right = new cs.CodePrimitiveExpression(true)
            };
        }
    }
}