namespace StoreLake.Sdk.SqlDom
{
    using System;
    using Microsoft.SqlServer.TransactSql.ScriptDom;
    using cs = System.CodeDom;

    internal sealed class BooleanExpressionGeneratorVisitor : TSqlFragmentVisitor
    {
        private readonly IDatabaseMetadataProvider databaseMetadata;
        private readonly TSqlFragment source;
        private cs.CodeExpression lastExpression;
        private string lastError;
        private bool lastHasError;

        public BooleanExpressionGeneratorVisitor(IDatabaseMetadataProvider databaseMetadata, TSqlFragment source)
        {
            this.databaseMetadata = databaseMetadata;
            this.source = source;
        }

        internal bool HasError()
        {
            return lastExpression == null;
        }

        private cs.CodeExpression BuildResult()
        {
            if (lastExpression == null)
            {
                throw new NotImplementedException("NoResult : " + source.AsText());
            }
            return lastExpression;
        }

        internal cs.CodeExpression TryBuildFromNode(TSqlFragment node, ref bool hasError, ref string error)
        {
            return TryBuildFromFragment(databaseMetadata, node, ref hasError, ref error);
        }
        internal static cs.CodeExpression TryBuildFromFragment(IDatabaseMetadataProvider databaseMetadata, TSqlFragment node, ref bool hasError, ref string error)
        {
            var vstor = new BooleanExpressionGeneratorVisitor(databaseMetadata, node);
            node.Accept(vstor);
            if (vstor.HasError())
            {
                //Console.WriteLine(vstor.lastError);
                hasError = true;
                error = vstor.lastError;
                return null;
            }
            //hasError = false;
            //error = null;
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
            if (comparisonType == BooleanComparisonType.NotEqualToExclamation)
                return cs.CodeBinaryOperatorType.IdentityInequality;
            throw new NotImplementedException("" + comparisonType);
        }

        public override void ExplicitVisit(BooleanBinaryExpression node)
        {
            // BinaryExpressionType + First + Second
            cs.CodeBinaryOperatorExpression binary = new cs.CodeBinaryOperatorExpression();
            binary.Operator = ConvertToBinaryOperatorType(node.BinaryExpressionType);
            binary.Left = TryBuildFromNode(node.FirstExpression, ref lastHasError, ref lastError);
            binary.Right = TryBuildFromNode(node.SecondExpression, ref lastHasError, ref lastError);

            if (lastHasError)
            {
            }
            else
            {
                this.lastExpression = binary;
            }
        }

        public override void ExplicitVisit(BooleanComparisonExpression node)
        {
            // ComparisonType + First + Second
            cs.CodeBinaryOperatorExpression binary = new cs.CodeBinaryOperatorExpression();
            binary.Operator = ConvertToBinaryOperatorType(node.ComparisonType);
            binary.Left = TryBuildFromNode(node.FirstExpression, ref lastHasError, ref lastError);
            binary.Right = TryBuildFromNode(node.SecondExpression, ref lastHasError, ref lastError);

            if (lastHasError)
            {
                return;
            }

            // adjust left or right : boolean column - compare to int : true==1 
            // try get left type
            // try  get right type

            this.lastExpression = binary;
        }
        public override void ExplicitVisit(BooleanIsNullExpression node)
        {
            cs.CodeBinaryOperatorExpression binary = new cs.CodeBinaryOperatorExpression();
            binary.Operator = node.IsNot
                ? cs.CodeBinaryOperatorType.IdentityInequality
                : cs.CodeBinaryOperatorType.ValueEquality;
            binary.Left = TryBuildFromNode(node.Expression, ref lastHasError, ref lastError);
            binary.Right = new cs.CodePrimitiveExpression(null);

            if (lastHasError) { }
            else
            {

                this.lastExpression = binary;
            }
        }

        public override void ExplicitVisit(BooleanParenthesisExpression node)
        {

            node.Expression.Accept(this);
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

            this.lastExpression = TryBuildFromNode(node.Expression, ref lastHasError, ref lastError);
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
            /*if (string.Equals(node.FunctionName.Value, "ISNULL", StringComparison.OrdinalIgnoreCase))
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
            else if (string.Equals(node.FunctionName.Value, "SUBSTRING", StringComparison.OrdinalIgnoreCase))
            {
                ExplicitVisit_FunctionCall_SUBSTRING(node);
            }
            else if (string.Equals(node.FunctionName.Value, "PARSENAME", StringComparison.OrdinalIgnoreCase))
            {
                ExplicitVisit_FunctionCall_PARSENAME(node);
            }
            else if (string.Equals(node.FunctionName.Value, "CONVERT", StringComparison.OrdinalIgnoreCase))
            {
                ExplicitVisit_FunctionCall_CONVERT(node);
            }
            else if (string.Equals(node.FunctionName.Value, "REPLICATE", StringComparison.OrdinalIgnoreCase))
            {
                ExplicitVisit_FunctionCall_REPLICATE(node);
            }
            else if (string.Equals(node.FunctionName.Value, "DATALENGTH", StringComparison.OrdinalIgnoreCase))
            {
                ExplicitVisit_FunctionCall_DATALENGTH(node);
            }
            else if (string.Equals(node.FunctionName.Value, "EOMONTH", StringComparison.OrdinalIgnoreCase))
            {
                ExplicitVisit_FunctionCall_EOMONTH(node);
            }
            else if (string.Equals(node.FunctionName.Value, "DATEDIFF", StringComparison.OrdinalIgnoreCase))
            {
                ExplicitVisit_FunctionCall_DATEDIFF(node);
            }
            else if (string.Equals(node.FunctionName.Value, "CHARINDEX", StringComparison.OrdinalIgnoreCase))
            {
                ExplicitVisit_FunctionCall_CHARINDEX(node);
            }
            else if (string.Equals(node.FunctionName.Value, "COL_NAME", StringComparison.OrdinalIgnoreCase))
            {
                ExplicitVisit_FunctionCall_COL_NAME(node);
            }
            else if (string.Equals(node.FunctionName.Value, "OBJECT_NAME", StringComparison.OrdinalIgnoreCase))
            {
                ExplicitVisit_FunctionCall_OBJECT_NAME(node);
            }
            else if (string.Equals(node.FunctionName.Value, "CONCAT", StringComparison.OrdinalIgnoreCase))
            {
                ExplicitVisit_FunctionCall_CONCAT(node);
            }
            else if (string.Equals(node.FunctionName.Value, "DATEFROMPARTS", StringComparison.OrdinalIgnoreCase))
            {
                ExplicitVisit_FunctionCall_DATEFROMPARTS(node);
            }
            else*/
            {
                lastExpression = null;
                lastError = "FunctionCall:" + node.FunctionName.Value;
                //throw new NotImplementedException(node.AsText());
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

            var prm_expression = TryBuildFromNode(node.Parameters[0], ref lastHasError, ref lastError);
            if (!lastHasError)
                invoke_ISNULL.Parameters.Add(prm_expression);
            var prm_replacement_value = TryBuildFromNode(node.Parameters[1], ref lastHasError, ref lastError);
            if (!lastHasError)
                invoke_ISNULL.Parameters.Add(prm_replacement_value);

            if (!lastHasError)
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

            var prm_interval = TryBuildFromNode(node.Parameters[0], ref lastHasError, ref lastError);
            if (!lastHasError)
                invoke_ISNULL.Parameters.Add(prm_interval);
            var prm_datetimeoffset = TryBuildFromNode(node.Parameters[1], ref lastHasError, ref lastError);

            if (!lastHasError)
                invoke_ISNULL.Parameters.Add(prm_datetimeoffset);

            if (lastHasError)
            {
            }
            else
            {
                lastExpression = invoke_ISNULL;
            }
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

            var prm_expression = TryBuildFromNode(node.Parameters[0], ref lastHasError, ref lastError);
            if (!lastHasError)
                invoke_ISNULL.Parameters.Add(prm_expression);

            if (!lastHasError)
                lastExpression = invoke_ISNULL;
        }

        private void ExplicitVisit_FunctionCall_DATALENGTH(FunctionCall node)
        {
            if (node.Parameters.Count != 1)
            {
                throw new NotSupportedException(node.AsText());
            }

            var invoke_ISNULL = new cs.CodeMethodInvokeExpression(new cs.CodeMethodReferenceExpression()
            {
                MethodName = "DATALENGTH",
                TargetObject = new cs.CodeTypeReferenceExpression(new cs.CodeTypeReference("KnownSqlFunction"))
            });

            var prm_expression = TryBuildFromNode(node.Parameters[0], ref lastHasError, ref lastError);
            if (!lastHasError)
                invoke_ISNULL.Parameters.Add(prm_expression);

            if (!lastHasError)
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
                MethodName = "REPLACE",
                TargetObject = new cs.CodeTypeReferenceExpression(new cs.CodeTypeReference("KnownSqlFunction"))
            });

            var prm_1 = TryBuildFromNode(node.Parameters[0], ref lastHasError, ref lastError);
            var prm_2 = TryBuildFromNode(node.Parameters[1], ref lastHasError, ref lastError);
            var prm_3 = TryBuildFromNode(node.Parameters[2], ref lastHasError, ref lastError);
            if (!lastHasError)
                invoke_ISNULL.Parameters.Add(prm_1);
            if (!lastHasError)
                invoke_ISNULL.Parameters.Add(prm_2);
            if (!lastHasError)
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

            var prm_expression = TryBuildFromNode(node.Parameters[0], ref lastHasError, ref lastError);
            if (!lastHasError)
            {

                invoke_ISNULL.Parameters.Add(prm_expression);

                lastExpression = invoke_ISNULL;
            }
        }

        private void ExplicitVisit_FunctionCall_SUBSTRING(FunctionCall node)
        {
            if (node.Parameters.Count != 3)
            {
                throw new NotSupportedException(node.AsText());
            }

            var invoke_ISNULL = new cs.CodeMethodInvokeExpression(new cs.CodeMethodReferenceExpression()
            {
                MethodName = "SUBSTRING",
                TargetObject = new cs.CodeTypeReferenceExpression(new cs.CodeTypeReference("KnownSqlFunction"))
            });

            var prm_expression = TryBuildFromNode(node.Parameters[0], ref lastHasError, ref lastError);
            var prm_starting_position = TryBuildFromNode(node.Parameters[1], ref lastHasError, ref lastError);
            var prm_length = TryBuildFromNode(node.Parameters[2], ref lastHasError, ref lastError);
            if (!lastHasError)
                invoke_ISNULL.Parameters.Add(prm_expression);
            if (!lastHasError)
                invoke_ISNULL.Parameters.Add(prm_starting_position);
            if (!lastHasError)
                invoke_ISNULL.Parameters.Add(prm_length);

            lastExpression = invoke_ISNULL;
        }

        private void ExplicitVisit_FunctionCall_PARSENAME(FunctionCall node)
        {
            if (node.Parameters.Count != 2)
            {
                throw new NotSupportedException(node.AsText());
            }

            var invoke_ISNULL = new cs.CodeMethodInvokeExpression(new cs.CodeMethodReferenceExpression()
            {
                MethodName = "PARSENAME",
                TargetObject = new cs.CodeTypeReferenceExpression(new cs.CodeTypeReference("KnownSqlFunction"))
            });

            var prm_object_name = TryBuildFromNode(node.Parameters[0], ref lastHasError, ref lastError);
            var prm_object_part = TryBuildFromNode(node.Parameters[1], ref lastHasError, ref lastError);
            if (!lastHasError)
                invoke_ISNULL.Parameters.Add(prm_object_name);
            if (!lastHasError)
                invoke_ISNULL.Parameters.Add(prm_object_part);
            if (!lastHasError)
                lastExpression = invoke_ISNULL;
        }

        private void ExplicitVisit_FunctionCall_CONVERT(FunctionCall node)
        {
            if (node.Parameters.Count != 2)
            {
                throw new NotSupportedException(node.AsText());
            }

            var invoke_ISNULL = new cs.CodeMethodInvokeExpression(new cs.CodeMethodReferenceExpression()
            {
                MethodName = "CONVERT",
                TargetObject = new cs.CodeTypeReferenceExpression(new cs.CodeTypeReference("KnownSqlFunction"))
            });

            var prm_object_name = TryBuildFromNode(node.Parameters[0], ref lastHasError, ref lastError);
            var prm_object_part = TryBuildFromNode(node.Parameters[1], ref lastHasError, ref lastError);
            if (!lastHasError)
            {
                invoke_ISNULL.Parameters.Add(prm_object_name);
                invoke_ISNULL.Parameters.Add(prm_object_part);

                lastExpression = invoke_ISNULL;
            }
        }

        private void ExplicitVisit_FunctionCall_REPLICATE(FunctionCall node)
        {
            if (node.Parameters.Count != 2)
            {
                throw new NotSupportedException(node.AsText());
            }

            var invoke_ISNULL = new cs.CodeMethodInvokeExpression(new cs.CodeMethodReferenceExpression()
            {
                MethodName = "REPLICATE",
                TargetObject = new cs.CodeTypeReferenceExpression(new cs.CodeTypeReference("KnownSqlFunction"))
            });

            var prm_string_expression = TryBuildFromNode(node.Parameters[0], ref lastHasError, ref lastError);
            var prm_int_expression = TryBuildFromNode(node.Parameters[1], ref lastHasError, ref lastError);
            if (!lastHasError)
            {
                invoke_ISNULL.Parameters.Add(prm_string_expression);
                invoke_ISNULL.Parameters.Add(prm_int_expression);

                lastExpression = invoke_ISNULL;
            }
        }

        private void ExplicitVisit_FunctionCall_EOMONTH(FunctionCall node)
        {
            if (node.Parameters.Count != 1)
            {
                throw new NotSupportedException(node.AsText());
            }

            var invoke_ISNULL = new cs.CodeMethodInvokeExpression(new cs.CodeMethodReferenceExpression()
            {
                MethodName = "EOMONTH",
                TargetObject = new cs.CodeTypeReferenceExpression(new cs.CodeTypeReference("KnownSqlFunction"))
            });

            var prm_1 = TryBuildFromNode(node.Parameters[0], ref lastHasError, ref lastError);
            if (!lastHasError)
            {

                invoke_ISNULL.Parameters.Add(prm_1);

                lastExpression = invoke_ISNULL;
            }
        }

        private void ExplicitVisit_FunctionCall_DATEDIFF(FunctionCall node)
        {
            if (node.Parameters.Count != 3)
            {
                throw new NotSupportedException(node.AsText());
            }

            var invoke_ISNULL = new cs.CodeMethodInvokeExpression(new cs.CodeMethodReferenceExpression()
            {
                MethodName = "DATEDIFF",
                TargetObject = new cs.CodeTypeReferenceExpression(new cs.CodeTypeReference("KnownSqlFunction"))
            });

            var prm_string_expression = TryBuildFromNode(node.Parameters[0], ref lastHasError, ref lastError);
            var prm_int_expression = TryBuildFromNode(node.Parameters[1], ref lastHasError, ref lastError);
            var prm_3 = TryBuildFromNode(node.Parameters[2], ref lastHasError, ref lastError);
            if (!lastHasError)
            {

                invoke_ISNULL.Parameters.Add(prm_string_expression);
                invoke_ISNULL.Parameters.Add(prm_int_expression);
                invoke_ISNULL.Parameters.Add(prm_3);

                lastExpression = invoke_ISNULL;
            }
        }

        private void ExplicitVisit_FunctionCall_CHARINDEX(FunctionCall node)
        {
            if (node.Parameters.Count != 2)
            {
                throw new NotSupportedException(node.AsText());
            }

            var invoke_ISNULL = new cs.CodeMethodInvokeExpression(new cs.CodeMethodReferenceExpression()
            {
                MethodName = "CHARINDEX",
                TargetObject = new cs.CodeTypeReferenceExpression(new cs.CodeTypeReference("KnownSqlFunction"))
            });

            var prm_string_expression = TryBuildFromNode(node.Parameters[0], ref lastHasError, ref lastError);
            var prm_int_expression = TryBuildFromNode(node.Parameters[1], ref lastHasError, ref lastError);
            if (!lastHasError)
            {

                invoke_ISNULL.Parameters.Add(prm_string_expression);
                invoke_ISNULL.Parameters.Add(prm_int_expression);

                lastExpression = invoke_ISNULL;
            }
        }

        private void ExplicitVisit_FunctionCall_COL_NAME(FunctionCall node)
        {
            if (node.Parameters.Count != 2)
            {
                throw new NotSupportedException(node.AsText());
            }

            var invoke_ISNULL = new cs.CodeMethodInvokeExpression(new cs.CodeMethodReferenceExpression()
            {
                MethodName = "COL_NAME",
                TargetObject = new cs.CodeTypeReferenceExpression(new cs.CodeTypeReference("KnownSqlFunction"))
            });

            var prm_string_expression = TryBuildFromNode(node.Parameters[0], ref lastHasError, ref lastError);
            var prm_int_expression = TryBuildFromNode(node.Parameters[1], ref lastHasError, ref lastError);
            if (!lastHasError)
            {

                invoke_ISNULL.Parameters.Add(prm_string_expression);
                invoke_ISNULL.Parameters.Add(prm_int_expression);

                lastExpression = invoke_ISNULL;
            }
        }

        private void ExplicitVisit_FunctionCall_OBJECT_NAME(FunctionCall node)
        {
            if (node.Parameters.Count != 1)
            {
                throw new NotSupportedException(node.AsText());
            }

            var invoke_ISNULL = new cs.CodeMethodInvokeExpression(new cs.CodeMethodReferenceExpression()
            {
                MethodName = "OBJECT_NAME",
                TargetObject = new cs.CodeTypeReferenceExpression(new cs.CodeTypeReference("KnownSqlFunction"))
            });

            var prm_string_expression = TryBuildFromNode(node.Parameters[0], ref lastHasError, ref lastError);
            if (!lastHasError)
                invoke_ISNULL.Parameters.Add(prm_string_expression);

            if (!lastHasError)
                lastExpression = invoke_ISNULL;
        }

        private void ExplicitVisit_FunctionCall_CONCAT(FunctionCall node)
        {
            //if (node.Parameters.Count != 1)
            //{
            //    throw new NotSupportedException(node.AsText());
            //}

            var invoke_ISNULL = new cs.CodeMethodInvokeExpression(new cs.CodeMethodReferenceExpression()
            {
                MethodName = "CONCAT",
                TargetObject = new cs.CodeTypeReferenceExpression(new cs.CodeTypeReference("KnownSqlFunction"))
            });

            for (int idx = 0; idx < node.Parameters.Count; idx++)
            {
                var prm = TryBuildFromNode(node.Parameters[idx], ref lastHasError, ref lastError);
                if (!lastHasError)
                {
                    invoke_ISNULL.Parameters.Add(prm);
                }
            }
            if (!lastHasError)
            {
                lastExpression = invoke_ISNULL;
            }
        }

        private void ExplicitVisit_FunctionCall_DATEFROMPARTS(FunctionCall node)
        {
            if (node.Parameters.Count != 3)
            {
                throw new NotSupportedException(node.AsText());
            }

            var invoke_ISNULL = new cs.CodeMethodInvokeExpression(new cs.CodeMethodReferenceExpression()
            {
                MethodName = "DATEFROMPARTS",
                TargetObject = new cs.CodeTypeReferenceExpression(new cs.CodeTypeReference("KnownSqlFunction"))
            });

            var prm_1 = TryBuildFromNode(node.Parameters[0], ref lastHasError, ref lastError);
            var prm_2 = TryBuildFromNode(node.Parameters[1], ref lastHasError, ref lastError);
            var prm_3 = TryBuildFromNode(node.Parameters[2], ref lastHasError, ref lastError);
            if (!lastHasError)
            {

                invoke_ISNULL.Parameters.Add(prm_1);
                invoke_ISNULL.Parameters.Add(prm_2);
                invoke_ISNULL.Parameters.Add(prm_3);

                lastExpression = invoke_ISNULL;
            }
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
            var binaryOp = new cs.CodeBinaryOperatorExpression()
            {
                Operator = cs.CodeBinaryOperatorType.IdentityInequality,
                Left = TryBuildFromNode(node.Expression, ref lastHasError, ref lastError),
                Right = new cs.CodePrimitiveExpression(true)
            };
            if (!lastHasError)
                lastExpression = binaryOp;
        }
    }
}