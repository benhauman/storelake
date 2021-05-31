CREATE VIEW [dbo].[hlsyspersonnamevw]
	AS SELECT CAST(0 AS INT)              AS [personid]
			, CAST(0 AS INT)              AS [persondefid]
			, CAST(NULL AS NVARCHAR(255)) AS [name]
			, CAST(0 AS BIT)              AS [isdefault]
	   FROM (VALUES(CAST(N'### DB ARTIFACTS WERE NOT REBUILT AFTER DEPLOY! ###' AS BIT))) AS [x]([a]) -- use Server.RebuildModelApp.exe
