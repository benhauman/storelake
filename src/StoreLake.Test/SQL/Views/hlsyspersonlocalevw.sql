CREATE VIEW [dbo].[hlsyspersonlocalevw]
	AS SELECT CAST([x].[a] AS INT) AS [personid]
			, CAST([x].[a] AS INT) AS [persondefid]
			, CAST([x].[a] AS INT) AS [localeid]
			, CAST([x].[a] AS BIT) AS [isdefault]
	   FROM (VALUES(CAST(N'### DB ARTIFACTS WERE NOT REBUILT AFTER DEPLOY! ###' AS BIT))) AS [x]([a]) -- use Server.RebuildModelApp.exe