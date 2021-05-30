CREATE FUNCTION [dbo].[hlsysdefaultattr_query_contract](@contractdefid INT, @contractid INT, @lcid INT)
RETURNS TABLE
RETURN
    SELECT CAST([x].[a] AS INT)           AS [defaultattrpathid]
		 , CAST([x].[a] AS INT)           AS [contractdefid]
		 , CAST([x].[a] AS INT)           AS [contractid]
		 , CAST([x].[a] AS NVARCHAR(200)) AS [defaultvalue]
	FROM (VALUES(@contractdefid, @contractid, @lcid, CAST(N'### DB ARTIFACTS WERE NOT REBUILT AFTER DEPLOY! ###' AS BIT))) AS [x]([contractdefid], [contractid], [lcid], [a]) -- use Server.RebuildModelApp.exe