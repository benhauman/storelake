﻿CREATE VIEW [dbo].[hlsyssuattachmentvw]
    AS SELECT CAST([x].[a] AS INT)            AS [sudefid]
            , CAST([x].[a] AS INT)            AS [suid]
            , CAST([x].[a] AS INT)            AS [caseid]
            , CAST([x].[a] AS INT)            AS [suindex]
            , CAST([x].[a] AS INT)            AS [blobid]
            , CAST([x].[a] AS NVARCHAR(510))  AS [name]
            , CAST([x].[a] AS DATETIME)       AS [lastmodified]
            , CAST([x].[a] AS NVARCHAR(4000)) AS [url]
            , CAST([x].[a] AS INT)            AS [blobsize]
            , CAST([x].[a] AS NVARCHAR(20))   AS [blobtype]
       FROM (VALUES(CAST(N'### DB ARTIFACTS WERE NOT REBUILT AFTER DEPLOY! ###' AS BIT))) AS [x]([a]) -- use Server.RebuildModelApp.exe