-- @Namespace ProcessManagement
-- @Name LoadProcessInfo
-- @Return ProcessManagement.ProcessInfo

-- HelplineData instead of ProcessManagement because of Server.ServiceLayer

CREATE -- ALTER
PROCEDURE [dbo].[hlpcm_query_processinfo_v1a] (@lcid INT, @agentid INT)
AS
BEGIN
    SET NOCOUNT ON
    
    DECLARE @hasvaliddefaultservice BIT = IIF(EXISTS(SELECT 1 
                                                     FROM [dbo].[hlsysslmservicedefault] AS [sd]
                                                     INNER JOIN [dbo].[hlsysslmservice] AS [s] ON [sd].[defaultserviceid] = [s].[id] AND [s].[lifecyclestate] <> 0), 1, 0)

    -- Ad hoc definitions
    SELECT [processtype]                = CASE [o].[objecttype] WHEN 2 THEN 0 -- Helpline.BusinessLayer.ProcessInformation.Contracts.BaseType
                                                                WHEN 3 THEN 4
                                                                WHEN 4 THEN 2
                                                                WHEN 5 THEN 3
                                                                WHEN 7 THEN 5
                                          END
         , [defid]                      = [o].[objectdefid]
         , [defname]                    = [o].[name]
         , [displayname]                = [d].[displayname]
         , [allowcreate]                = IIF(ISNULL([secc].[cancreate], 0) = 1 AND ([o].[objecttype] <> 2 OR [pd].[id] IS NOT NULL), 1, 0) -- If case, an ad hoc process definition must be present
         , [allowsearch]                = ISNULL([secs].[cansearch], 0)
         , [imageindex]                 = ISNULL([ix].[imageindex], -1)
       -- CanStartFromProcessMenu = AllowCreate
       --, [canstartfromprocessmenu]    = IIF(ISNULL([secc].[cancreate], 0) = 1 AND ([o].[objecttype] <> 2 OR [pd].[id] IS NOT NULL), 1, 0) -- CanStartFromProcessMenu = AllowCreate
         , [canstartfromprocessmenu]    = IIF(ISNULL([secc].[cancreate], 0) = 1 AND ([o].[objecttype] <> 2 OR [pd].[id] IS NOT NULL), 1, 0) -- If case, an ad hoc process definition must be present
         , [version]                    = NULL
         , [isactive]                   = 1 -- This would make more sense: IIF([pd].[status] = 1, 1, 0). But keep it stable to the (bugged) C# implementation.
         , [cancreate]                  = NULL
         , [servicerequired]            = NULL
         , [usescontractmanagement]     = [pd].[usescontractmanagement]
         , [usescontractagreement]      = [pd].[usescontractagreement]
         , [allowdefaultserviceusage]   = [pd].[allowdefaultserviceusage]
         , [prioritymatrixkind]         = IIF([pd].[prioritymatrixpath] IS NOT NULL, 1, 0) -- AdHocPriorityMatrixKind => 0 NotUsed | 1 PriorityForTimes
         , [prioritymatrixpathpriority] = [pd].[prioritymatrixpath]
    FROM [dbo].[hlsysobjectdef] AS [o]
    LEFT JOIN [dbo].[hlsysadhocprocessdefinition] AS [pd] ON [o].[objectdefid] = [pd].[casedefid] AND [pd].[status] = 1
    LEFT JOIN [dbo].[hlsysdisplayname] AS [d] ON [o].[objectdefid] = [d].[reposid] AND [d].[languageid] = @lcid
    OUTER APPLY (
        SELECT [i].[imageindex]
        FROM [dbo].[hlsysobjectdefimage] AS [odi]
        INNER JOIN [dbo].[hlsysimage] AS [i] ON [odi].[imageid] = [i].[imageid] AND [odi].[objectdefid] = [o].[objectdefid]
    ) AS [ix]
    OUTER APPLY [dbo].[hlsyssec_query_agentpopcdefprmcreate](@agentid, [o].[objectdefid], IIF([objecttype] = 7, 789, [objecttype] + 783)) AS [secc]
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
    SELECT [processtype]                = 1 -- Sequential
         , [defid]                      = [wf].[rootworkflowid]
         , [defname]                    = [wf].[name]
         , [displayname]                = ISNULL([wfd].[name], [wf].[name])
         , [allowcreate]                = ISNULL([wfs].[cancreate], 0)
         , [allowsearch]                = NULL -- Not relevant for workflows, since search is performed on object definitions
         , [imageindex]                 = ISNULL([ix].[imageindex], -1)
         , [canstartfromprocessmenu]    = [wf].[allowstartwithoutcontext]
         , [version]                    = [wf].[version]
         , [isactive]                   = [wf].[isactive]
         , [cancreate]                  = 1
         , [servicerequired]            = IIF([wf].[allowdefaultserviceusage] = 0, 1, 0)
         , [usescontractmanagement]     = [wf].[usescontractmanagement]
         , [usescontractagreement]      = [wf].[usescontractagreement]
         , [allowdefaultserviceusage]   = [wf].[allowdefaultserviceusage]
         , [prioritymatrixkind]         = IIF([wf].[prioritymatrixpath] IS NOT NULL, 1, 0) -- WorkflowPriorityMatrixKind => 0 NotUsed | 1 PriorityForTimes
         , [prioritymatrixpathpriority] = [wf].[prioritymatrixpath]
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
END