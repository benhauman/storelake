CREATE FUNCTION [dbo].[hlsysdefaultattr_query_orgunit](@orgunitdefid INT, @orgunitid INT, @lcid INT)
RETURNS TABLE
RETURN
    SELECT CAST([x].[a] AS INT)           AS [defaultattrpathid]
		 , CAST([x].[a] AS INT)           AS [orgunitdefid]
		 , CAST([x].[a] AS INT)           AS [orgunitid]
		 , CAST([x].[a] AS NVARCHAR(200)) AS [defaultvalue]
	FROM (VALUES(@orgunitdefid, @orgunitid, @lcid, CAST(N'### DB ARTIFACTS WERE NOT REBUILT AFTER DEPLOY! ###' AS BIT))) AS [x]([orgunitdefid], [orgunitid], [lcid], [a]) -- use Server.RebuildModelApp.exe