-- @Name GetSubProcessApprovalResult
-- @Return SubProcess.ApprovalResult
CREATE PROCEDURE [dbo].[hlsp_approvalfulfilled]
	@approvalid UNIQUEIDENTIFIER NULL = NULL,
	@approvementcontext UNIQUEIDENTIFIER NULL = NULL
AS
BEGIN
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