CREATE FUNCTION [dbo].[hlsysdefaultattr_query_case](@casedefid INT, @caseid INT, @lcid INT)
RETURNS TABLE
RETURN
    SELECT CAST([x].[a] AS INT)           AS [defaultattrpathid]
		 , CAST([x].[a] AS INT)           AS [casedefid]
         , CAST([x].[a] AS INT)           AS [caseid]
         , CAST([x].[a] AS NVARCHAR(200)) AS [defaultvalue]
	FROM (VALUES(@casedefid, @caseid, @lcid, CAST(N'### DB ARTIFACTS WERE NOT REBUILT AFTER DEPLOY! ###' AS BIT))) AS [x]([casedefid], [caseid], [lcid], [a]) -- use Server.RebuildModelApp.exe