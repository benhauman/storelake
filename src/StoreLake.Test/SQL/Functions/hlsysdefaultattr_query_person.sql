CREATE FUNCTION [dbo].[hlsysdefaultattr_query_person](@persondefid INT, @personid INT, @lcid INT)
RETURNS TABLE
RETURN
    SELECT CAST([x].[a] AS INT)           AS [defaultattrpathid]
		 , CAST([x].[a] AS INT)           AS [persondefid]
		 , CAST([x].[a] AS INT)           AS [personid]
		 , CAST([x].[a] AS NVARCHAR(200)) AS [defaultvalue]
	FROM (VALUES(@persondefid, @personid, @lcid, CAST(N'### DB ARTIFACTS WERE NOT REBUILT AFTER DEPLOY! ###' AS BIT))) AS [x]([persondefid], [personid], [lcid], [a]) -- use Server.RebuildModelApp.exe