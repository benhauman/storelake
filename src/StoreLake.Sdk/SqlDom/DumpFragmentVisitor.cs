using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StoreLake.Sdk.SqlDom
{
    public class DumpFragmentVisitor : TSqlFragmentVisitor
    {
        int fragmentIndex = 0;
        string prefix = "";
        public DumpFragmentVisitor(bool dumpEnabled)
        {
            DumpEnabled = dumpEnabled;
        }
        protected bool DumpEnabled { get; set; }
        public override void Visit(TSqlFragment node)
        {
            fragmentIndex++;
            if (DumpEnabled)
            {
                Console.WriteLine(fragmentIndex + " " + prefix + node.GetType().Name + "  " + node.AsText());
            }
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
            || (node is FullTextPredicate) // CONTAINS()
            || (node is FullTextTableReference) // CONTAINS()
            || (node is SelectFunctionReturnType)) //  SELECT dv.[defaultattrpathid]
            {
                if (node is SelectInsertSource)
                {

                }
                // ok
            }
            else
            {
                //throw new NotImplementedException("Node type:" + node.GetType().Name);
                throw new NotImplementedException("|| (node is " + node.GetType().Name + ") //  " + node.AsText());
            }
            base.Visit(node);
        }
    }
}
