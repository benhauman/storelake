CREATE FUNCTION [dbo].[hlsyssec_query_agentwfdefprmcreate]
(
    @agentid INT,
	@rootworkflowid INT,
	@version INT
)
RETURNS TABLE
RETURN
	SELECT TOP 1 IIF(MAX([sec].[accessmask] & 0x0001) -- HL_ACCESS_READ
	               + MAX([sec].[accessmask] & 0x0002) -- HL_ACCESS_NEW
				     = 3, 1, 0) AS [cancreate]
	FROM [dbo].[hlsysagenttogroup] AS [a2g] WITH (NOLOCK)
	INNER JOIN (
		SELECT [wf].[objectid] AS [groupid], [wf].[accessmask], 1 AS [source]
		FROM [dbo].[hlwfdefrights] AS [wf] WITH (NOLOCK)
		WHERE [wf].[rootworkflowid] = @rootworkflowid AND [wf].[version] = @version
		UNION ALL
		SELECT [g].[groupid], [g].[accessmask], 2 AS [source]
		FROM [dbo].[hlsysglobalacl] AS [g] WITH (NOLOCK)
		WHERE [g].[id] = 810 /* GLOBAL_WFUSER */
	) AS [sec] ON [a2g].[groupid] = [sec].[groupid] AND [a2g].[agentid] = @agentid
	GROUP BY [sec].[source]
	ORDER BY [sec].[source]