-- @Namespace ProcessManagement
-- @Name LoadProcessInfo
-- @Return ProcessManagement.ProcessInfo

-- HelplineData instead of ProcessManagement because of Server.ServiceLayer

CREATE -- ALTER
PROCEDURE [dbo].[hlpcm_query_processinfo_v2] (@lcid INT, @agentid INT)
AS
BEGIN
    SET NOCOUNT ON
    
    DECLARE @hasvaliddefaultservice BIT = IIF(EXISTS(SELECT 1 
                                                     FROM [dbo].[hlsysslmservicedefault] AS [sd]
                                                     INNER JOIN [dbo].[hlsysslmservice] AS [s] ON [sd].[defaultserviceid] = [s].[id] AND [s].[lifecyclestate] <> 0), 1, 0)

    -- Workflow definitions
    SELECT [processtype]                = 1 -- Sequential
         , [defid]                      = [wf].[rootworkflowid]
         , [defname]                    = [wf].[name]
         , [displayname]                = ISNULL([wfd].[name], [wf].[name])
         , [allowcreate]                = ISNULL([wfs].[cancreate], 0)
         , [allowsearch]                = CAST(NULL AS BIT) -- Not relevant for workflows, since search is performed on object definitions
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