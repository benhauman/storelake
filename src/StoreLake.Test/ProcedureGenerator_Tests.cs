﻿using Microsoft.SqlServer.TransactSql.ScriptDom;
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

                throw new NotImplementedException(key);
                //return null;

            }

            IColumnSourceMetadata ISchemaMetadataProvider.TryGetFunctionTableMetadata(string schemaName, string objectName)
            {
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
                    .AddSource(new TestSource("dbo", "hlsysagent")
                        .AddColumn("agentid", DbType.Int32)
                        .AddColumn("name", DbType.String)
                        .AddColumn("fullname", DbType.String)
                        .AddColumn("description", DbType.String)
                        .AddColumn("active", DbType.Int16)
                    )
                    .AddSource(new TestSource("dbo", "hlsysrole")
                        .AddColumn("roleid", DbType.Int32)
                        .AddColumn("name", DbType.String)
                        .AddColumn("description", DbType.String)
                    )
                    .AddSource(new TestSource("dbo", "hlsysagenttorole")
                        .AddColumn("agentid", DbType.Int32)
                        .AddColumn("roleid", DbType.Int32)
                        .AddColumn("inherited", DbType.Boolean)
                        .AddColumn("rank", DbType.Byte)
                    )
                    .AddSource(new TestSource("dbo", "hlsysagenttoobject")
                        .AddColumn("agentid", DbType.Int32)
                        .AddColumn("objectid", DbType.Int32)
                        .AddColumn("objectdefid", DbType.Int32)
                    )
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
                    .AddSource(new TestSource("dbo", "hlsysobjectdef")
                        .AddColumn("objectdefid", DbType.Int32)
                        .AddColumn("name", DbType.String)
                        .AddColumn("objecttype", DbType.Int32)
                        .AddColumn("isfrozen", DbType.Boolean)
                    )
                    .AddSource(new TestSource("dbo", "hlsysdisplayname")
                        .AddColumn("reposid", DbType.Int32)
                        .AddColumn("languageid", DbType.Int32)
                        .AddColumn("displayname", DbType.String)
                        .AddColumn("type", DbType.Int16)
                    )
                    .AddSource(new TestSource("dbo", "hlsysadhocprocessdefinition")
                        .AddColumn("id", DbType.Guid)
                        .AddColumn("version", DbType.Int32)
                        .AddColumn("fileversion", DbType.Int32)
                        .AddColumn("name", DbType.String)
                        .AddColumn("creationdate", DbType.DateTime)
                        .AddColumn("lastmodified", DbType.DateTime)
                        .AddColumn("lockedby", DbType.String)
                        .AddColumn("fullassemblyname", DbType.String)
                        .AddColumn("processdefinition", DbType.String)
                        .AddColumn("assemblybytes", DbType.Binary)
                        .AddColumn("status", DbType.Int32)
                        .AddColumn("casedefid", DbType.Int32)
                        .AddColumn("category", DbType.Int32)
                        .AddColumn("description", DbType.String)
                        .AddColumn("usescontractmanagement", DbType.Boolean)
                        .AddColumn("usescontractagreement", DbType.Boolean)
                        .AddColumn("allowdefaultserviceusage", DbType.Boolean)
                        .AddColumn("prioritymatrixpath", DbType.String)
                    )
            .AddSource(new TestSource("dbo", "hlwfworkflowdefinition")
                        .AddColumn("rootworkflowid", DbType.Int32)
                        .AddColumn("version", DbType.Int32)
                        .AddColumn("fileversion", DbType.Int32)
                        .AddColumn("name", DbType.String)
                        .AddColumn("createdat", DbType.DateTime)
                        .AddColumn("lastmodified", DbType.DateTime)
                        .AddColumn("lockedby", DbType.String)
                        .AddColumn("isactive", DbType.Boolean)
                        .AddColumn("fullassemblyname", DbType.String)
                        .AddColumn("workflowinfo", DbType.String)
                        .AddColumn("assemblybytes", DbType.Binary)
                        .AddColumn("hasinstances", DbType.Boolean)
                        .AddColumn("description", DbType.String)
                        .AddColumn("objectdefname", DbType.String)
                        .AddColumn("objectdefid", DbType.Int32)
                        .AddColumn("notifgroupid", DbType.Int16)
                        .AddColumn("allowstartwithoutcontext", DbType.Boolean)
                        .AddColumn("usescontractmanagement", DbType.Boolean)
                        .AddColumn("usescontractagreement", DbType.Boolean)
                        .AddColumn("allowdefaultserviceusage", DbType.Boolean)
                        .AddColumn("prioritymatrixpath", DbType.String)
                        .AddColumn("lastmodifiedby", DbType.Int32)
                    )
                    .AddSource(new TestSource("dbo", "hlwfdisplayname")
                        .AddColumn("rootworkflowid", DbType.Int32)
                        .AddColumn("version", DbType.Int32)
                        .AddColumn("lcid", DbType.Int32)
                        .AddColumn("name", DbType.String)
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
            var res = Sdk.SqlDom.ProcedureGenerator.IsQueryProcedure(true, schemaMetadata, procedure_metadata);
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
            var res = Sdk.SqlDom.ProcedureGenerator.IsQueryProcedure(true, schemaMetadata, procedure_metadata);
            Assert.AreEqual(1, res.Length);
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
            var res = Sdk.SqlDom.ProcedureGenerator.IsQueryProcedure(true, schemaMetadata, procedure_metadata);
            Assert.AreEqual(1, res.Length);
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
            var res = Sdk.SqlDom.ProcedureGenerator.IsQueryProcedure(true, schemaMetadata, procedure_metadata);
            Assert.AreEqual(1, res.Length);
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
            var res = Sdk.SqlDom.ProcedureGenerator.IsQueryProcedure(true, schemaMetadata, procedure_metadata);
            Assert.AreEqual(1, res.Length);
        }


        [TestMethod]
        public void hlcmgetrolemembers()
        {
            string sql = @"BEGIN
SET NOCOUNT ON;
SELECT agent.agentid, agent.name AS agentname ,  COALESCE(NULLIF(agent.fullname,N''), agent.name) AS agentfullname, agent.active AS agentactive,
 role.roleid, role.name AS rolename, personid, persondefid AS persondefinitionid, surname, contact.name, language, title,
 street, city, region, zipcode,country, email, phonenumber
FROM dbo.hlsysagent AS agent  
INNER JOIN dbo.hlsysagenttorole AS agent2role
ON agent.agentid = agent2role.agentid
INNER JOIN dbo.hlsysrole AS role  
ON agent2role.roleid = role.roleid
INNER JOIN dbo.hlsysagenttoobject AS agent2object
ON agent2object.agentid = agent.agentid
INNER JOIN dbo.hlcmcontactvw AS contact
ON personid = agent2object.objectid AND persondefid = agent2object.objectdefid AND contact.agentid = agent2object.agentid
WHERE role.roleid=@RoleId
END";

            var procedure_metadata = Sdk.SqlDom.ProcedureGenerator.ParseProcedureBody(TestContext.TestName, sql);
            var schemaMetadata = CreateTestMetadata();
            var res = Sdk.SqlDom.ProcedureGenerator.IsQueryProcedure(true, schemaMetadata, procedure_metadata);
            Assert.AreEqual(1, res.Length, "OutputSet.Count");

            var outputSet = res[0];
            // use: SELECT * FROM sys.dm_exec_describe_first_result_set('dbo.hlcmgetrolemembers', NULL, 0)
            Assert.AreEqual(19, outputSet.ColumnCount, "ColumnCount");
            for (int ix = 0; ix < outputSet.ColumnCount; ix++)
            {
                var column = outputSet.ColumnAt(ix);
                Assert.IsNotNull(column, "ix:" + ix);

                Assert.IsNotNull(column.ColumnDbType, "(" + ix + ") ");
            }
        }


        [TestMethod]
        public void hlomobjectinfo_query()
        {
            string sql = @"BEGIN
    SET NOCOUNT ON
    
    DECLARE @hasvaliddefaultservice BIT = IIF(EXISTS(SELECT 1 
                                                     FROM [dbo].[hlsysslmservicedefault] AS [sd]
                                                     INNER JOIN [dbo].[hlsysslmservice] AS [s] ON [sd].[defaultserviceid] = [s].[id] AND [s].[lifecyclestate] <> 0), 1, 0)

    -- Ad hoc definitions
    SELECT CASE [o].[objecttype] WHEN 2 THEN 0
                                 WHEN 3 THEN 4
                                 WHEN 4 THEN 2
                                 WHEN 5 THEN 3
                                 WHEN 7 THEN 5
           END AS [processtype] -- Helpline.BusinessLayer.ProcessInformation.Contracts.BaseType
         , [o].[objectdefid] AS [defid]
         , [o].[name] AS [defname]
         , [d].[displayname]
         , IIF(ISNULL([secc].[cancreate], 0) = 1 AND ([o].[objecttype] <> 2 OR [pd].[id] IS NOT NULL), 1, 0) AS [allowcreate] -- If case, an ad hoc process definition must be present
         , ISNULL([secs].[cansearch], 0) AS [allowsearch]
         , ISNULL([ix].[imageindex], -1) AS [imageindex]
         , IIF(ISNULL([secc].[cancreate], 0) = 1 AND ([o].[objecttype] <> 2 OR [pd].[id] IS NOT NULL), 1, 0) AS [canstartfromprocessmenu] -- CanStartFromProcessMenu = AllowCreate
         , NULL AS [version]
         , 1 AS [isactive] /* This would make more sense, but keep it stable to the (bugged) C# implementation: IIF([pd].[status] = 1, 1, 0) AS */
         , NULL AS [cancreate]
         , NULL AS [servicerequired]
         , [pd].[usescontractmanagement]
         , [pd].[usescontractagreement]
         , [pd].[allowdefaultserviceusage]
         , IIF([pd].[prioritymatrixpath] IS NOT NULL, 1, 0) AS [prioritymatrixkind] -- AdHocPriorityMatrixKind => 0 NotUsed | 1 PriorityForTimes
         , [pd].[prioritymatrixpath] AS [prioritymatrixpathpriority]
    FROM [dbo].[hlsysobjectdef] AS [o]
    LEFT JOIN [dbo].[hlsysadhocprocessdefinition] AS [pd] ON [o].[objectdefid] = [pd].[casedefid] AND [pd].[status] = 1
    LEFT JOIN [dbo].[hlsysdisplayname] AS [d] ON [o].[objectdefid] = [d].[reposid] AND [d].[languageid] = @lcid
    OUTER APPLY (
        SELECT [i].[imageindex]
        FROM [dbo].[hlsysobjectdefimage] AS [odi]
        INNER JOIN [dbo].[hlsysimage] AS [i] ON [odi].[imageid] = [i].[imageid] AND [odi].[objectdefid] = [o].[objectdefid]
    ) AS [ix]
    OUTER APPLY [dbo].[hlsyssec_query_agentobjectdefprmcreate](@agentid, [o].[objectdefid], IIF([objecttype] = 7, 789, [objecttype] + 783)) AS [secc]
    OUTER APPLY [dbo].[hlsyssec_query_agentobjectdefprmsearch](@agentid, [o].[objectdefid], IIF([objecttype] = 7, 789, [objecttype] + 783)) AS [secs]
    WHERE [o].[isfrozen] = 0
      AND ([secc].[cancreate] = 1 OR [secs].[cansearch] = 1)
      AND ([pd].[casedefid] IS NULL -- If ad hoc process definition exists, validate it
        OR ( -- UsesContractManagement? If no, check default service (see: ISLMProcessRuntimeService.QueryAdHocDefinitions)
            ([pd].[usescontractmanagement] = 1 OR @hasvaliddefaultservice = 1)
            -- UsesContractAgreement? If yes, check agreement reference (see: ISLMProcessRuntimeService.QueryAdHocDefinitions)
        AND ([pd].[usescontractagreement] = 0
          OR EXISTS(SELECT 1
                    FROM [dbo].[hlsysslmcontractagreement] AS [ca]
                    INNER JOIN [dbo].[hlsysslmagreement] AS [sla] ON [ca].[agreementid] = [sla].[id]
                    INNER JOIN [dbo].[hlsysprocessconfig] AS [pc] ON [sla].[configid] = [pc].[id] AND [pc].[isworkflow] = 0 AND [pc].[adhoc] = [pd].[id] AND [pc].[adhocversion] = [pd].[version]
                    INNER JOIN [dbo].[hlsysslmservicedefault] AS [sd] ON [sla].[serviceid] = [sd].[defaultserviceid]))))
    UNION ALL
    -- Workflow definitions
    SELECT 1 AS [processtype] -- Sequential
         , [wf].[rootworkflowid] AS [defid]
         , [wf].[name] AS [defname]
         , ISNULL([wfd].[name], [wf].[name]) AS [displayname]
         , ISNULL([wfs].[cancreate], 0) AS [allowcreate]
         , NULL AS [allowsearch] -- Not relevant for workflows, since search is performed on object definitions
         , ISNULL([ix].[imageindex], -1) AS [imageindex]
         , [wf].[allowstartwithoutcontext] AS [canstartfromprocessmenu]
         , [wf].[version]
         , [wf].[isactive]
         , 1 AS [cancreate]
         , IIF([wf].[allowdefaultserviceusage] = 0, 1, 0) AS [servicerequired]
         , [wf].[usescontractmanagement]
         , [wf].[usescontractagreement]
         , [wf].[allowdefaultserviceusage]
         , IIF([wf].[prioritymatrixpath] IS NOT NULL, 1, 0) AS [prioritymatrixkind] -- WorkflowPriorityMatrixKind => 0 NotUsed | 1 PriorityForTimes
         , [wf].[prioritymatrixpath] AS [prioritymatrixpathpriority]
    FROM [dbo].[hlwfworkflowdefinition] AS [wf]
    LEFT JOIN [dbo].[hlwfdisplayname] AS [wfd] ON [wf].[rootworkflowid] = [wfd].[rootworkflowid] AND [wf].[version] = [wfd].[version] AND [wfd].[lcid] = @lcid
    OUTER APPLY (
        SELECT [i].[imageindex]
        FROM [dbo].[hlsysobjectdefimage] AS [odi]
        INNER JOIN [dbo].[hlsysimage] AS [i] ON [odi].[imageid] = [i].[imageid] AND [odi].[objectdefid] = [wf].[objectdefid]
    ) AS [ix]
    OUTER APPLY [dbo].[hlsyssec_query_agentwfdefprmcreate](@agentid, [wf].[rootworkflowid], [wf].[version]) AS [wfs]
    WHERE [wf].[fullassemblyname] IS NOT NULL -- Is deployed
      AND ( -- UsesContractManagement? If no, check default service (see: WorkflowDefinitionProvider.GetWorkflowDefinitionsOfUser)
           ([wf].[usescontractmanagement] = 1 OR @hasvaliddefaultservice = 1)
            -- UsesContractAgreement? If yes, check default service (see: WorkflowDefinitionProvider.GetWorkflowDefinitionsOfUser)
       AND ([wf].[usescontractagreement] = 0 OR @hasvaliddefaultservice = 1))
END";

            var procedure_metadata = Sdk.SqlDom.ProcedureGenerator.ParseProcedureBody(TestContext.TestName, sql);
            var schemaMetadata = CreateTestMetadata();
            var res = Sdk.SqlDom.ProcedureGenerator.IsQueryProcedure(true, schemaMetadata, procedure_metadata);
            Assert.AreEqual(1, res.Length, "OutputSet.Count");

            var outputSet = res[0];
            // use: SELECT * FROM sys.dm_exec_describe_first_result_set('dbo.hlomobjectinfo_query', NULL, 0)
            Assert.AreEqual(17, outputSet.ColumnCount, "ColumnCount");
            for (int ix = 0; ix < outputSet.ColumnCount; ix++)
            {
                var column = outputSet.ColumnAt(ix);
                Assert.IsNotNull(column, "ix:" + ix);

                Assert.IsNotNull(column.ColumnDbType, "(" + ix + ") ");
            }
        }
    }
}
