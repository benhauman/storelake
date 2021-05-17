using Microsoft.SqlServer.TransactSql.ScriptDom;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StoreLake.Sdk.SqlDom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StoreLake.Test
{
    [TestClass]
    public class ProcedureGenerator_Tests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void hlbpm_query_cmdbflowattributes()
        {
            string sql = @"BEGIN
	DECLARE @error_text NVARCHAR(MAX);
	DECLARE @table TABLE (
		cmdbflowname NVARCHAR(255) NOT NULL,
		cmdbflowid INT NULL,
		cmdbflowattributeid INT NULL,
		attributeid INT NULL
	)
	INSERT INTO @table (cmdbflowname, cmdbflowid, cmdbflowattributeid, attributeid)
    SELECT S.id, [root].[cmdbflowid], [fa].[attributeid] ,[a].[attrdefid]
	FROM @cmdbflownames AS S
	LEFT JOIN [dbo].[hlspcmdbflow] AS [root] ON S.id = [root].[cmdbflowname]
    LEFT JOIN [dbo].[hlspcmdbflowmodelattribute] AS [fa] ON [root].[cmdbflowid] = [fa].[cmdbflowid]
    LEFT JOIN [dbo].[hlsysattributedef] AS [a] ON [a].[attrdefid] = [fa].[attributeid]
	
	IF EXISTS(SELECT * FROM @table WHERE cmdbflowid IS NULL OR cmdbflowattributeid IS NULL OR attributeid IS NULL)
	BEGIN
		DECLARE @cmdbflowname NVARCHAR(255)
				, @cmdbflowattributeid INT
				, @attributedefid INT
		SELECT TOP 1 @cmdbflowname = cmdbflowname
					, @cmdbflowattributeid = cmdbflowattributeid
					, @attributedefid = attributeid
		FROM @table 
		WHERE cmdbflowid IS NULL OR cmdbflowattributeid IS NULL OR attributeid IS NULL;
		
		SET @error_text = CONCAT(N'CmdbFlowAttributes cannot be queued for cmdbflowname: ', @cmdbflowname 
		, N'. cmdbflowattributeid: ' , CAST(@cmdbflowattributeid AS NVARCHAR(50))
		, N'. attributeid: ' , CAST(@attributedefid AS NVARCHAR(50)));
			THROW 50000, @error_text, 1;
		-- @cmdbflowid is null --> service task internal name (aka cmdbflowname) cannot be resolved to cmdbflowid. 
		--		Check hlbpmprocessversion(mxdefinition, xpdldefinition) for serviceTaskInternalName  hlspcmdbflow
		-- @cmdbflowattributeid is null --> deployment of master data catalog project is not working.
		-- @attributeid is null --> should not happen because of FK. CmdbFlowAttributes are not matching with the hlsysattributedef table.
	END

    SELECT [a].[attributeid] AS AttributeDefId, pile=NULL, pale=112233
	FROM @table AS a
	GROUP BY [a].[attributeid]
END
";
            TSqlFragment sqlF = ScriptDomFacade.Parse(sql);
            var res = SelectVisitor.AnalyzeHasOutputResultSet(sqlF);
            //Assert.IsTrue(vstor.HasSelectStatements(), "HasSelectStatements");
        }

        class SelectVisitor : TSqlFragmentVisitor
        {
            private readonly TSqlFragment _toAnalyze;
            //internal readonly List<SelectVisitor> statements_If = new List<SelectVisitor>();
            //internal readonly List<SelectVisitor> statements_TryCatch = new List<SelectVisitor>();
            //internal readonly List<SelectStatement> statements_Select = new List<SelectStatement>();
            internal bool? resultHasOutputResultSet;
            internal TSqlFragment resultFragment;

            private SelectVisitor(TSqlFragment toAnalyze = null)
            {
                _toAnalyze = toAnalyze;
            }

            public static bool? AnalyzeHasOutputResultSet(TSqlFragment toAnalyze)
            {
                SelectVisitor vstor = new SelectVisitor(toAnalyze);
                toAnalyze.Accept(vstor);
                return vstor.resultHasOutputResultSet;
            }
            private bool? DoHasOutputResultSet(TSqlFragment toAnalyze)
            {
                SelectVisitor vstor = new SelectVisitor(toAnalyze);
                toAnalyze.Accept(vstor);
                if (vstor.resultHasOutputResultSet.GetValueOrDefault())
                {
                    this.resultHasOutputResultSet = true;
                    this.resultFragment = vstor.resultFragment;
                }

                return this.resultHasOutputResultSet;
            }

            private SelectVisitor Sub(TSqlFragment node)
            {
                return new SelectVisitor(node);
            }


            //internal bool HasSelectStatements()
            //{
            //    return resultHasOutputResultSet.GetValueOrDefault();
            //}

            public override void Visit(TSqlFragment node)
            {

                if ((node is TSqlScript)
                    || (node is TSqlBatch)
                    || (node is BeginEndBlockStatement) // CREATE PROCEDURE
                    || (node is StatementList)
                    || (node is DeclareVariableStatement) // skip
                    || (node is DeclareVariableElement) // skip
                    || (node is Identifier) // skip
                    || (node is SqlDataTypeReference) // NVARCHAR(MAX)
                    || (node is SchemaObjectName) // NVARCHAR
                    || (node is MaxLiteral) // MAX
                    || (node is DeclareTableVariableStatement) // DECLARE @table TABLE(
                    || (node is DeclareTableVariableBody) // @table TABLE(cmdbflowname...
                    || (node is TableDefinition) // cmdbflowname NVARCHAR(...
                    || (node is ColumnDefinition) // cmdbflowname NVARCHAR(255) NOT NULL
                    || (node is IntegerLiteral) // 255
                    || (node is NullableConstraintDefinition) // NOT NULL

                    || (node is InsertStatement)
                    || (node is InsertSpecification)
                    || (node is VariableTableReference)
                    || (node is SelectInsertSource) // SELECT S.id, [root].
                    || (node is QuerySpecification)
                    || (node is ColumnReferenceExpression)
                    || (node is MultiPartIdentifier)
                    || (node is FromClause)
                    || (node is QualifiedJoin)
                    || (node is NamedTableReference) // [dbo].[hlspcmdbflow] AS [root]
                    || (node is BooleanComparisonExpression) // S.id = [root].[cmdbflowname]

                    || (node is VariableReference)
                    || (node is IfStatement)
                 || (node is TryCatchStatement)
                 || (node is SelectElement)

                 || (node is ExistsPredicate) // (SELECT * FROM
                 || (node is ScalarSubquery) // (SELECT * FROM @table WHERE cmdbflowid IS NULL OR cmdbflowatt
                 || (node is WhereClause) // WHERE cmdbflowid IS NULL OR cmdbflowattri
                || (node is BooleanBinaryExpression) // cmdbflowid IS NULL OR cmdbflowattributeid IS N
                || (node is BooleanIsNullExpression)  // cmdbflowid IS NULL O

                || (node is SelectStatement) // SELECT TOP 1 @cmdb
                || (node is TopRowFilter) // TOP 1
                || (node is SetVariableStatement) // SET @error_text = CONCAT(N'C
                || (node is FunctionCall) // CONCAT(N'CmdbFlowAttrib
                || (node is StringLiteral) // N'CmdbFlowAtt
                || (node is CastCall) //CAST(@cmdbflowattributeid AS NVARCHAR(50))
                || (node is ThrowStatement) // THROW 50000, @error_text, 1;
                || (node is IdentifierOrValueExpression) // AttributeDefId
                || (node is GroupByClause) // GROUP BY [a].[attributeid]
                || (node is ExpressionGroupingSpecification) // [a].[attributeid]

                 //|| (node is ParenthesisExpression)
                 //|| (node is ColumnReferenceExpression)
                 //|| (node is MultiPartIdentifier)
                 //|| (node is Identifier)
                 //|| (node is IntegerLiteral)
                 //|| (node is BooleanIsNullExpression)
                 //|| (node is StringLiteral)
                 //|| (node is FunctionCall)
                 //|| (node is BinaryLiteral)
                 //|| (node is BooleanNotExpression)
                 //|| (node is BinaryExpression)
                 )
                {
                    // ok
                }
                else
                {
                    throw new NotImplementedException(node.GetType().Name + " =>  " + node.AsText());
                }
                base.Visit(node);
            }

            public override void ExplicitVisit(IfStatement node)
            {
                DoHasOutputResultSet(node.ThenStatement);
                if (node.ElseStatement != null)
                {
                    DoHasOutputResultSet(node.ElseStatement);
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
                QuerySpecification qspec = (QuerySpecification)node.QueryExpression;
                var vstor = new SelectElementVisitor();
                foreach (var se in qspec.SelectElements)
                {
                    if (se is SelectSetVariable setVar)
                    {
                        // premature optimization
                    }
                    else
                    {
                        SelectScalarExpression scalarExpr = (SelectScalarExpression)se;
                        //scalarExpr.ColumnName
                        //scalarExpr.Accept(vstor);
                        //if (vstor.resultHasOutputResultSet.GetValueOrDefault())
                        {
                            resultHasOutputResultSet = true;
                            resultFragment = scalarExpr; //vstor.resultFragment;
                            break;
                        }
                    }
                }
                //DoHasOutputResultSet(node.QueryExpression);
            }


        }

        class SelectElementVisitor : TSqlFragmentVisitor
        {
            internal bool? resultHasOutputResultSet;
            internal TSqlFragment resultFragment;

            public override void ExplicitVisit(SelectSetVariable node)
            {
                // no! do not call the base implementation : stop the visiting of the child fragments!
                resultHasOutputResultSet = false;
            }

            public override void ExplicitVisit(ColumnReferenceExpression node)
            {
                resultHasOutputResultSet = true;
                resultFragment = node;
            }

            public override void ExplicitVisit(IdentifierOrValueExpression node)
            {
                // column from constant
                resultHasOutputResultSet = true;
                resultFragment = node;
            }
        }
    }
}
