CREATE VIEW [dbo].[hlsysorgunitnamevw] (orgunitid, orgunitdefid, name, isdefault) AS
	SELECT CAST([x].[a] AS INT)           AS [orgunitid]
		 , CAST([x].[a] AS INT)           AS [orgunitdefid]
		 , CAST([x].[a] AS NVARCHAR(255)) AS [name]
		 , CAST([x].[a] AS BIT)           AS [isdefault]
	FROM (VALUES(CAST(N'### DB ARTIFACTS WERE NOT REBUILT AFTER DEPLOY! ###' AS BIT))) AS [x]([a]) -- use Server.RebuildModelApp.exe
