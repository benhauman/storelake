using Microsoft.SqlServer.TransactSql.ScriptDom;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StoreLake.Sdk.CodeGeneration;
using StoreLake.Sdk.SqlDom;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace StoreLake.Test
{

    [TestClass]
    public class ProcedureGenerator_Tests
    {
        public TestContext TestContext { get; set; }



        private static TestSchema s_metadata_1 = new TestSchema()
                    .LoadTables()
                    .LoadViews()
                    .LoadFunctionsMetadata()
                    .LoadUDTs()
                    .AddTable(new TestTable("sys", "tables") // sys.tables
                                .AddColumn("name", DbType.String, false)
                                .AddColumn("object_id", DbType.Int32, false)
                    )
                    .AddTable(new TestTable("sys", "columns") // sys.columns
                                            .AddColumn("object_id", DbType.Int32, false)
                                            .AddColumn("name", DbType.String, false)
                                            .AddColumn("max_length", DbType.Int16, true)
                                            .AddColumn("precision", DbType.Byte, true)
                                            .AddColumn("user_type_id", DbType.Int32, true)
                    )

                    .AddTable(new TestTable("sys", "systypes") // sys.systypes
                                            .AddColumn("xtype", DbType.Byte, false)
                                            .AddColumn("name", DbType.String, false)
                    )

                    .AddTable(new TestTable("dbo", "hlsysagent")
                        .AddColumn("agentid", DbType.Int32, false)
                        .AddColumn("name", DbType.String, false)
                        .AddColumn("fullname", DbType.String, true)
                        .AddColumn("description", DbType.String, true)
                        .AddColumn("active", DbType.Int16, false)
                    )
                    .AddTable(new TestTable("dbo", "hlsysrole")
                        .AddColumn("roleid", DbType.Int32, false)
                        .AddColumn("name", DbType.String, false)
                        .AddColumn("description", DbType.String, true)
                    )
                    .AddTable(new TestTable("dbo", "hlsysagenttorole")
                        .AddColumn("agentid", DbType.Int32, false)
                        .AddColumn("roleid", DbType.Int32, false)
                        .AddColumn("inherited", DbType.Boolean, false)
                        .AddColumn("rank", DbType.Byte, false)
                    )
                    .AddTable(new TestTable("dbo", "hlsysagenttoobject")
                        .AddColumn("agentid", DbType.Int32, false)
                        .AddColumn("objectid", DbType.Int32, false)
                        .AddColumn("objectdefid", DbType.Int32, false)
                    )
                    .AddTable(new TestTable("dbo", "hlsysobjectdef")
                        .AddColumn("objectdefid", DbType.Int32, false)
                        .AddColumn("name", DbType.String, false)
                        .AddColumn("objecttype", DbType.Int32, false)
                        .AddColumn("isfrozen", DbType.Boolean, false)
                    )
                    .AddTable(new TestTable("dbo", "hlsysdisplayname")
                        .AddColumn("reposid", DbType.Int32, false)
                        .AddColumn("languageid", DbType.Int32, false)
                        .AddColumn("displayname", DbType.String, false)
                        .AddColumn("type", DbType.Int16, false)
                    )
                    .AddTable(new TestTable("dbo", "hlsysadhocprocessdefinition")
                        .AddColumn("id", DbType.Guid, false)
                        .AddColumn("version", DbType.Int32, false)
                        .AddColumn("fileversion", DbType.Int32, false)
                        .AddColumn("name", DbType.String, false)
                        .AddColumn("creationdate", DbType.DateTime, false)
                        .AddColumn("lastmodified", DbType.DateTime, false)
                        .AddColumn("lockedby", DbType.String, true)
                        .AddColumn("fullassemblyname", DbType.String, true)
                        .AddColumn("processdefinition", DbType.String, true)
                        .AddColumn("assemblybytes", DbType.Binary, true)
                        .AddColumn("status", DbType.Int32, false)
                        .AddColumn("casedefid", DbType.Int32, true)
                        .AddColumn("category", DbType.Int32, true)
                        .AddColumn("description", DbType.String, true)
                        .AddColumn("usescontractmanagement", DbType.Boolean, true)
                        .AddColumn("usescontractagreement", DbType.Boolean, true)
                        .AddColumn("allowdefaultserviceusage", DbType.Boolean, true)
                        .AddColumn("prioritymatrixpath", DbType.String, true)
                    )
            .AddTable(new TestTable("dbo", "hlwfworkflowdefinition")
                        .AddColumn("rootworkflowid", DbType.Int32, false)
                        .AddColumn("version", DbType.Int32, false)
                        .AddColumn("fileversion", DbType.Int32, false)
                        .AddColumn("name", DbType.String, false)
                        .AddColumn("createdat", DbType.DateTime, false)
                        .AddColumn("lastmodified", DbType.DateTime, false)
                        .AddColumn("lockedby", DbType.String, true)
                        .AddColumn("isactive", DbType.Boolean, false)
                        .AddColumn("fullassemblyname", DbType.String, true)
                        .AddColumn("workflowinfo", DbType.String, true)
                        .AddColumn("assemblybytes", DbType.Binary, true)
                        .AddColumn("hasinstances", DbType.Boolean, true)
                        .AddColumn("description", DbType.String, true)
                        .AddColumn("objectdefname", DbType.String, true)
                        .AddColumn("objectdefid", DbType.Int32, true)
                        .AddColumn("notifgroupid", DbType.Int16, true)
                        .AddColumn("allowstartwithoutcontext", DbType.Boolean, true)
                        .AddColumn("usescontractmanagement", DbType.Boolean, true)
                        .AddColumn("usescontractagreement", DbType.Boolean, true)
                        .AddColumn("allowdefaultserviceusage", DbType.Boolean, true)
                        .AddColumn("prioritymatrixpath", DbType.String, true)
                        .AddColumn("lastmodifiedby", DbType.Int32, true)
                    )
                    .AddTable(new TestTable("dbo", "hlwfdisplayname")
                        .AddColumn("rootworkflowid", DbType.Int32, false)
                        .AddColumn("version", DbType.Int32, false)
                        .AddColumn("lcid", DbType.Int32, false)
                        .AddColumn("name", DbType.String, false)
                    )
                    .AddTable(new TestTable("dbo", "hlsyscasedata")
                        .AddColumn("caseid", DbType.Int32, false)
                        .AddColumn("casedefid", DbType.Int32, false)
                        .AddColumn("currenthistorystep", DbType.Int32, false)
                        .AddColumn("sourceobjectversion", DbType.Int32, false)
                        .AddColumn("internalstate", DbType.Int32, false)
                        .AddColumn("promisedsolutiontime", DbType.DateTime, true)
                        .AddColumn("dataformat", DbType.Byte, true)
                        .AddColumn("suid_first", DbType.Int32, false)
                        .AddColumn("suid_last", DbType.Int32, false)
                        .AddColumn("su_attachmentcount", DbType.Int32, false)
                        .AddColumn("subject", DbType.String, true)
                        .AddColumn("description", DbType.String, true)
                        .AddColumn("solution", DbType.String, true)
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
            Dictionary<string, ProcedureCodeParameter> procedureParameters = new Dictionary<string, ProcedureCodeParameter>();
            var procedure_metadata = Sdk.SqlDom.ProcedureGenerator.ParseProcedureBody("dbo", TestContext.TestName, sql, procedureParameters);
            var schemaMetadata = CreateTestMetadata()
                .AddTable(new TestTable(null, "@table"))
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
            Dictionary<string, ProcedureCodeParameter> procedureParameters = new Dictionary<string, ProcedureCodeParameter>();
            var procedure_metadata = Sdk.SqlDom.ProcedureGenerator.ParseProcedureBody("dbo", TestContext.TestName, sql, procedureParameters);
            //procedure_metadata.BodyFragment.Accept(new DumpFragmentVisitor());
            var schemaMetadata = CreateTestMetadata();
            var res = Sdk.SqlDom.ProcedureGenerator.IsQueryProcedure(true, schemaMetadata, procedure_metadata);
            Assert.AreEqual(1, res.Length);
        }

        //        [TestMethod]
        //        public void hlcmgetcontact()
        //        {
        //            string sql = @"BEGIN
        //SET NOCOUNT ON;
        //SELECT 
        // personid, persondefid, surname, name, language, title,
        // street, city, region, zipcode,country, email, phonenumber
        //FROM dbo.hlcmcontactvw
        //WHERE personid=@PersonId AND persondefid=@PersonDefId
        //END
        //";
        //            var procedure_metadata = Sdk.SqlDom.ProcedureGenerator.ParseProcedureBody(TestContext.TestName, sql);
        //            //procedure_metadata.BodyFragment.Accept(new DumpFragmentVisitor());
        //            var schemaMetadata = CreateTestMetadata();
        //            var res = Sdk.SqlDom.ProcedureGenerator.IsQueryProcedure(true, schemaMetadata, procedure_metadata);
        //            Assert.AreEqual(1, res.Length);
        //        }


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
            Dictionary<string, ProcedureCodeParameter> procedureParameters = new Dictionary<string, ProcedureCodeParameter>();
            var procedure_metadata = Sdk.SqlDom.ProcedureGenerator.ParseProcedureBody("dbo", TestContext.TestName, sql, procedureParameters);
            var schemaMetadata = CreateTestMetadata();
            var res = Sdk.SqlDom.ProcedureGenerator.IsQueryProcedure(true, schemaMetadata, procedure_metadata);
            Assert.AreEqual(1, res.Length);
        }



        [TestMethod]
        public void hlsp_approvalfulfilled()
        {
            TestProcedureOutput(new
            {
                processname = default(string),
                processinstanceid = default(Guid?),
                taskid = default(Guid?),
                approved = default(int?),
            });
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

            Dictionary<string, ProcedureCodeParameter> procedureParameters = new Dictionary<string, ProcedureCodeParameter>();
            var procedure_metadata = Sdk.SqlDom.ProcedureGenerator.ParseProcedureBody("dbo", TestContext.TestName, sql, procedureParameters);
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

            Dictionary<string, ProcedureCodeParameter> procedureParameters = new Dictionary<string, ProcedureCodeParameter>();
            var procedure_metadata = Sdk.SqlDom.ProcedureGenerator.ParseProcedureBody("dbo", TestContext.TestName, sql, procedureParameters);
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

        [TestMethod]
        public void hlpm_ssp_casetable_query()
        {
            string sql = @"
BEGIN
    -- Query case page first, otherwise large data is loaded for all cases and will be ordered and paged afterwards
    DECLARE @cases TABLE ([caseid] INT NOT NULL, [creationtime] DATETIME NOT NULL, [referencenumber] NVARCHAR(50) NOT NULL, PRIMARY KEY([caseid]))
    INSERT INTO @cases ([caseid], [creationtime], [referencenumber])
    SELECT [c].[caseid], [cs].[creationtime], [cs].[referencenumber]
    FROM [dbo].[hlsyssuassociation] AS [ac]
    INNER JOIN [dbo].[hlsysagenttoobject] AS [ato] ON [ac].[associationdefid] = 130
                                                  AND [ac].[objecttypeb]      = 3
                                                  AND [ac].[objectdefidb]     = [ato].[objectdefid]
                                                  AND [ac].[objectidb]        = [ato].[objectid]
                                                  AND [ato].[agentid]         = @agentid
    INNER JOIN [dbo].[hlsyscasedata] AS [c] ON [c].[suid_first] <> 0 AND [ac].[suid] = [c].[suid_first]
    INNER JOIN [dbo].[hlsyscasesystem] AS [cs] ON [c].[caseid] = [cs].[caseid]
    CROSS APPLY [dbo].[hlsyssec_query_agentcaseprmread](@agentid, [c].[caseid], [c].[casedefid]) AS [sec]
    WHERE [sec].[canread] = 1
    ORDER BY [cs].[creationtime]
    OFFSET     @skip ROWS 
    FETCH NEXT @take ROWS ONLY

    SELECT [caseid]          = [c].[caseid]
         , [referencenumber] = [ci].[referencenumber]
         , [casedefid]       = [c].[casedefid]
         , [casedefname]     = ISNULL([od].[displayname], [o].[name])
         , [internalstate]   = [c].[internalstate]
         , [status]          = ISNULL([id].[displayname], [i].[name])
         , [created]         = [ci].[creationtime]
         , [subject]         = [c].[subject]
         , [description]     = [c].[description]
    FROM @cases AS [ci]
    INNER JOIN [dbo].[hlsyscasedata] AS [c] ON [ci].[caseid] = [c].[caseid]
    INNER JOIN [dbo].[hlsysobjectdef] AS [o] ON [c].[casedefid] = [o].[objectdefid]
    INNER JOIN [dbo].[hlsyslistitem] AS [i] ON [c].[internalstate] = [i].[listitemid]
    LEFT JOIN [dbo].[hlsysdisplayname] AS [od] ON [c].[casedefid] = [od].[reposid] AND [od].[languageid] = @languageid
    LEFT JOIN [dbo].[hlsysdisplayname] AS [id] ON [c].[internalstate] = [id].[reposid] AND [id].[languageid] = @languageid
    ORDER BY [ci].[creationtime]
END";

            Dictionary<string, ProcedureCodeParameter> procedureParameters = new Dictionary<string, ProcedureCodeParameter>();
            var procedure_metadata = Sdk.SqlDom.ProcedureGenerator.ParseProcedureBody("dbo", TestContext.TestName, sql, procedureParameters);
            var schemaMetadata = CreateTestMetadata();
            var res = Sdk.SqlDom.ProcedureGenerator.IsQueryProcedure(true, schemaMetadata, procedure_metadata);
            Assert.AreEqual(1, res.Length, "OutputSet.Count");

            var outputSet = res[0];
            // use: SELECT * FROM sys.dm_exec_describe_first_result_set('dbo.hlomobjectinfo_query', NULL, 0)
            Assert.AreEqual(9, outputSet.ColumnCount, "ColumnCount");
            for (int ix = 0; ix < outputSet.ColumnCount; ix++)
            {
                var column = outputSet.ColumnAt(ix);
                Assert.IsNotNull(column, "ix:" + ix);

                Assert.IsNotNull(column.ColumnDbType, "(" + ix + ") ");
            }
        }
        /*
        internal static T ReadParameterValues<T>(T parameter_values)
        {

            var members = parameter_values.GetType().GetProperties();
            //Assert.AreEqual(members.Length, command.Parameters.Count, "Parameters.Count");
            foreach (PropertyInfo member in members)
            {
                DbParameter prm = command.Parameters.TryFindParameter(member.Name);
                if (prm == null)
                {
                    Assert.IsNotNull(prm, "Parameter not found:" + member.Name);
                }
                else
                {
                    object prm_Value;
                    if (prm.Value == DBNull.Value)
                    {
                        prm_Value = null;
                    }
                    else
                    {
                        prm_Value = prm.Value;
                    }

                    if (member.CanWrite)
                    {
                        member.SetValue(parameter_values, prm_Value);
                    }
                    else
                    {
                        var fi = member.GetBackingField();
                        if (fi != null)
                        {
                            fi.SetValue(parameter_values, prm_Value);
                        }
                        else
                        {
                            // System.ArgumentException: Property set method not found.
                            throw new ArgumentException("Property set method not found. Property:" + member.Name);
                        }
                    }
                }
            }
            return parameter_values;
        }
        */
        // T parameter_values
        private void TestProcedureNoOutput()
        {
            TestProcedureOutputCore(0, 0, new { });
        }
        private void TestProcedureOutput<T>(int outputSetCount, int outputSetIndex, T expected_columns)
        {
            TestProcedureOutputCore(outputSetCount, outputSetIndex, expected_columns);
        }

        private void TestProcedureOutput<T>(T expected_columns)
        {
            TestProcedureOutputCore(1, 0, expected_columns);
        }
        private void TestProcedureOutputCore<T>(int outputSetCount, int outputSetIndex, T expected_columns)
        {
            //Type columnsType = typeof(T)
            PropertyInfo[] properties = expected_columns.GetType().GetProperties();
            int expected_columns_count = expected_columns.GetType().GetProperties().Count();

            Dictionary<string, bool> collected_columns = new Dictionary<string, bool>();
            TestProcedureOutputCore(outputSetCount, outputSetIndex, expected_columns_count, (outputColumnName, column) =>
            {
                PropertyInfo pi = TryGetPropertyInfoByName(properties, outputColumnName);
                Assert.IsNotNull(pi, "'" + outputColumnName + "' " + " (" + column.ColumnDbType.Value + ")" + " not expected.");

                collected_columns.Add(outputColumnName, true);

                Type expectedColumnType;
                Type Nullable_UnderlyingType = IsNullable(pi.PropertyType);
                bool expected_AllowNull;
                if (Nullable_UnderlyingType != null)
                {
                    expectedColumnType = Nullable_UnderlyingType;
                    expected_AllowNull = true;
                }
                else
                {
                    expectedColumnType = pi.PropertyType;
                    expected_AllowNull = !pi.PropertyType.IsValueType;
                }

                Type columnClrType = TypeMap.ResolveColumnClrType(column.ColumnDbType.Value);

                Assert.AreEqual(expectedColumnType.FullName, columnClrType.FullName, outputColumnName);

                if (expected_AllowNull != column.AllowNull.Value)
                {
                    if (!column.AllowNull.Value && (columnClrType == typeof(string)))
                    {
                        // TODO: use empty string for required string
                        string pi_Value = (string)pi.GetValue(expected_columns);
                        if (pi_Value != null)
                        {
                            // ok
                        }
                        else
                        {
                            Assert.AreEqual(expected_AllowNull, column.AllowNull.Value, "AllowNull  '" + outputColumnName + "' " + " (" + column.ColumnDbType.Value + ")");
                        }
                    }
                    else
                    {
                        Assert.AreEqual(expected_AllowNull, column.AllowNull.Value, "AllowNull '" + outputColumnName + "' " + " (" + column.ColumnDbType.Value + ")");
                    }
                }
            });

            if (properties.Length > collected_columns.Count)
            {
                // not all columns has been collected/found
                for (int ix = 0; ix < properties.Length; ix++)
                {
                    var pi = properties[ix];
                    Assert.IsTrue(collected_columns.ContainsKey(pi.Name), "'" + pi.Name + "' not found.");
                }
            }
        }

        private static Type IsNullable(Type tValue)
        {
            return Nullable.GetUnderlyingType(tValue);// != null;
        }

        private PropertyInfo TryGetPropertyInfoByName(PropertyInfo[] properties, string propertyName)
        {
            for (int ix = 0; ix < properties.Length; ix++)
            {
                var pi = properties[ix];
                if (string.Equals(pi.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                    return pi;
            }
            return null;
        }

        private void TestProcedureOutputCore(int outputSetCount, int outputSetIndex, int columnCount, Action<string, ProcedureOutputColumn> column_assert)
        {
            var ddl = ResourceHelper.GetSql("SQL.Procedures." + TestContext.TestName);

            TSqlFragment sqlF = ScriptDomFacade.Parse(ddl);

            string sql_body = TestResources.LoadProcedureBody(ddl);

            CreateProcedureStatement stmt_CreateFunction = (CreateProcedureStatement)((TSqlScript)sqlF).Batches[0].Statements[0];
            Dictionary<string, ProcedureCodeParameter> procedureParameters = new Dictionary<string, ProcedureCodeParameter>();

            foreach (ProcedureParameter prm in stmt_CreateFunction.Parameters)
            {
                ProcedureCodeParameter parameterType;
                if (prm.DataType.Name.Count == 2)
                {
                    //SqlDbType sqlType = SqlDbType.Structured;
                    string fullName = prm.DataType.Name.SchemaIdentifier.Dequote()
                        + "." + prm.DataType.Name.BaseIdentifier.Dequote();
                    parameterType = new ProcedureCodeParameter(fullName, DbType.Object);
                }
                else
                {
                    var parameterDbType = ProcedureGenerator.ResolveToDbDataType(prm.DataType);

                    SqlDbType sqlType = ResolveToSqlType(parameterDbType);

                    parameterType = Sdk.CodeGeneration.TypeMap.GetParameterClrType(sqlType, "bzzz");
                }
                string parameterName = prm.VariableName.Dequote();
                //var parameterDbType = ProcedureGenerator.ResolveToDbDataType(prm.DataType);
                //
                //SqlDbType sqlType = ResolveToSqlType(parameterDbType);
                //var parameterType = Sdk.CodeGeneration.SchemaExportCode.GetParameterClrType(sqlType, "bzzz");
                procedureParameters.Add(parameterName, parameterType);
            }


            ProcedureMetadata procedure_metadata = Sdk.SqlDom.ProcedureGenerator.ParseProcedureBody("dbo", TestContext.TestName, sql_body, procedureParameters);

            CreateProcedureStatement stmt = (CreateProcedureStatement)((TSqlScript)(sqlF)).Batches[0].Statements[0];
            foreach (ProcedureParameter prm in stmt.Parameters)
            {
                string parameterName = prm.VariableName.Dequote();
                ProcedureCodeParameter parameterType;
                if (prm.DataType.Name.Count == 2)
                {
                    //SqlDbType sqlType = SqlDbType.Structured;
                    string fullName = prm.DataType.Name.SchemaIdentifier.Dequote()
                        + "." + prm.DataType.Name.BaseIdentifier.Dequote();
                    parameterType = new ProcedureCodeParameter(fullName, DbType.Object);
                }
                else
                {
                    var parameterDbType = ProcedureGenerator.ResolveToDbDataType(prm.DataType);

                    SqlDbType sqlType = ResolveToSqlType(parameterDbType);

                    parameterType = Sdk.CodeGeneration.TypeMap.GetParameterClrType(sqlType, "bzzz");
                }
                //??procedure_metadata.AddParameter(parameterName, parameterType);
            }

            //procedure_metadata.BodyFragment.Accept(new DumpFragmentVisitor(true));

            var schemaMetadata = CreateTestMetadata();
            var res = Sdk.SqlDom.ProcedureGenerator.IsQueryProcedure(true, schemaMetadata, procedure_metadata);
            Assert.AreEqual(outputSetCount, res.Length, "OutputSet.Count");

            if (outputSetCount == 0 && outputSetIndex == 0)
            {
                // no output expected. outputSetIndex must be 0
            }
            else
            {
                var outputSet = res[outputSetIndex];
                // use: SELECT * FROM sys.dm_exec_describe_first_result_set('dbo.hlomobjectinfo_query', NULL, 0)
                Assert.AreEqual(columnCount, outputSet.ColumnCount, "ColumnCount");

                IDictionary<string, ProcedureOutputColumn> outputColumnNames = new SortedDictionary<string, ProcedureOutputColumn>(StringComparer.OrdinalIgnoreCase);
                for (int ix = 0; ix < outputSet.ColumnCount; ix++)
                {
                    ProcedureOutputColumn outputColumn = outputSet.ColumnAt(ix);
                    Assert.IsNotNull(outputColumn, "ix:" + ix);

                    string outputColumnName = ProcedureOutputSet.PrepareOutputColumnName(outputSet, outputColumn, outputColumnNames.Keys, ix);
                    outputColumnNames.Add(outputColumnName, outputColumn);

                    Assert.IsTrue(outputColumn.ColumnDbType.HasValue, "(" + ix + ") column [" + outputColumnName + "]");

                    Sdk.CodeGeneration.TypeMap.ResolveColumnClrType(outputColumn.ColumnDbType.Value);
                    Assert.IsFalse(string.IsNullOrEmpty(outputColumnName), "(" + ix + ") column [" + outputColumnName + "]");

                    column_assert(outputColumnName, outputColumn);
                }
            }
        }

        private static SqlDbType ResolveToSqlType(DbType parameterDbType)
        {
            if (parameterDbType == DbType.Int16)
                return SqlDbType.SmallInt;
            if (parameterDbType == DbType.Int32)
                return SqlDbType.Int;
            if (parameterDbType == DbType.Int64)
                return SqlDbType.BigInt;
            if (parameterDbType == DbType.Boolean)
                return SqlDbType.Bit;
            if (parameterDbType == DbType.DateTime)
                return SqlDbType.DateTime;
            if (parameterDbType == DbType.Byte)
                return SqlDbType.TinyInt;
            if (parameterDbType == DbType.String)
                return SqlDbType.NVarChar;
            if (parameterDbType == DbType.Guid)
                return SqlDbType.UniqueIdentifier;
            throw new System.NotImplementedException("" + parameterDbType);
        }

        [TestMethod]
        public void hlsys_query_historydetails()
        {
            // [finalresult] is a CTE and not a NamedTableReference
            TestProcedureOutput(new
            {
                timestamp = default(DateTime?),
                agentid = default(int?),
                type = default(short?),
                historyitem = default(string),
                associationdefid = default(int?),
                otherobjectid = default(int?),
                otherobjectdefid = default(int?),
                isobjecta = default(bool?),
                agentname = default(string),
                associationdefname = default(string),
                associationdisplayname = "", //???
                otherobjectname = default(string),
            });
        }

        [TestMethod]
        public void hlcmgetcontact()
        {
            TestProcedureOutput(new
            {
                personid = default(int),
                persondefid = default(int),
                surname = default(string),
                name = default(string),
                language = default(int?),
                title = default(string),
                street = default(string),
                city = default(string),
                region = default(string),
                zipcode = default(string),
                country = default(string),
                email = default(string),
                phonenumber = "", //????
            });
        }

        [TestMethod]
        public void hlom_query_possibleactions()
        {
            TestProcedureOutput(new { actionid = default(short?) });
        }
        [TestMethod]
        public void hlsyssession_getdisconnectedsessions()
        {
            TestProcedureOutput(new { SessionId = default(Guid?) });
        }

        [TestMethod]
        public void hlsys_query_agentcounters_sp()
        {
            TestProcedureOutput(new
            {
                agentdesk = default(int),
                waitingqueue = default(int),
                infodesk = default(int),
                watchlist = default(int),
                approvals = default(int),
            });
        }


        [TestMethod]
        public void hlsys_query_templates()
        {
            // cte recursion 'grouprecursion'
            TestProcedureOutput(new
            {
                Id = default(int?),
                Path = default(string),
                Name = default(string),
                AgentId = default(int?),
                Sortorder = default(int?),
                ObjectDefinitionName = default(string),
                ParentId = default(int?),
            });
        }

        [TestMethod]
        public void hlsys_store_objectqueue()
        {
            // UDT variable
            TestProcedureOutput(new
            {
                objectid = default(int?),
                objectdefid = default(int?),
                agentid = default(int?),
                agentdesk = default(bool?),
                waitingqueue = default(bool?),
                infodesk = default(bool?),
                agentdeskchanged = default(bool?),
                waitingqueuechanged = default(bool?),
                infodeskchanged = default(bool?),
            });
        }

        [TestMethod]
        public void hlsysadhocinstanceaction_targetqueue_receive()
        {
            // conversation handle
            TestProcedureOutput(new
            {
                targetconversationhandle = default(Guid?),
                requestid = default(int?),
            });
        }

        [TestMethod]
        public void hlsyscal_calculate_offset()
        {
            // @expected_datetime_utc
            TestProcedureOutput(7, 0, new
            {
                TEST = default(string),
                TEST_DFF = default(int?),
                timezone = default(string),
                result_datetime_utc = default(DateTime?),
                result_datetime_loc = default(DateTime?),
                expected_datetime_utc = default(DateTime?),
                expected_datetime_loc = default(DateTime?),
                expected_datetime_dst = default(int),
            });
        }

        [TestMethod]
        public void hlsysdetail_query_case()
        {
            TestProcedureOutput(3, 0, new
            {
                hasconfiguration = default(int),
                objectdefinitiondisplayname = "",//???
            });
            TestProcedureOutput(3, 1, new
            {
                type = default(int?),
                kind = default(short?), // ELSE [apo].[attr_type]
                displayname = "",//???
                datatype = default(int?),
                valuebit = default(bool?),
                valueint = default(int?),
                valuedecimal = default(decimal?),
                valuedatetime = default(DateTime?),
                valuenvarchar = default(string),
                associatedobjectdefid = default(int?),
                associatedobjectid = default(int?),
                hasdisplayvalue = default(int?),
                displayvalue = "", //???
                displayregion = default(byte?),
                sortorder = default(byte?),
            });
            TestProcedureOutput(3, 2, new
            {
                blobid = default(int?),
                name = default(string),
                blobsize = default(int?),
                blobtype = default(string),
            });
        }

        [TestMethod]
        public void hlsyscontract_increment_numberofcalls()
        {
            TestProcedureOutput(new { value = default(bool?) });
        }

        [TestMethod]
        public void hlsyscfgchange_targetqueue_receive()
        {
            TestProcedureOutput(new { value = default(string) });
        }

        [TestMethod]
        public void hlsysdetail_query_su()
        {
            TestProcedureOutput(2, 0, new
            {
                suid = default(int?),
                suindex = default(Int64?), // ??????? DENSE_RANK
                type = default(int),
                kind = default(short?), // ELSE [apo].[attr_type]
                displayname = "", //??
                datatype = default(int?),
                valuebit = default(bool?),
                valueint = default(int?),
                valuedecimal = default(decimal?),
                valuedatetime = default(DateTime?),
                valuenvarchar = default(string),
                associatedobjectdefid = default(int?),
                associatedobjectid = default(int?),
                hasdisplayvalue = default(int),
                displayvalue = "",//???
                displayregion = default(byte?),
                sortorder = default(byte?),
            });
            TestProcedureOutput(2, 1, new
            {
                suid = default(int?),
                suindex = default(int?),
                blobid = default(int?),
                name = default(string),
                blobsize = default(int?),
                blobtype = default(string),
            });
        }

        [TestMethod]
        public void hlsyslic_getpendingbooklicenserequest()
        {
            // StringComparer.OrdinalIgnoreCase for sources 'PortalCount <> portalCount'
            TestProcedureOutput(new
            {
                SessionId = default(Guid?),
                LicenseAction = default(int),
                HlProduct = default(int?),
                PerSeatToken = default(string),
                UserId = default(int?),
                UserDefId = default(int?),
                LicenseKey = default(string),
                CanUsePerSeatLicense = default(int),
                CountPortalUser = default(int?),
            });
        }

        [TestMethod]
        public void hlsysportal_query_casetable_data_user()
        {
            // ParenthesisExpression
            TestProcedureOutput(new
            {
                caseid = default(int?),
                casedefid = default(int?),
                internalstate = default(int?),
                caseimageid = default(int?),
                field_position = default(byte?),
                field_attrpathid = default(int?),
                field_display_kind = default(byte?),
                field_display_type = default(byte?),
                field_value_type = default(byte?),
                value_nvarchar = default(string),
                value_datetime = default(DateTime?),
                value_decimal = default(decimal?),
                value_int = default(int?),
                value_bit = default(bool?),
                field_has_displayvalue = default(bool),
                displayvalue = "",//???
                listitemimageid = default(int?), //!!! // ELSE NULL END) AS INT) AS listitemimageid
            });
        }

        [TestMethod]
        public void hlsyssec_qyery_agentdynamicsystemaccessacl() // hlsyssec_qyery_agentsystemitemacl
        {
            // SqlMultiStatementTableValuedFunction : hlsyssec_query_agentsystemacl
            TestProcedureOutput(new { accessmask = default(short?) });
        }

        [TestMethod]
        public void hlsyssession_query_countportaluser()
        {
            TestProcedureOutput(new { value = default(int?) });
        }

        [TestMethod]
        public void hlaiwebrequestsolution_run()
        {
            TestProcedureOutput(new
            {
                requestid = default(int?),
                isconfident = default(int),
                position = default(byte?),
                subject = default(string),
                excerpt = default(string),
            });
        }

        [TestMethod]
        public void hlbigettablemetadata()
        {
            TestProcedureOutput(new
            {
                TableName = "",
                ColumnName = "",
                TypeName = "",
                MaxLength = default(short?),
                Precision = default(byte?),
                Hashvalue = default(byte?),
            });
        }
        [TestMethod]
        public void hlbigettomattribute()
        {
            // ScalarSubQuery
            TestProcedureOutput(new
            {
                id = default(int?),
                objectdefinition = default(string),
                attributepath = default(string),
                description = default(string),
                ExternalDescription = default(string),
                tablename = default(string),
                ColumnName = default(string),
                ExternalColumnName = default(string),
                DataType = "",
                HashValue = default(byte?),
                CreatedOn = default(DateTime?),
                ModifiedOn = default(DateTime?),
                DeletedOn = default(DateTime?),
                Deleted = default(bool?),
                Status = default(string),
            });
        }

        [TestMethod]
        public void hlbigettomtable()
        {
            // SUBSTRING
            TestProcedureOutput(new
            {
                tablename = default(string),
                Columns = default(string),
                Query = default(string),
                HashValue = default(byte[]),
                CreatedOn = default(DateTime?),
                ModifiedOn = default(DateTime?),
                DeletedOn = default(DateTime?),
                Deleted = default(bool?),
                Status = default(string),
            });
        }
        // 
        [TestMethod]
        public void hlsysdxi_generate_objecthistory()
        {
            // XML
            TestProcedureOutput(new
            {
                objecttype = default(int?),
                objectid = default(int?),
                objectdefid = default(int?),
                historyid = default(int?),
                historyitem = default(XElement),
                type = default(byte?),
            });
        }
        [TestMethod]
        public void hlsyssvccat_query_catalogflat()
        {
            // POWER
            TestProcedureOutput(2, 0, new
            {
                id = default(Guid?),
                displayname = "",//????
            });
            TestProcedureOutput(2, 1, new
            {
                type = default(int),
                id = default(Guid?),
                displayname = "",//???
                description = default(string),
                image = default(byte[]),
                processdefinitionid = default(int?),
                price = default(decimal?),
                currency = default(string),
                restrictions = default(int?),
            });
        }

        [TestMethod]
        public void hlsyssvccat_query_catalogtree()
        {
            //  ScalarSubQuery
            TestProcedureOutput(2, 0, new
            {
                id = default(Guid?),
                parentid = default(Guid?),
                displayname = "",//???
                productcount = default(int?),
            });
            TestProcedureOutput(2, 1, new
            {
                id = default(Guid?),
                kind = default(int),
                displayname = "",//???
                description = default(string),
                thumbnail = default(byte[]),
                processdefinitionid = default(int?),
                isbundle = default(int),
                price = default(decimal?),
                currency = default(string),
                restrictions = default(int),
            });
        }

        [TestMethod]
        public void hlsur_rpt_overview_fivestar()
        {
            // AVG
            TestProcedureOutput(new
            {
                name = default(string),
                avg = default(decimal?),
                one = default(int),
                two = default(int),
                three = default(int),
                four = default(int),
                five = default(int),
            });
        }
        [TestMethod]
        public void hlsur_surveycontent_insert()
        {
            TestProcedureOutput(new
            {
                value0 = default(string),
                value1 = default(string),
                value2 = default(int?),
            });
        }

        [TestMethod]
        public void hlsysum_query_adminorgunits()
        {
            TestProcedureOutput(new
            {
                orgunitid = default(int?),
                orgunitdefid = default(int?),
                name = default(string),
                parentorgunitid = default(int?),
                parentorgunitdefid = default(int?),
                haschildren = default(bool),
                hasadmins = default(bool),
                found = default(bool),
            });
        }

        [TestMethod]
        public void hlseglobalsearch_query_filters()
        {
            TestProcedureOutput(new
            {
                name = default(string),
                displayname = "", //???  [displayname] = NULL
                kind = default(int?),
                name2 = default(string),
                displayname2 = "", //??? [displayname] = NULL
                imageurl = default(string),
                count = default(int?),
            });
        }

        [TestMethod]
        public void hlsyssession_connectaddon()
        {
            TestProcedureOutput(new
            {
                Result = default(int?),
                DaysToFinalSaasExpiration = default(int?) //NULL AS 
            });
        }
        [TestMethod]
        public void hlmad_contract_fulltextsearch_query()
        {
            TestProcedureOutput(new
            {
                identifier = default(string),
                count = default(int?),
                displayvalue = default(string),
                hint = default(int?),
                objectdefid = default(int?),
                objectid = default(int?),
            });
        }

        [TestMethod]
        public void hlsyscal_calculate_difference_formatted()
        {
            TestProcedureOutput(new
            {
                ResultDaysCompensated = default(int),
                ResultDiffSecondsCompensated = default(int),
                ResultTotalSecondsCompensated = default(int?),
                ResultNothing = default(bool),
            });
        }

        [TestMethod]
        public void spBlobSelect() // PathName, GET_FILESTREAM_TRANSACTION_CONTEXT
        {
            TestProcedureOutput(new
            {
                Path = default(string),
                TransactionContext = default(byte[])
            });
        }

        [TestMethod]
        public void hlsyssession_connectportal() // TRY_CAST
        {
            TestProcedureOutput(new
            {
                AgentId = default(int?), // 1 !?! Nullable variable => TODO evaluate variable initialization
                AgentName = default(string), // 2
                PersonId = default(int?), // 3
                PersonDefId = default(int?), // 4
                PersonSurname = default(string), // 5
                PersonGivenName = default(string), // 6
                PersonMail = default(string), // 7
                EnvironmentName = default(string), // 8
                ReferenceNoFormat = default(string), // 9
                ShowTaskDesks = default(bool?), // 10
                PortalSessionTimeout = default(string), // 11
                SessionId = default(Guid?), // 12
                Result = default(byte?), // 13
                DaysSinceSaasExpiration = default(int?), // 14

            });
        }
        // 
        [TestMethod]
        public void hlnewseditorial_merge() // udt
        {
            TestProcedureNoOutput();
        }

        // hlseglobalsearch_query_groups
        // 
        // 
    }
}
