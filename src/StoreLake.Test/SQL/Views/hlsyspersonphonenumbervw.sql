CREATE VIEW [dbo].[hlsyspersonphonenumbervw]
	AS SELECT CAST(0 AS INT)           AS [personid]
			, CAST(0 AS INT)           AS [persondefid]
			, CAST(0 AS NVARCHAR(255)) AS [phonenumber]
			, CAST(0 AS BIT)           AS [isdefault]
	   FROM (VALUES(CAST(N'### DB ARTIFACTS WERE NOT REBUILT AFTER DEPLOY! ###' AS BIT))) AS [x]([a]) -- use Server.RebuildModelApp.exe
