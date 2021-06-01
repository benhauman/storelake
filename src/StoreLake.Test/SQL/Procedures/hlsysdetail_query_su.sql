-- @Namespace ObjectManagement.Details
-- @Name GetSUDetails
-- @Return ClrTypes:ObjectManagement.Details.SUDetailValue Name:Values
-- @Return ObjectManagement.Details.SUDetailAttachment Name:Attachments
CREATE 
--OR ALTER
PROCEDURE [dbo].[hlsysdetail_query_su] @casedefid INT, @caseid INT, @agentid INT, @lcid INT, @channelid SMALLINT, @prefertextvalue BIT NULL = NULL
AS
BEGIN
    SET NOCOUNT ON
    
    DECLARE @prefertextvalueresolved BIT = ISNULL(@prefertextvalue, IIF(@channelid = 212 /* WebDesk */, 1, 0))
    DECLARE @detailcfgid INT = (SELECT TOP 1 [detailcfgid] FROM [dbo].[hlsysdetailcfg_query_id] (@casedefid, @agentid))

    SELECT [suid]                  = [su].[suid]
         , [suindex]               = DENSE_RANK() OVER(ORDER BY [su].[suid])
         , [type]                  = [f].[type]
         , [kind]                  = [f].[kind]
         , [displayname]           = [f].[displayname]
         , [datatype]              = [f].[datatype]
         , [valuebit]              = [f].[valuebit]
         , [valueint]              = [f].[valueint]
         , [valuedecimal]          = [f].[valuedecimal]
         , [valuedatetime]         = [f].[valuedatetime]
         , [valuenvarchar]         = [f].[valuenvarchar]
         , [associatedobjectdefid] = [f].[associatedobjectdefid]
         , [associatedobjectid]    = [f].[associatedobjectid]
         , [hasdisplayvalue]       = [f].[hasdisplayvalue]
         , [displayvalue]          = [f].[displayvalue]
         , [displayregion]         = [f].[displayregion]
         , [sortorder]             = [f].[sortorder]
    FROM [dbo].[hlsyssusystem] AS [su]
    OUTER APPLY (
        -- Attributes
        SELECT [type]                  = [fa].[type]
             , [kind]                  = [fa].[kind]
             , [displayname]           = [fa].[displayname]
             , [datatype]              = [fa].[datatype]
             , [valuebit]              = [fa].[valuebit]
             , [valueint]              = [fa].[valueint]
             , [valuedecimal]          = [fa].[valuedecimal]
             , [valuedatetime]         = [fa].[valuedatetime]
             , [valuenvarchar]         = [fa].[valuenvarchar]
             , [associatedobjectdefid] = [fa].[associatedobjectdefid]
             , [associatedobjectid]    = [fa].[associatedobjectid]
             , [hasdisplayvalue]       = [fa].[hasdisplayvalue]
             , [displayvalue]          = [fa].[displayvalue]
             , [displayregion]         = [fa].[displayregion]
             , [sortorder]             = [fa].[sortorder]
        FROM (
            SELECT [type]                  = 1
                 , [kind]                  = CASE WHEN [a].[iscomplextext] = 1 THEN 80
                                                  WHEN [a].[ismultilingual] = 1 THEN 81
                                                  ELSE [apo].[attr_type]
                                             END
                 , [displayname]           = IIF([ap].[iscomplextext] = 1, ISNULL([adp].[displayname], [a].[name]), ISNULL([ad].[displayname], [a].[name]))
                 , [datatype]              = [v].[datatype]
                 , [valuebit]              = [v].[value_bit]
                 , [valueint]              = [v].[value_int]
                 , [valuedecimal]          = [v].[value_decimal]
                 , [valuedatetime]         = CAST([v].[value_datetime] AS DATETIMEOFFSET)
                 , [valuenvarchar]         = [v].[value_nvarchar]
                 , [associatedobjectdefid] = NULL
                 , [associatedobjectid]    = NULL
                 , [hasdisplayvalue]       = IIF([v].[displaytype] <> 0, 1, 0)
                 , [displayvalue]          = CASE WHEN [v].[displaytype] = 1 OR [v].[displaytype] = 2 OR [v].[displaytype] = 4 THEN [d].[displayname]
                                                  WHEN [v].[displaytype] = 3 THEN ISNULL(NULLIF([ag].[fullname], N''), [ag].[name])
                                                  ELSE NULL
                                             END
                 , [displayregion]         = [f].[displayregion]
                 , [sortorder]             = [f].[sortorder]
                 , [fieldpartition] = ROW_NUMBER() OVER(PARTITION BY [f].[displayregion], [v].[suid], [v].[fieldid] ORDER BY [v].[contentid])
            FROM [dbo].[hlsysdetail_query_view_su] ([su].[sudefid], [su].[suid], @lcid, @prefertextvalueresolved) AS [v]
            INNER JOIN [dbo].[hlsysdetailcfgfieldattr] AS [f] ON [f].[detailcfgid] = @detailcfgid AND [v].[fieldid] = [f].[attrpathid]
            INNER JOIN [dbo].[hlsysattrpathodedef] AS [apo] ON [f].[attrpathid] = [apo].[attrpathid]
            INNER JOIN [dbo].[hlsysattributedef] AS [a] ON [apo].[attr_defid] = [a].[attrdefid]
            LEFT JOIN [dbo].[hlsysattributedef] AS [ap] ON [apo].[parent_defid] = [ap].[attrdefid]
            LEFT JOIN [dbo].[hlsysdisplayname] AS [ad] ON [apo].[attr_defid] = [ad].[reposid] AND [ad].[languageid] = @lcid
            LEFT JOIN [dbo].[hlsysdisplayname] AS [adp] ON [apo].[parent_defid] = [adp].[reposid] AND [adp].[languageid] = @lcid
            LEFT JOIN [dbo].[hlsysdisplayname] AS [d] ON ([v].[displaytype] = 1 OR [v].[displaytype] = 2 OR [v].[displaytype] = 4) AND [v].[value_int] = [d].[reposid] AND [d].[languageid] = @lcid
            LEFT JOIN [dbo].[hlsysagent] AS [ag] ON [v].[displaytype] = 3 AND [ag].[agentid] = [v].[value_int]
            CROSS APPLY [dbo].[hlsyssec_query_agentcaseprmread] (@agentid, @caseid, @casedefid) AS [sec]
            WHERE [sec].[canread] = 1
        ) AS [fa]
        WHERE [fa].[fieldpartition] = 1 -- Partition fields since we support multiple attributes which will result in selecting the first item
        UNION ALL
        -- SU associations
        SELECT [type]                  = 2
             , [kind]                  = [f].[kind]
             , [displayname]           = ISNULL([d].[displayname], [ad].[name])
             , [datatype]              = 6
             , [valuebit]              = NULL
             , [valueint]              = NULL
             , [valuedecimal]          = NULL
             , [valuedatetime]         = NULL
             , [valuenvarchar]         = NULL
             , [associatedobjectdefid] = [suo].[objectdefid]
             , [associatedobjectid]    = [suo].[objectid]
             , [hasdisplayvalue]       = 1
             , [displayvalue]          = [suo].[defaultvalue]
             , [displayregion]         = [f].[displayregion]
             , [sortorder]             = [f].[sortorder]
        FROM (
             SELECT [associationdefid] = [f].[associationdefid]
                  , [objecttypeb]      = [f].[objecttypeb]
                  , [kind]             = CASE WHEN [f].[associationdefid] = 130 AND [f].[objecttypeb] = 3 THEN 82 -- Customer
                                              WHEN [f].[associationdefid] = 130 AND [f].[objecttypeb] = 4 THEN 83 -- Organisation
                                              WHEN [f].[associationdefid] = 131                           THEN 84 -- Product
                                         END
                  , [displayregion]    = [f].[displayregion]
                  , [sortorder]        = [f].[sortorder]
             FROM [dbo].[hlsysdetailcfgfieldassoc] AS [f]
             WHERE [f].[detailcfgid] = @detailcfgid AND ([f].[displayregion] = 4 OR [f].[displayregion] = 5)
        ) AS [f]
        INNER JOIN [dbo].[hlsysassociationdef] AS [ad] ON [f].[associationdefid] = [ad].[associationdefid]
        OUTER APPLY (
            SELECT [objectdefid]  = [sual].[objectdefidb]
                 , [objectid]     = [sual].[objectidb]
                 , [defaultvalue] = [v].[defaultvalue]
            FROM [dbo].[hlsyssuassociation] AS [sual]
            CROSS APPLY [dbo].[hlsyssec_query_agentobjectprmread] (@agentid, [sual].[objectidb], [sual].[objectdefidb], [sual].[objecttypeb] /* 3, 4 or 5 */ + 783) AS [s]
            OUTER APPLY (
                SELECT [v].[defaultvalue]
                FROM [dbo].[hlsysdefaultattr_query_person] ([sual].[objectdefidb], [sual].[objectidb], @lcid) AS [v]
                WHERE [sual].[objecttypeb] = 3
                UNION ALL
                SELECT [v].[defaultvalue]
                FROM [dbo].[hlsysdefaultattr_query_orgunit] ([sual].[objectdefidb], [sual].[objectidb], @lcid) AS [v]
                WHERE [sual].[objecttypeb] = 4
                UNION ALL
                SELECT [v].[defaultvalue]
                FROM [dbo].[hlsysdefaultattr_query_product] ([sual].[objectdefidb], [sual].[objectidb], @lcid) AS [v]
                WHERE [sual].[objecttypeb] = 5
            ) AS [v]
            WHERE [sual].[suid]             = [su].[suid]
              AND [sual].[associationdefid] = [f].[associationdefid]
              AND [sual].[objecttypeb]      = [f].[objecttypeb]
              AND [s].[canread]             = 1
        ) AS [suo]
        LEFT JOIN [dbo].[hlsysdetailcfgfielddisplayname] AS [fd] ON [f].[kind] = [fd].[fieldkind]
        OUTER APPLY (
            SELECT TOP 1 [d].[displayname]
            FROM [dbo].[hlsys_query_culturepriority](@lcid, 1) AS [c]
            INNER JOIN [dbo].[hlsysdisplayname] AS [d] ON ISNULL([fd].[displaynameid], [f].[associationdefid]) = [d].[reposid] AND [c].[lcid] = [d].[languageid]
            ORDER BY [c].[index]
        ) AS [d]
    ) AS [f] 
    WHERE [su].[sudefid] = @casedefid AND [su].[caseid] = @caseid
    
    SELECT [a].[suid]
         , [a].[suindex]
         , [a].[blobid]
         , [a].[name]
         , [a].[blobsize]
         , [a].[blobtype]
    FROM [dbo].[hlsyssuattachmentvw] AS [a]
    WHERE [a].[sudefid] = @casedefid AND [a].[caseid] = @caseid AND ISNULL([a].[blobsize], 0) > 0 -- Links are not supported
END
--GO
--EXEC [dbo].[hlsysdetail_query_su] @casedefid = 100824, @caseid = 155774, @agentid = 710, @lcid = 1033, @channelid = 212