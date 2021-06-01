CREATE FUNCTION [dbo].[hlsysdetail_query_view_case](@casedefid INT, @caseid INT, @lcid INT, @prefertextvalue BIT)
RETURNS TABLE
RETURN
	SELECT CAST([x].[a] AS INT)			  AS [casedefid]
		 , CAST([x].[a] AS INT)			  AS [caseid]
		 , CAST([x].[a] AS INT)			  AS [fieldid]
		 , CAST([x].[a] AS INT)			  AS [contentid]
		 , CAST([x].[a] AS INT)			  AS [datatype]	   -- See: SELECT [system_type_id] FROM [sys].[types]
		 , CAST([x].[a] AS TINYINT)		  AS [displaytype] -- 0: Default | 1: ListItem | 2: TreeItem | 3: Agent | 4: ObjectDefinition
		 , CAST([x].[a] AS BIT)			  AS [value_bit]
		 , CAST([x].[a] AS INT)			  AS [value_int]
		 , CAST([x].[a] AS DECIMAL(11,2)) AS [value_decimal]
		 , CAST([x].[a] AS DATETIME)	  AS [value_datetime]
		 , CAST([x].[a] AS NVARCHAR(MAX)) AS [value_nvarchar]
	FROM (VALUES(@casedefid, @caseid, @lcid, @prefertextvalue, CAST(N'### DB ARTIFACTS WERE NOT REBUILT AFTER DEPLOY! ###' AS BIT))) AS [x]([casedefid], [caseid], [lcid], [prefertextvalue], [a]) -- use Server.RebuildModelApp.exe
