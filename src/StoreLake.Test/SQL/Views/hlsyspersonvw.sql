CREATE VIEW [dbo].[hlsyspersonvw]
	AS SELECT CAST([x].[a] AS INT)          AS [personid]
			, CAST([x].[a] AS INT)          AS [persondefid]
			, CAST([x].[a] AS NVARCHAR(50)) AS [controller]
			, CAST([x].[a] AS NVARCHAR(50)) AS [inputname]
			, CAST([x].[a] AS INT)          AS [version]
	   FROM (VALUES(CAST(N'### DB ARTIFACTS WERE NOT REBUILT AFTER DEPLOY! ###' AS BIT))) AS [x]([a]) -- use Server.RebuildModelApp.exe
