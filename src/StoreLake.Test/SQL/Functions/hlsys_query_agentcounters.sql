CREATE FUNCTION [dbo].[hlsys_query_agentcounters] (@agentid INT, @persondefid INT, @personid INT)
RETURNS TABLE
RETURN
    SELECT [agentdesk]    = ISNULL(SUM(IIF([q].[agentdesk]    = 1, 1, 0)), 0)
         , [waitingqueue] = ISNULL(SUM(IIF([q].[waitingqueue] = 1, 1, 0)), 0)
         , [infodesk]     = ISNULL(SUM(IIF([q].[infodesk]     = 1, 1, 0)), 0)
         , [watchlist]    = ISNULL(SUM(IIF([q].[watchlist]    = 1, 1, 0)), 0)
         , [approvals]    = ISNULL(SUM(IIF([q].[approvals]    = 1, 1, 0)), 0)
    FROM (
		SELECT [q].[agentdesk], [q].[waitingqueue], [q].[infodesk], [watchlist] = 0, [approvals] = 0
		FROM [dbo].[hlsysobjectqueue] AS [q]
		WHERE [q].[agentid] = @agentid
		UNION ALL
		SELECT [agentdesk] = 0, [waitingqueue] = 0, [infodesk] = 0, [watchlist] = 1, [approvals] = 0
		FROM [dbo].[hlsyswatchlist] AS [w]
		WHERE [w].[agentid] = @agentid
		UNION ALL
		SELECT [agentdesk] = 0, [waitingqueue] = 0, [infodesk] = 0, [watchlist] = 0, [approvals] = 1
		FROM [dbo].[hlsysapprovements] AS [a]
		WHERE [a].[approverdefid] = @persondefid 
		  AND [a].[approverid] = @personid
		  AND NOT EXISTS(SELECT 1 FROM [dbo].[hlsysapprovementfulfillment] AS [af] WHERE [af].[approvementid] = [a].[approvementid])
	) AS [q]