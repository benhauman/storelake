CREATE PROCEDURE [dbo].[hlsys_store_objectqueue] @queueitems [dbo].[hlsys_udt_objectqueueitem] READONLY
AS
BEGIN

	DECLARE @queueitemchanges [dbo].[hlsys_udt_objectqueueitemchanges]

	;WITH [sourcequeue] AS
	(
		SELECT [agentid], [objectdefid], [objectid], [agentdesk], [waitingqueue], [infodesk]
		FROM @queueitems 
		WHERE [agentdesk] <> 0 OR [waitingqueue] <> 0 OR [infodesk] <> 0
	), [targetqueue] AS
	(
		SELECT [q].[objectid], [q].[objectdefid], [q].[agentid], [q].[agentdesk], [q].[waitingqueue], [q].[infodesk]
		FROM [dbo].[hlsysobjectqueue] AS [q]
		WHERE EXISTS (
			SELECT 1
			FROM @queueitems AS [qi] 
			WHERE [q].[objectid] = [qi].[objectid] AND [q].[objectdefid] = [qi].[objectdefid]
		)
	)
	MERGE [targetqueue] AS [T]
	USING [sourcequeue] AS [S]
	ON ([T].[objectid] = [S].[objectid]
    AND [T].[objectdefid] = [S].[objectdefid]
	AND [T].[agentid] = [S].[agentid]
    AND ([S].[agentdesk] <> 0 OR [S].[waitingqueue] <> 0 OR [S].[infodesk] <> 0))
	WHEN MATCHED AND ([T].[agentdesk] <> [S].[agentdesk]
	               OR [T].[waitingqueue] <> [S].[waitingqueue]
				   OR [T].[infodesk] <> [S].[infodesk]) THEN
		UPDATE SET [T].[agentdesk] = [S].[agentdesk]
				 , [T].[waitingqueue] = [S].[waitingqueue]
				 , [T].[infodesk] = [S].[infodesk]
	WHEN NOT MATCHED BY TARGET THEN 
		INSERT (    [objectid],     [objectdefid],     [agentid],     [agentdesk],     [waitingqueue],     [infodesk]) 
		VALUES ([S].[objectid], [S].[objectdefid], [S].[agentid], [S].[agentdesk], [S].[waitingqueue], [S].[infodesk])
	WHEN NOT MATCHED BY SOURCE THEN
		DELETE
		OUTPUT ISNULL([inserted].[objectid], [deleted].[objectid]) AS [objectid]
		     , ISNULL([inserted].[objectdefid], [deleted].[objectdefid]) AS [objectdefid]
		     , ISNULL([inserted].[agentid], [deleted].[agentid]) AS [agentid]
			 
		     , ISNULL([inserted].[agentdesk], 0) AS [agentdesk]
		     , ISNULL([inserted].[waitingqueue], 0) AS [waitingqueue]
		     , ISNULL([inserted].[infodesk], 0) AS [infodesk]
			 
			 , IIF(ISNULL([inserted].[agentdesk], 0) = ISNULL([deleted].[agentdesk], 0), 0, 1) AS [agentdeskchanged]
			 , IIF(ISNULL([inserted].[waitingqueue], 0) = ISNULL([deleted].[waitingqueue], 0), 0, 2) AS [waitingqueuechanged]
			 , IIF(ISNULL([inserted].[infodesk], 0) = ISNULL([deleted].[infodesk], 0), 0, 4) AS [infodeskchanged]
		INTO @queueitemchanges ([objectid], [objectdefid], [agentid], [agentdesk], [waitingqueue], [infodesk], [agentdeskchanged], [waitingqueuechanged], [infodeskchanged])
	;

	SELECT [objectid], [objectdefid], [agentid], [agentdesk], [waitingqueue], [infodesk], [agentdeskchanged], [waitingqueuechanged], [infodeskchanged]
	FROM @queueitemchanges
END