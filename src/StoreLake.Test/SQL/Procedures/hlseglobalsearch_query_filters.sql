-- @Name GlobalSearchFilters
-- @Return ClrTypes:FilterDescription;FilterSuggestion SplitOn:name
CREATE
--OR ALTER
PROCEDURE [dbo].[hlseglobalsearch_query_filters]
    /* @ClrType SearchSource */ @source     TINYINT
  ,                             @searchtext NVARCHAR(128)
  ,                             @agentid    INT
  ,                             @lcid       INT
AS
    SET NOCOUNT ON

    IF @source = 1 -- Case
    BEGIN
        SELECT [name]        = [fx].[field_name]
             , [displayname] = [fx].[field_displayname]
             , [kind]        = [fx].[value_kind]
             , [name]        = [fx].[value_name]
             , [displayname] = [fx].[value_displayname]
             , [imageurl]    = [fx].[imageurl]
             , [count]       = [f].[count]
        FROM (
		    SELECT [kind] = MIN([x].[kind]), [x].[defid], [count] = COUNT(1)
            FROM [dbo].[hlsysobjectdata] AS [s]
            INNER JOIN [dbo].[hlsyscasesystem] AS [c] ON [s].[objectid] = [c].[caseid]
            CROSS APPLY [dbo].[hlsyssec_query_agentcaseprmreadsearch](@agentid, [s].[objectid], [s].[objectdefid]) AS [sec]
            CROSS APPLY (
                SELECT [kind]  = 1 -- ObjectDefinition
                     , [defid] = [s].[objectdefid]
                UNION ALL
                SELECT [kind]  = 2 -- ListItem
                     , [defid] = [c].[internalstate]
            ) AS [x]
            WHERE CONTAINS([s].[objectdata], @searchtext)
              AND [sec].[canreadandsearch] = 1
              AND [s].[objecttype] = 2
            GROUP BY [x].[defid]
		) AS [f]
        CROSS APPLY (
            SELECT [priority]          = 1
                 , [field_name]        = N'ObjectDefinition'
                 , [field_displayname] = [fd].[displayname]
                 , [value_kind]        = 1 -- List
                 , [value_name]        = [o].[name]
                 , [value_displayname] = [vd].[displayname]
                 , [imageurl]          = CONCAT(N'/FrontendServices/Metadata/api/Image/ObjectDef/', [o].[objectdefid], N'_small.png')
            FROM (VALUES (1)) AS [x]([kind])
            INNER JOIN [dbo].[hlsysobjectdef] AS [o] ON [f].[defid] = [o].[objectdefid]
            CROSS APPLY (
                SELECT TOP 1 [d].[displayname]
                FROM [dbo].[hlsys_query_culturepriority](@lcid, 1) AS [c]
                INNER JOIN [dbo].[hlsysdisplayname] AS [d] ON [d].[reposid] = 17 /* TYPE */ AND [c].[lcid] = [d].[languageid]
                ORDER BY [c].[index]
            ) AS [fd]
            CROSS APPLY (
                SELECT TOP 1 [d].[displayname]
                FROM [dbo].[hlsys_query_culturepriority](@lcid, 1) AS [c]
                INNER JOIN [dbo].[hlsysdisplayname] AS [d] ON [o].[objectdefid] = [d].[reposid] AND [c].[lcid] = [d].[languageid]
                ORDER BY [c].[index]
            ) AS [vd]
            WHERE [x].[kind] = [f].[kind]
            UNION ALL
            SELECT [priority]          = 2
                 , [field_name]        = N'State'
                 , [field_displayname] = [fd].[displayname]
                 , [kind]              = 1 -- List
                 , [value_name]        = [li].[name]
                 , [value_displayname] = [vd].[displayname]
                 , [imageurl]          = CONCAT(N'/FrontendServices/Metadata/api/Image/ListItem/', [li].[listitemid], N'_small.png')
            FROM (VALUES (2)) AS [x]([kind])
            INNER JOIN [dbo].[hlsyslistitem] AS [li] ON [f].[defid] = [li].[listitemid]
            CROSS APPLY (
                SELECT TOP 1 [d].[displayname]
                FROM [dbo].[hlsys_query_culturepriority](@lcid, 1) AS [c]
                INNER JOIN [dbo].[hlsysdisplayname] AS [d] ON [d].[reposid] = 32 /* INTERNALSTATE */ AND [c].[lcid] = [d].[languageid]
                ORDER BY [c].[index]
            ) AS [fd]
            CROSS APPLY (
                SELECT TOP 1 [d].[displayname]
                FROM [dbo].[hlsys_query_culturepriority](@lcid, 1) AS [c]
                INNER JOIN [dbo].[hlsysdisplayname] AS [d] ON [li].[listitemid] = [d].[reposid] AND [c].[lcid] = [d].[languageid]
                ORDER BY [c].[index]
            ) AS [vd]
            WHERE [x].[kind] = [f].[kind]
        ) AS [fx]
        ORDER BY [fx].[priority] ASC, [f].[count] DESC
    END
    ELSE IF @source = 2 -- Person
         OR @source = 3 -- OrgUnit
         OR @source = 4 -- Product
    BEGIN
        SELECT [name]        = [fx].[field_name]
             , [displayname] = [fx].[field_displayname]
             , [kind]        = [fx].[value_kind]
             , [name]        = [fx].[value_name]
             , [displayname] = [fx].[value_displayname]
             , [imageurl]    = [fx].[imageurl]
             , [count]       = [f].[count]
        FROM (
		    SELECT [kind] = MIN([x].[kind]), [x].[defid], [count] = COUNT(1)
            FROM [dbo].[hlsysobjectdata] AS [s]
            CROSS APPLY [dbo].[hlsyssec_query_agentobjectprmreadsearch](@agentid, [s].[objectid], [s].[objectdefid], 783 + [s].[objecttype]) AS [sec]
            CROSS APPLY (
                SELECT [kind]  = 1 -- ObjectDefinition
                     , [defid] = [s].[objectdefid]
            ) AS [x]
            WHERE CONTAINS([s].[objectdata], @searchtext)
              AND [sec].[canreadandsearch] = 1
              AND [s].[objecttype] = CASE @source WHEN 2 THEN 3
                                                  WHEN 3 THEN 4
                                                  WHEN 4 THEN 5
                                     END
            GROUP BY [x].[defid]
		) AS [f]
        CROSS APPLY (
            SELECT [priority]          = 1
                 , [field_name]        = N'ObjectDefinition'
                 , [field_displayname] = [fd].[displayname]
                 , [value_kind]        = 1 -- List
                 , [value_name]        = [o].[name]
                 , [value_displayname] = [vd].[displayname]
                 , [imageurl]          = CONCAT(N'/FrontendServices/Metadata/api/Image/ObjectDef/', [o].[objectdefid], N'_small.png')
            FROM (VALUES (1)) AS [x]([kind])
            INNER JOIN [dbo].[hlsysobjectdef] AS [o] ON [f].[defid] = [o].[objectdefid]
            CROSS APPLY (
                SELECT TOP 1 [d].[displayname]
                FROM [dbo].[hlsys_query_culturepriority](@lcid, 1) AS [c]
                INNER JOIN [dbo].[hlsysdisplayname] AS [d] ON [d].[reposid] = 17 /* TYPE */ AND [c].[lcid] = [d].[languageid]
                ORDER BY [c].[index]
            ) AS [fd]
            CROSS APPLY (
                SELECT TOP 1 [d].[displayname]
                FROM [dbo].[hlsys_query_culturepriority](@lcid, 1) AS [c]
                INNER JOIN [dbo].[hlsysdisplayname] AS [d] ON [o].[objectdefid] = [d].[reposid] AND [c].[lcid] = [d].[languageid]
                ORDER BY [c].[index]
            ) AS [vd]
            WHERE [x].[kind] = [f].[kind]
        ) AS [fx]
        ORDER BY [fx].[priority] ASC, [f].[count] DESC
    END
    ELSE IF @source = 5 -- Task
    BEGIN
        -- Filter descriptions
        -- BR: for now no filter needed for Task Management
        DECLARE @true BIT = 1
        SELECT [name]        = NULL
             , [displayname] = NULL
             , [kind]        = NULL
             , [name]        = NULL
             , [displayname] = NULL
             , [imageurl]    = NULL
             , [count]       = NULL
        WHERE @true = 0

        /*
        SELECT [name]        = [fx].[field_name]
             , [displayname] = [fx].[field_displayname]
             , [kind]        = [fx].[value_kind]
             , [name]        = [fx].[value_name]
             , [displayname] = [fx].[value_displayname]
             , [imageurl]    = [fx].[imageurl]
             , [count]       = [f].[count]
        FROM (
		    SELECT [kind] = MIN([x].[kind]), [x].[defid], [count] = COUNT(1)
            FROM [dbo].[hltmtasksearchdata] AS [s]
            INNER JOIN [dbo].[hltmtaskaggregate] AS [t] ON [s].[id] = [t].[taskguid]
            INNER JOIN [dbo].[hltmusertaskaggregate] AS [ut] ON [t].[taskid] = [ut].[taskid]
            CROSS APPLY (
                SELECT [kind]  = 1 -- State
                     , [defid] = [ut].[state]
            ) AS [x]
            WHERE CONTAINS([s].[searchdata], @searchtext)
            GROUP BY [x].[defid]
		) AS [f]
        CROSS APPLY (
            SELECT [priority]          = 1
                 , [field_name]        = N'State'
                 , [field_displayname] = [fd].[displayname]
                 , [value_kind]        = 1 -- List
                 , [value_name]        = [ts].[name]
                 , [value_displayname] = [ts].[name] -- TODO: Collect displaynames into hlsysdisplayname
                 , [imageurl]          = NULL
            FROM (VALUES (1)) AS [x]([kind])
            CROSS APPLY (
                SELECT TOP 1 [d].[displayname]
                FROM [dbo].[hlsys_query_culturepriority](@lcid, 1) AS [c]
                -- TODO: Maybe store these in hlsysdisplayname. Known key? Known reposid?
                INNER JOIN (VALUES (0,  N'State')
                                 , (7,  N'Status')
                                 , (9,  N'Status')
                                 , (12, N'Statut')
                                 , (16, N'Stato')) AS [d]([languageid], [displayname]) ON [c].[lcid] = [d].[languageid]
                ORDER BY [c].[index]
            ) AS [fd]
            INNER JOIN @taskstate AS [ts] ON [f].[defid] = [ts].[state]
            WHERE [x].[kind] = [f].[kind]
        ) AS [fx]
        ORDER BY [fx].[priority] ASC, [f].[count] DESC
        */
    END
    ELSE
    BEGIN
        DECLARE @errormessage NVARCHAR(100) = CONCAT(N'Invalid search source: ', @source)
        ;THROW 400001, @errormessage, 1 -- 400 Bad Request

        SELECT [name]        = NULL
             , [displayname] = NULL
             , [kind]        = NULL
             , [name]        = NULL
             , [displayname] = NULL
             , [imageurl]    = NULL
             , [count]       = NULL
        WHERE @errormessage IS NOT NULL
    END