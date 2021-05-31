-- @Name GetAgentCounters
-- @Return ClrTypes:QueryEngine.AgentDeskCounters Mode:Single
CREATE PROCEDURE [dbo].[hlsys_query_agentcounters_sp] @agentid INT, @persondefid INT, @personid INT -- Query engine use case -> CD/WD -> Therefore also returns approvals
AS
    SELECT [agentdesk]
         , [waitingqueue]
         , [infodesk]
         , [watchlist]
		 , [approvals]
    FROM [dbo].[hlsys_query_agentcounters](@agentid, @persondefid, @personid)