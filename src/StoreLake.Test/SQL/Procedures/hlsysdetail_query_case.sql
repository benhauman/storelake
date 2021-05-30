-- @Namespace ObjectManagement.Details
-- @Name GetCaseDetails
-- @MergeGridResult
-- @Return ClrTypes:ObjectManagement.Details.ObjectDetailResult Mode:Single
-- @Return ClrTypes:ObjectManagement.Details.ObjectDetailValue Name:Values
-- @Return ObjectManagement.Details.ObjectDetailAttachment Name:Attachments
CREATE 
--OR ALTER
PROCEDURE [dbo].[hlsysdetail_query_case] @casedefid INT, @caseid INT, @agentid INT, @lcid INT, @channelid SMALLINT, @prefertextvalue BIT NULL = NULL
AS
BEGIN
    SET NOCOUNT ON
    
    DECLARE @prefertextvalueresolved BIT = ISNULL(@prefertextvalue, IIF(@channelid = 212 /* WebDesk */, 1, 0))
    DECLARE @canread BIT = (SELECT TOP 1 CAST([canread] AS BIT) FROM [dbo].[hlsyssec_query_agentcaseprmread] (@agentid, @caseid, @casedefid))
    DECLARE @true BIT = 1
    
    IF @canread = 1
    BEGIN
        DECLARE @detailcfgid INT = (SELECT TOP 1 [detailcfgid] FROM [dbo].[hlsysdetailcfg_query_id] (@casedefid, @agentid))

        SELECT [hasconfiguration]            = IIF(@detailcfgid IS NOT NULL, 1, 0)
             , [objectdefinitiondisplayname] = ISNULL([d].[displayname], [o].[name])
        FROM [dbo].[hlsysobjectdef] AS [o]
        LEFT JOIN [dbo].[hlsysdisplayname] AS [d] ON [o].[objectdefid] = [d].[reposid] AND [d].[languageid] = @lcid
        WHERE [o].[objectdefid] = @casedefid
    
        ;WITH [attributefields] AS 
        (
            -- Join field configuration with runtime view
            SELECT [fieldid]        = [v].[fieldid]
                 , [datatype]       = [v].[datatype]
                 , [displaytype]    = [v].[displaytype]
                 , [value_bit]      = [v].[value_bit]
                 , [value_int]      = [v].[value_int]
                 , [value_decimal]  = [v].[value_decimal]
                 , [value_datetime] = [v].[value_datetime]
                 , [value_nvarchar] = [v].[value_nvarchar]
                 , [displayregion]  = [f].[displayregion]
                 , [sortorder]      = [f].[sortorder]
                 , [fieldpartition] = ROW_NUMBER() OVER(PARTITION BY [f].[displayregion], [v].[fieldid] ORDER BY [v].[contentid])
            FROM [dbo].[hlsysdetail_query_view_case] (@casedefid, @caseid, @lcid, @prefertextvalueresolved) AS [v]
            INNER JOIN [dbo].[hlsysdetailcfgfieldattr] AS [f] ON [f].[detailcfgid] = @detailcfgid AND [v].[fieldid] = [f].[attrpathid]
            WHERE [f].[displayregion] = 1 OR [f].[displayregion] = 2 OR [f].[displayregion] = 3
            UNION ALL
            -- Join default attribute for caption as fallback if no detail configuration is found
            SELECT TOP 1 [fieldid]        = [df].[defaultattrpathid]
                       , [datatype]       = 231
                       , [displaytype]    = 0
                       , [valuebit]       = NULL
                       , [valueint]       = NULL
                       , [valuedecimal]   = NULL
                       , [valuedatetime]  = NULL
                       , [valuenvarchar]  = [df].[defaultvalue]
                       , [displayregion]  = 1
                       , [sortorder]      = 1
                       , [fieldpartition] = 1
            FROM [dbo].[hlsysdefaultattr_query_case] (@casedefid, @caseid, @lcid) AS [df]
        ), [assocfields] AS (
            SELECT [associationdefid] = [f].[associationdefid]
                 , [objecttypeb]      = [f].[objecttypeb]
                 , [kind]             = CASE WHEN [f].[associationdefid] = 130 AND [f].[objecttypeb] = 3 THEN 82 -- Customer
                                             WHEN [f].[associationdefid] = 130 AND [f].[objecttypeb] = 4 THEN 83 -- Organisation
                                             WHEN [f].[associationdefid] = 131                           THEN 84 -- Product
                                        END
                 , [displayregion]    = [f].[displayregion]
                 , [sortorder]        = [f].[sortorder]
            FROM [dbo].[hlsysdetailcfgfieldassoc] AS [f]
            WHERE [f].[detailcfgid] = @detailcfgid AND ([f].[displayregion] = 1 OR [f].[displayregion] = 2 OR [f].[displayregion] = 3)
        ), [assocfieldsdisplayname] AS (
            SELECT [associationdefid] = [f].[associationdefid]
                 , [objecttypeb]      = [f].[objecttypeb]
                 , [kind]             = [f].[kind]
                 , [displaynameid]    = ISNULL([fd].[displaynameid], [f].[associationdefid])
                 , [displayregion]    = [f].[displayregion]
                 , [sortorder]        = [f].[sortorder]
            FROM [assocfields] AS [f]
            LEFT JOIN [dbo].[hlsysdetailcfgfielddisplayname] AS [fd] ON [f].[kind] = [fd].[fieldkind]
        )
        SELECT [type]                  = 1
             , [kind]                  = CASE WHEN [a].[iscomplextext]  = 1 THEN 80
                                              WHEN [a].[ismultilingual] = 1 THEN 81
                                              ELSE [apo].[attr_type]
                                         END
             , [displayname]           = IIF([ap].[iscomplextext] = 1, ISNULL([adp].[displayname], [ap].[name]), ISNULL([ad].[displayname], [a].[name]))
             , [datatype]              = [f].[datatype]
             , [valuebit]              = [f].[value_bit]
             , [valueint]              = [f].[value_int]
             , [valuedecimal]          = [f].[value_decimal]
             , [valuedatetime]         = CAST([f].[value_datetime] AS DATETIMEOFFSET)
             , [valuenvarchar]         = [f].[value_nvarchar]
             , [associatedobjectdefid] = NULL
             , [associatedobjectid]    = NULL
             , [hasdisplayvalue]       = IIF([f].[displaytype] <> 0, 1, 0)
             , [displayvalue]          = CASE [f].[displaytype] WHEN 1 THEN ISNULL([d].[displayname], [li].[name])
                                                                WHEN 2 THEN ISNULL([d].[displayname], [ti].[name])
                                                                WHEN 3 THEN ISNULL(NULLIF([ag].[fullname], N''), [ag].[name])
                                                                WHEN 4 THEN ISNULL([d].[displayname], [o].[name])
                                         END
             , [displayregion]         = [f].[displayregion]
             , [sortorder]             = [f].[sortorder]
        FROM [attributefields] AS [f]
        INNER JOIN [dbo].[hlsysattrpathodedef] AS [apo] ON [f].[fieldid] = [apo].[attrpathid]
        INNER JOIN [dbo].[hlsysattributedef] AS [a] ON [apo].[attr_defid] = [a].[attrdefid]
        LEFT JOIN [dbo].[hlsyslistitem] AS [li] ON [f].[displaytype] = 1 AND [f].[value_int] = [li].[listitemid]
        LEFT JOIN [dbo].[hlsystreeitem] AS [ti] ON [f].[displaytype] = 2 AND [f].[value_int] = [ti].[treeitemid]
        LEFT JOIN [dbo].[hlsysobjectdef] AS [o] ON [f].[displaytype] = 4 AND [f].[value_int] = [o].[objectdefid]
        LEFT JOIN [dbo].[hlsysattributedef] AS [ap] ON [apo].[parent_defid] = [ap].[attrdefid]
        LEFT JOIN [dbo].[hlsysdisplayname] AS [ad] ON [apo].[attr_defid] = [ad].[reposid] AND [ad].[languageid] = @lcid
        LEFT JOIN [dbo].[hlsysdisplayname] AS [adp] ON [apo].[parent_defid] = [adp].[reposid] AND [adp].[languageid] = @lcid
        LEFT JOIN [dbo].[hlsysdisplayname] AS [d] ON ([f].[displaytype] = 1 OR [f].[displaytype] = 2 OR [f].[displaytype] = 4) AND [f].[value_int] = [d].[reposid] AND [d].[languageid] = @lcid
        LEFT JOIN [dbo].[hlsysagent] AS [ag] ON [f].[displaytype] = 3 AND [ag].[agentid] = [f].[value_int]
        WHERE [f].[fieldpartition] = 1
        UNION ALL
        SELECT [type]                  = 2
             , [kind]                  = [f].[kind]
             , [displayname]           = ISNULL([d].[displayname], [ad].[name])
             , [datatype]              = 6
             , [valuebit]              = NULL
             , [valueint]              = NULL
             , [valuedecimal]          = NULL
             , [valuedatetime]         = NULL
             , [valuenvarchar]         = NULL
             , [associatedobjectdefid] = [fa].[objectdefid]
             , [associatedobjectid]    = [fa].[objectid]
             , [hasdisplayvalue]       = 1
             , [displayvalue]          = [fa].[defaultvalue]
             , [displayregion]         = [f].[displayregion]
             , [sortorder]             = [f].[sortorder]
        FROM [assocfieldsdisplayname] AS [f]
        INNER JOIN [dbo].[hlsysassociationdef] AS [ad] ON [f].[associationdefid] = [ad].[associationdefid]
        OUTER APPLY (
            SELECT TOP 1 [objectdefid]  = [sual].[objectdefidb]
			           , [objectid]     = [sual].[objectidb]
					   , [defaultvalue] = [v].[defaultvalue]
            FROM [dbo].[hlsyssusystem] AS [su] 
            INNER JOIN [dbo].[hlsyssuassociation] AS [sual] ON [sual].[suid]             = [su].[suid]
                                                           AND [sual].[associationdefid] = [f].[associationdefid]
                                                           AND [sual].[objecttypeb]      = [f].[objecttypeb]
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
            WHERE [su].[caseid]  = @caseid
              AND [su].[sudefid] = @casedefid
              AND [s].[canread]  = 1
            ORDER BY ROW_NUMBER() OVER(PARTITION BY [sual].[objecttypeb] ORDER BY [su].[suid] DESC)
        ) AS [fa]
        OUTER APPLY (
            SELECT TOP 1 [d].[displayname]
            FROM [dbo].[hlsys_query_culturepriority](@lcid, 1) AS [c]
            INNER JOIN [dbo].[hlsysdisplayname] AS [d] ON [f].[displaynameid] = [d].[reposid] AND [c].[lcid] = [d].[languageid]
            ORDER BY [c].[index]
        ) AS [d]
        ORDER BY [f].[displayregion], [f].[sortorder] -- Only required because of default attribute fallback priority
    END
    ELSE
    BEGIN
        SELECT [hasconfiguration]            = 0
             , [objectdefinitiondisplayname] = NULL

        SELECT [type]				   = NULL
             , [kind]				   = NULL
             , [displayname]		   = NULL
             , [datatype]			   = NULL
             , [valuebit]			   = NULL
             , [valueint]			   = NULL
             , [valuedecimal]		   = NULL
             , [valuedatetime]		   = NULL
             , [valuenvarchar]		   = NULL
             , [associatedobjectdefid] = NULL
             , [associatedobjectid]	   = NULL
             , [hasdisplayvalue]	   = NULL
             , [displayvalue]		   = NULL
             , [displayregion]		   = NULL
             , [sortorder]			   = NULL
        WHERE @true = 0
    END
    
    SELECT [a].[blobid]
         , [a].[name]
         , [a].[blobsize]
         , [a].[blobtype]
    FROM [dbo].[hlsyscaseattachmentvw] AS [a]
    WHERE [a].[casedefid] = @casedefid AND [a].[caseid] = @caseid AND ISNULL([a].[blobsize], 0) > 0 -- Links are not supported
END
--GO
--EXEC [dbo].[hlsysdetail_query_case] @casedefid = 100824, @caseid = 158858, @agentid = 710, @lcid = 1033, @channelid = 212