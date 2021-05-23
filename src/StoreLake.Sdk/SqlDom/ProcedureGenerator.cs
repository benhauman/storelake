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
        public static bool? IsQueryProcedure(ProcedureMetadata procedure_metadata)
        {
            
            bool? res = SelectVisitor.AnalyzeHasOutputResultSet(procedure_metadata.BodyFragment);
            return res.GetValueOrDefault();
        }


        class SelectVisitor : TSqlFragmentVisitor
        {
            private readonly TSqlFragment _toAnalyze;
            //internal readonly List<SelectVisitor> statements_If = new List<SelectVisitor>();
            //internal readonly List<SelectVisitor> statements_TryCatch = new List<SelectVisitor>();
            //internal readonly List<SelectStatement> statements_Select = new List<SelectStatement>();
            internal int? resultHasOutputResultSet;
            internal TSqlFragment resultFragment;

            private SelectVisitor(TSqlFragment toAnalyze = null)
            {
                _toAnalyze = toAnalyze;
            }

            public static bool? AnalyzeHasOutputResultSet(TSqlFragment toAnalyze)
            {
                SelectVisitor vstor = new SelectVisitor(toAnalyze);
                toAnalyze.Accept(vstor);
                return vstor.resultHasOutputResultSet.GetValueOrDefault() > 0;
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

                    //|| (node is ColumnReferenceExpression)
                    //|| (node is MultiPartIdentifier)
                    //|| (node is Identifier)
                    //|| (node is IntegerLiteral)
                    //|| (node is BooleanIsNullExpression)
                    //|| (node is StringLiteral)

                    || (node is ValuesInsertSource) //=> VALUES(@parentid, @domain, @href, @rel, @title)
                    || (node is RowValue) //  (@parentid, @domain, @href, @rel, @title)
                    || (node is ReturnStatement) //  RETURN @@ROWCOUNT
                    || (node is GlobalVariableExpression) //  @@ROWCOUNT
                    || (node is DeleteStatement) //  DELETE [dbo].[hlcmhypermedialinks] WHE
                    || (node is DeleteSpecification) //  DELETE [dbo].[hlcmhypermedialinks]
                    || (node is UpdateStatement) //  UPDATE [dbo].[hlcmhypermedialinks]
                    || (node is UpdateSpecification) //  UPDATE [dbo].[hlcmhypermedialinks]
                    || (node is AssignmentSetClause) //  title = @title
                    || (node is PredicateSetStatement) //  SET NOCOUNT ON;
                    || (node is UniqueConstraintDefinition) //  PRIMARY KEY ([actionid])
                    || (node is ColumnWithSortOrder) //  [actionid]
                    || (node is SchemaObjectFunctionTableReference) //  [dbo].[hlsyssec_query_agentobjectmsk](@age
                    || (node is IIfCall) //  IIF(@objecttype = 7, 789, @objecttype + 783)
                    || (node is BinaryExpression) //  @objecttype + 783
                    || (node is ParenthesisExpression) //  (@accessmask & 133)
                    || (node is TableHint) //  NOLOCK
                    || (node is BooleanParenthesisExpression) //  (@isportalsupporter = 1 OR @reservedbyme
                    || (node is SimpleCaseExpression) //  CASE [actionid] WHEN 5
                    || (node is SimpleWhenClause) //  WHEN 5 /
                    || (node is OrderByClause) //  ORDER BY [cs].[creationtime]
                    || (node is ExpressionWithSortOrder) //  [cs].[creationtime]
                    || (node is OffsetClause) //  OFFSET     @skip ROWS
                    || (node is UnqualifiedJoin) //  [dbo].[hlsyssuassociation] AS [ac]
                    || (node is UserDataTypeReference) //  SYSNAME
                    || (node is RaiseErrorStatement) //  RAISERROR (@errortext, 16, 2)
                    || (node is MergeStatement) //  MERGE [dbo].[hlspcmdbflow] AS T
                    || (node is MergeSpecification) //  MERGE [dbo].[hlspcmdbflow] AS T
                    || (node is QueryDerivedTable) //  (SELECT @processrootid AS cmdbflowid,
                    || (node is MergeActionClause) //  INSERT (cmdbflowid, cmdbflowname, deploytime)
                    || (node is InsertMergeAction) //  INSERT (cmdbflowid, cmdbflowname, deploytime)
                    || (node is UpdateMergeAction) //  UPDATE SET T.deploytime = GETUTCDATE()
                    || (node is WithCtesAndXmlNamespaces) //  WITH PartialTarget AS
                    || (node is CommonTableExpression) //  PartialTarget AS
                    || (node is DeleteMergeAction) //  DELETE
                    || (node is ExecuteStatement) //  EXEC [dbo].[hlsp_persistattributes_bool]
                    || (node is ExecuteSpecification) //  EXEC [dbo].[hlsp_persistattributes_bool] @
                    || (node is ExecutableProcedureReference) //  [dbo].[hlsp_persistattributes_bool] @
                    || (node is ExecuteParameter) //  @spinstanceid = @spinstanceid
                    || (node is ProcedureReferenceName) //  [dbo].[hlsp_persistattributes_bool]
                    || (node is ProcedureReference) //  [dbo].[hlsp_persistattributes_bool]
                    || (node is BooleanNotExpression) //  NOT ISNULL(S.[attributevalue],N'') = N''
                    || (node is OutputClause) //  OUTPUT INSERTED.[actioncontextid]
                    || (node is NextValueForExpression) //  NEXT VALUE FOR [dbo].[SEQ_h
                    || (node is PrintStatement) //  PRINT(N'do nothing');
                    || (node is SetRowCountStatement) //  SET ROWCOUNT 0
                    || (node is XmlDataTypeReference) //  XML
                    || (node is XmlForClause) //  PATH(N'person'), ELEMENTS
                    || (node is XmlForClauseOption) //  PATH(N'person')
                    || (node is BinaryQueryExpression) //  SELECT CAST(N'A' AS SYSNAME) producer
                    || (node is ConvertCall) //  CONVERT(NVARCHAR(100), DATEDIFF(m
                    || (node is ParameterlessCall) //  CURRENT_TIMESTAMP
                    || (node is OutputIntoClause) //  OUTPUT ISNULL(INSERTED.id, DELETED.ID) INTO
                    || (node is MultiPartIdentifierCallTarget) //  trs
                    || (node is VariableMethodCallTableReference) //  @translationmodel.nodes(N'declare default element nam
                    || (node is OverClause) //  OVER(PARTITION BY tv.tthc, ttlcid ORDER BY LEN(ttvalu
                    || (node is SetCommandStatement) //  SET DEADLOCK_PRIORITY LOW;
                    || (node is GeneralSetCommand) //  LOW
                    || (node is IdentifierLiteral) //  LOW
                    || (node is InPredicate) //  requestid IN (SELECT id FROM @mailstoprocess)
                    || (node is WhileStatement) //  WHILE @rowcount > 0
                    || (node is UnaryExpression) //  -@retentiondays
                    || (node is BeginTransactionStatement) //  BEGIN TRANSACTION;
                    || (node is ExecuteInsertSource) //  EXEC
                    || (node is ExecutableStringList) // N''hlsys_searchdata_targetsvc'' AND[ce].[state] = ''CO''
                    || (node is BeginDialogStatement) //  BEGIN DIALOG CONVERSATION @ch
                    || (node is OnOffDialogOption) //  OFF
                    || (node is SendStatement) //  SEND ON CONVERSATION @ch MESSAG
                    || (node is CommitTransactionStatement) //  COMMIT;
                    || (node is RollbackTransactionStatement) //  ROLLBACK TRANSACTION
                    || (node is NullLiteral) //  NULL
                    || (node is WaitForStatement) //  WAITFOR(
                    || (node is ReceiveStatement) //  RECEIVE TOP (1) @ch = co
                    || (node is InlineDerivedTable) //  (VALUES (@objectdefid, 10, GETUTCDATE(), @a
                    || (node is EndConversationStatement) //  @initiatorconversationhandle
                    || (node is BinaryLiteral) //  0xC0400081
                    || (node is SearchedCaseExpression) //  CASE WHEN @isapproved = 1 THEN 2
                    || (node is SearchedWhenClause) //  WHEN @isapproved = 1 THEN 2
                    || (node is CoalesceExpression) //  COALESCE(tib_fromtime,tia_totime,0)
                    || (node is BooleanTernaryExpression) //  rd.dayno_wrk BETWEEN (@dfd1_dayno_wrk+1
                    || (node is BreakStatement) //  BREAK
                    || (node is ExpressionCallTarget) //  s.v.query (N'Name')
                    || (node is LikePredicate) //  regkeyvaluedata LIKE(N'dword:%')
                    || (node is DefaultConstraintDefinition) //  DEFAULT(0)
                    || (node is CheckConstraintDefinition) //  CHECK([configurationid]=1)
                    || (node is AlterSequenceStatement) //  ALTER SEQUENCE [dbo].[SEQ_hlsysdailycounter] R
                    || (node is ScalarExpressionSequenceOption) //  RESTART WITH 1
                    || (node is SetTransactionIsolationLevelStatement) //  SET TRANSACTION ISOLATION LEVEL
                    || (node is SetIdentityInsertStatement) //  SET IDENTITY_INSERT [dbo].[hlsysde
                    || (node is NullIfExpression) //  NULLIF(@objectid, 0)
                    || (node is XmlNamespaces) //  N'http://tempuri.org/hlsys.xsd' AS pd)
                    || (node is XmlNamespacesAliasElement) //  N'http://tempuri.org/hlsys.xsd' AS pd
                    || (node is AlterTableConstraintModificationStatement) //  ALTER TABLE [dbo].[hlspatt
                    || (node is DbccStatement) //  DBCC CHECKIDENT (N'dbo.hlsysattrpathodedef', RESEED, 1);
                    || (node is DbccNamedLiteral) //  N'dbo.hlsysattrpathodedef'
                    || (node is XmlNamespacesDefaultElement) //  DEFAULT N'pd'
                    || (node is DefaultLiteral) //  DEFAULT
                    || (node is QueryParenthesisExpression) // hltm_getnotificationagentids (SELECT a3.objectida, a3.objectdefida
                 )
                {
                    // ok
                }
                else
                {
                    //throw new NotImplementedException("Node type:" + node.GetType().Name);
                    throw new NotImplementedException("|| (node is " + node.GetType().Name + ") //  " + node.AsText());
                }
                base.Visit(node);
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
                foreach (var se in qspec.SelectElements)
                {
                    if (se is SelectSetVariable setVar)
                    {
                        // premature optimization : without visitor
                    }
                    else
                    {
                        SelectScalarExpression scalarExpr = (SelectScalarExpression)se;
                        //scalarExpr.ColumnName
                        //scalarExpr.Accept(vstor);
                        //if (vstor.resultHasOutputResultSet.GetValueOrDefault())
                        {
                            resultHasOutputResultSet = 1 + resultHasOutputResultSet.GetValueOrDefault();
                            resultFragment = scalarExpr; //vstor.resultFragment;
                            //break;
                        }
                    }
                }

                //DoHasOutputResultSet(node.QueryExpression);
            }

            public override void ExplicitVisit(InsertStatement node)
            {
                var vstor = new InsertSpecificationVisitor();
                node.InsertSpecification.Accept(vstor);
                if (vstor.hasOutputColumns.GetValueOrDefault())
                {
                    resultHasOutputResultSet = 1 + resultHasOutputResultSet.GetValueOrDefault();
                    resultFragment = node; //vstor.resultFragment;
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
