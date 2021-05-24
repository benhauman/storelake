using Microsoft.SqlServer.TransactSql.ScriptDom;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StoreLake.Sdk.SqlDom;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StoreLake.Test
{
	[TestClass]
	public class ProcedureGenerator_Tests
	{
		public TestContext TestContext { get; set; }

        class TestSchema : ISchemaMetadataProvider
        {
			internal readonly IDictionary<string, IColumnSourceMetadata> sources = new SortedDictionary<string, IColumnSourceMetadata>();
			IColumnSourceMetadata ISchemaMetadataProvider.TryGetColumnSourceMetadata(string schemaName, string objectName)
            {
				string key;
				if (string.IsNullOrEmpty(schemaName) && objectName[0] == '@')
				{
					key = objectName.ToUpperInvariant();
				}
				else
				{
					key = TestSource.CreateKey(schemaName, objectName).ToUpperInvariant();
				}
				if (sources.TryGetValue(key, out IColumnSourceMetadata source))
					return source;
				return null;
            }

			internal TestSchema AddSource(TestSource source)
            {
				string key = ("[" + source.SchemaName + "].[" + source.ObjectName + "]").ToUpperInvariant();
				sources.Add(key, source);
				return this;
			}
		}

		class TestSource : IColumnSourceMetadata
		{
			internal readonly string Key;
			internal readonly string SchemaName;
			internal readonly string ObjectName;
            public TestSource(string schemaName, string objectName)
            {
				Key = CreateKey(schemaName, objectName);
				SchemaName = schemaName;
				ObjectName = objectName;
			}
			public TestSource(string objectName)
			{
				Key = objectName.ToUpperInvariant();
				ObjectName = objectName;
			}

			internal static string CreateKey(string schemaName, string objectName)
            {
				return ("[" + schemaName + "].[" + objectName + "]").ToUpperInvariant();
			}

			internal readonly IDictionary<string, TestColumn> columns = new SortedDictionary<string, TestColumn>();


			DbType? IColumnSourceMetadata.TryGetColumnTypeByName(string columnName)
            {
				string key = columnName.ToUpperInvariant();
				return columns.TryGetValue(key, out TestColumn column)
					? column.ColumnDbType
					: null;
            }

            internal TestSource AddColumn(string name, DbType columnDbType)
            {
				columns.Add(name.ToUpperInvariant(), new TestColumn(name, columnDbType));
				return this;
            }
        }

		class TestColumn
        {
			internal readonly string ColumnName;
			internal readonly DbType ColumnDbType;

			public TestColumn(string columnName, DbType columnDbType)
            {
				ColumnName = columnName;
				ColumnDbType = columnDbType;

			}
        }

		private static TestSchema s_metadata_1 = new TestSchema()
					.AddSource(new TestSource("dbo", "hlcmcontactvw")
						.AddColumn("personid", DbType.Int32)
						.AddColumn("persondefid", DbType.Int32)
						.AddColumn("surname", DbType.String)
						.AddColumn("name", DbType.String)
						.AddColumn("language", DbType.Int32)
						.AddColumn("title", DbType.String)
						.AddColumn("street", DbType.String)
						.AddColumn("city", DbType.String)
						.AddColumn("region", DbType.String)
						.AddColumn("zipcode", DbType.String)
						.AddColumn("country", DbType.String)
						.AddColumn("email", DbType.String)
						.AddColumn("phonenumber", DbType.String)
					)
					.AddSource(new TestSource("dbo", "hlsysactioncontext")
						.AddColumn("actioncontextid", DbType.Int64)
					)
		;

		private static TestSchema CreateTestMetadata()
        {
			// SEQ_hlsysactioncontext_id
			return s_metadata_1;

		}

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
			var procedure_metadata = Sdk.SqlDom.ProcedureGenerator.ParseProcedureBody(TestContext.TestName, sql);
			var schemaMetadata = CreateTestMetadata()
				.AddSource(new TestSource(null, "@table"))
				;
			var res = Sdk.SqlDom.ProcedureGenerator.IsQueryProcedure(schemaMetadata, procedure_metadata);
			//Assert.IsTrue(vstor.HasSelectStatements(), "HasSelectStatements");
		}

		[TestMethod]
		public void hlsys_createactioncontext()
		{
			string sql = @"
BEGIN
	SET NOCOUNT ON;

	DECLARE @actionid BIGINT;
	
	SELECT @actionid = actionid FROM [dbo].[hlsysaction] WITH (NOLOCK) WHERE actionname = @actionname

	IF (@actionid IS NULL)
	BEGIN
		SET @actionid = CAST(HASHBYTES(N'SHA2_512', @actionname) AS BIGINT);
		--DECLARE @action TABLE (actionid BIGINT);

		INSERT INTO [dbo].[hlsysaction] (actionid, actionname)
		--OUTPUT INSERTED.actionid INTO @action
		VALUES (@actionid, @actionname);

		--SELECT @actionid = actionid FROM @action;
	END

	INSERT INTO [dbo].[hlsysactioncontext] ([actioncontextid], [actionid], [parentactioncontextid], [spid], [hostname], [actionagentid], [actionchannel], [actionlcid], [creationtime])
	OUTPUT INSERTED.[actioncontextid]
	SELECT NEXT VALUE FOR [dbo].[SEQ_hlsysactioncontext_id], @actionid, @parentactioncontextid, @@SPID, HOST_NAME(), @actionagentid, @actionchannel, @actionlcid, GETUTCDATE();  
END

-- DROP PROCEDURE [dbo].[hlsys_createactioncontext]
";
			var procedure_metadata = Sdk.SqlDom.ProcedureGenerator.ParseProcedureBody(TestContext.TestName, sql);
			//procedure_metadata.BodyFragment.Accept(new DumpFragmentVisitor());
			var schemaMetadata = CreateTestMetadata();
			var res = Sdk.SqlDom.ProcedureGenerator.IsQueryProcedure(schemaMetadata, procedure_metadata);
			Assert.AreEqual(1, res.Value);
		}

		[TestMethod]
		public void hlcmgetcontact()
		{
			string sql = @"BEGIN
SET NOCOUNT ON;
SELECT 
 personid, persondefid, surname, name, language, title,
 street, city, region, zipcode,country, email, phonenumber
FROM dbo.hlcmcontactvw
WHERE personid=@PersonId AND persondefid=@PersonDefId
END
";
			var procedure_metadata = Sdk.SqlDom.ProcedureGenerator.ParseProcedureBody(TestContext.TestName, sql);
			//procedure_metadata.BodyFragment.Accept(new DumpFragmentVisitor());
			var schemaMetadata = CreateTestMetadata();
			var res = Sdk.SqlDom.ProcedureGenerator.IsQueryProcedure(schemaMetadata, procedure_metadata);
			Assert.AreEqual(1, res.Value);
		}

		
		[TestMethod]
		public void hlsyswatchlist_add()
		{
			string sql = @"
BEGIN
    SET NOCOUNT ON
	
    --DECLARE @agentid INT = 106690
    --DECLARE @ids [dbo].[hlsys_udt_intthreeset]
    --INSERT INTO @ids VALUES (1, 100824, 530510), (2, 101009, 531927), (3, 101009, 592357), (4, 100824, 4179322)
    
    DECLARE @limit INT = 100
    
    -- Determine how many entries to add until the limit is reached for this agent
	DECLARE @count INT = ISNULL((SELECT COUNT(1) FROM [dbo].[hlsyswatchlist] WHERE [agentid] = @agentid GROUP BY [agentid]), 0)
	DECLARE @todo INT = (SELECT COUNT (1) FROM @ids)
    DECLARE @take INT = @limit - @count
	DECLARE @limitreached BIT = IIF((@count + @todo) > @limit, 1, 0)
    
    -- Merge new entries until limit is reached
    INSERT INTO [dbo].[hlsyswatchlist] ([agentid], [defid], [objid], [createdtime])
    SELECT @agentid AS [agentid], [i].[vb] AS [objectdefid], [i].[vc] AS [objectid], GETUTCDATE() AS [createdtime]
    FROM [dbo].[hlsyswatchlist] AS [w]
    RIGHT JOIN @ids AS [i] ON [w].[defid] = [i].[vb] AND [w].[objid] = [i].[vc] AND [w].[agentid] = @agentid
    WHERE [w].[objid] IS NULL
    ORDER BY [i].[va]
    OFFSET 0 ROWS FETCH NEXT @take ROWS ONLY

	SELECT IIF(@limitreached = 1, 1, 0)
END
";
			var procedure_metadata = Sdk.SqlDom.ProcedureGenerator.ParseProcedureBody(TestContext.TestName, sql);
			var schemaMetadata = CreateTestMetadata();
			var res = Sdk.SqlDom.ProcedureGenerator.IsQueryProcedure(schemaMetadata, procedure_metadata);
			Assert.AreEqual(1, res.Value);
		}


		
			[TestMethod]
		public void hlsp_approvalfulfilled()
		{
			string sql = @"BEGIN
	SET NOCOUNT ON

	--DECLARE @processname NVARCHAR(255)	
	DECLARE @taskid UNIQUEIDENTIFIER
	--DECLARE @administrative_approval_already_fullfiled BIT

	IF @approvalid IS NOT NULL
	BEGIN
		SELECT @taskid = [approvementcontext] FROM [dbo].[hlsysapprovements] WHERE [approvementid] = @approvalid
	END
	ELSE
	BEGIN
		SET @taskid = @approvementcontext
	END

	-- if in progress then return fixed row instead of final result
	IF EXISTS (
		SELECT [app].[approvementcontext]
		FROM [dbo].[hlsysapprovementfulfillment] AS [fullfillment]
		INNER JOIN [dbo].[hlsysapprovements] AS [app] ON [fullfillment].[approvementid] = [app].[approvementid]
		WHERE [app].[approvementcontext] = @taskid
		AND [fullfillment].[closereason] >= 2 -- administrative approvement has 2 or 3
		GROUP BY [app].[approvementcontext]
		HAVING MIN([fullfillment].[state]) < 2 AND MAX([fullfillment].[state]) >= 2 -- some rows already closed and some still pending
	)
	BEGIN
		SELECT NULL AS [processname], NULL AS [processinstanceid], NULL AS [taskid], 0 AS [approved]
	END
	ELSE
	BEGIN
		SELECT  [sp_def].[definitionname] + N'_' + [sp_def].[processversion] AS [processname], -- reterive full process name
			[sp_i].[processinstanceid],
			@taskid AS [taskid],
			[fullfillment_count].[approved]
		FROM [dbo].[hlsprequiredapprovalrejection] AS [sp_rar]
		INNER JOIN (
			-- count approved and disapproved votes for given approvment context
			SELECT DISTINCT [app].[workflowinstanceid], [app].[approvementcontext], [fullfillment].[approved], 
				COUNT(*) OVER(PARTITION BY [app].[approvementcontext], [fullfillment].[approved]) AS [count]
			FROM [dbo].[hlsysapprovementfulfillment] AS [fullfillment]
			INNER JOIN [dbo].[hlsysapprovements] AS [app] ON [fullfillment].[approvementid] = [app].[approvementid]
			WHERE [app].[approvementcontext] = @taskid
		) AS [fullfillment_count]
		ON [fullfillment_count].[approvementcontext] = [sp_rar].[approvementcontext]
		INNER JOIN [dbo].[hlpeprocessstate] AS [pe_st]
		ON [pe_st].[taskid] = @taskid
		INNER JOIN [dbo].[hlspinstance] AS [sp_i]
		ON [sp_i].[processinstanceid] = [pe_st].[processinstanceid]
		INNER JOIN [dbo].[hlspdefinition] AS [sp_def] 
		ON [sp_def].[spdefinitionid] = [sp_i].[spdefinitionid]
		-- compare current counts with thresholds from [hlsprequiredapprovalrejection]
		WHERE ([fullfillment_count].[count] >= [sp_rar].[approvals] AND [fullfillment_count].[approved] = 1) 
			OR ([fullfillment_count].[count] >= [sp_rar].[rejections] AND [fullfillment_count].[approved] = 0)
	END
END
";
			var procedure_metadata = Sdk.SqlDom.ProcedureGenerator.ParseProcedureBody(TestContext.TestName, sql);
			var schemaMetadata = CreateTestMetadata();
			var res = Sdk.SqlDom.ProcedureGenerator.IsQueryProcedure(schemaMetadata, procedure_metadata);
			Assert.AreEqual(1, res.Value);
		}
	}
}
