﻿CREATE VIEW [dbo].[hlsyscasevw]
    AS SELECT [caseid]               = CAST([x].[a] AS INT)
            , [casedefid]            = CAST([x].[a] AS INT)
            , [controller]           = CAST([x].[a] AS NVARCHAR(50))
            , [inputname]            = CAST([x].[a] AS NVARCHAR(50))
            , [calendarid]           = CAST([x].[a] AS INT)
            , [internalstate]        = CAST([x].[a] AS INT)
            , [promisedsolutiontime] = CAST([x].[a] AS DATETIME)
            , [referencenumber]      = CAST([x].[a] AS NVARCHAR(50))
            , [registrationtime]     = CAST([x].[a] AS DATETIME)
            , [reservedby]           = CAST([x].[a] AS INT)
            , [shape]                = CAST([x].[a] AS INT)
            , [resubmissiontime]     = CAST([x].[a] AS DATETIME) --<-- since @v64up2: 2018.11.27
            , [directrouting]        = CAST([x].[a] AS BIT)
            , [version]              = CAST([x].[a] AS INT)
            , [lastmodified]         = CAST([x].[a] AS DATETIME)
            , [subject]              = CAST([x].[a] AS NVARCHAR(MAX))
            , [description]          = CAST([x].[a] AS NVARCHAR(MAX))
            , [solution]             = CAST([x].[a] AS NVARCHAR(MAX))
       FROM (VALUES(CAST(N'### DB ARTIFACTS WERE NOT REBUILT AFTER DEPLOY! ###' AS BIT))) AS [x]([a]) -- use Server.RebuildModelApp.exe