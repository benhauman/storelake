CREATE PROCEDURE [dbo].[hlmad_contract_fulltextsearch_query] @agentid INT, @lcid INT, @condition NVARCHAR(128), @skip INT, @take INT, @calculatecount BIT
AS
BEGIN
    SET NOCOUNT ON
	
    --DECLARE @agentid INT = 710
    --DECLARE @lcid INT = 1033
    --DECLARE @condition NVARCHAR(128) = '"helpline*"'
    --DECLARE @skip INT = 0
    --DECLARE @take INT = 5
    --DECLARE @calculatecount BIT = 1
    
	DECLARE @count INT 

	IF @calculatecount = 1
	BEGIN
		SELECT @count = COUNT(1)
		FROM [dbo].[hlsysobjectdata] AS [d]
		INNER JOIN [dbo].[hlsyscontractdef] AS [o] ON [d].[objectdefid] = [o].[contractdefid]
		CROSS APPLY [dbo].[hlsyssec_query_agentobjectprmreadsearch] (@agentid, [d].[objectid], [d].[objectdefid], 789) AS [sec]
		WHERE [sec].[canreadandsearch] = 1 AND CONTAINS([d].[objectdata], @condition)
	END

	SELECT [identifier]   = N'Contract'
	     , [count]        = @count
	     , [displayvalue] = [t].[defaultvalue]
	     , [hint]         = CAST(NULL AS INT)
		 , [objectdefid]  = [o].[objectdefid]
         , [objectid]     = [o].[objectid]
    FROM CONTAINSTABLE([dbo].[hlsysobjectdata], [objectdata], @condition) AS [f]
    INNER JOIN [dbo].[hlsysobjectdata] AS [o] ON [f].[KEY] = [o].[objectid]
    CROSS APPLY [dbo].[hlsysdefaultattr_query_contract] ([o].[objectdefid], [o].[objectid], @lcid) AS [t]
    CROSS APPLY [dbo].[hlsyssec_query_agentobjectprmreadsearch] (@agentid, [o].[objectid], [o].[objectdefid], 789) AS [sec]
	WHERE [sec].[canreadandsearch] = 1
    ORDER BY [f].[RANK] DESC
    OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY
END