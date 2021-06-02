-- @Namespace Runtime.Legacy
-- @Name GetFlatCatalog
-- @Return ClrTypes:Runtime.HierarchyItem Name:Hierarchy
-- @Return ClrTypes:Runtime.Legacy.CatalogItem Name:Items
-- @GeneratedResultTypeName Runtime.Legacy.CatalogFlatResult
CREATE PROCEDURE [dbo].[hlsyssvccat_query_catalogflat] @parentcategoryid UNIQUEIDENTIFIER NULL, @personid INT, @persondefid INT, @lcid INT
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @rootcategoryid UNIQUEIDENTIFIER = CAST(0x0 AS UNIQUEIDENTIFIER)

    IF @parentcategoryid IS NULL
        SET @parentcategoryid = @rootcategoryid


    -- Spread some bread crumbs :)
    ;WITH [categories] AS (
        SELECT [c].[id], [c].[parentcategoryid], 1 AS [depth]
        FROM [dbo].[hlsyssvccatcategory] AS [c]
        WHERE [c].[id] = @parentcategoryid
        UNION ALL
        SELECT [cp].[id], [cp].[parentcategoryid], [c].[depth] + 1 AS [depth]
        FROM [dbo].[hlsyssvccatcategory] AS [cp]
        INNER JOIN [categories] AS [c] ON [c].[parentcategoryid] = [cp].[id]
    )
    SELECT [id]          = [c].[id]
         , [displayname] = [dn].[displayname]
    FROM [categories] AS [ci]
    INNER JOIN [dbo].[hlsyssvccatcategory] AS [c] ON [ci].[id] = [c].[id]
    OUTER APPLY (
        SELECT TOP 1 [d].[displayname]
        FROM [dbo].[hlsys_query_culturepriority](@lcid, 1) AS [cp]
        INNER JOIN [dbo].[hlsysdisplayname] AS [d] ON [c].[displaynameid] = [d].[reposid] AND [cp].[lcid] = [d].[languageid]
        ORDER BY [cp].[index]
    ) AS [dn]
    ORDER BY [depth] DESC

    -- Catalog
    SELECT [x].[type]
         , [x].[id]
         , [x].[displayname]
         , [x].[description]
         , [x].[image]
         , [x].[processdefinitionid]
         , [x].[price]
         , [x].[currency]
         , [x].[restrictions]
    FROM (
        SELECT [type]                = IIF([c].[parentcategoryid] IS NULL, 1, 2) -- 1 = Catalog, 2 = Category
             , [id]                  = [c].[id]
             , [displayname]         = [dn].[displayname]
             , [description]         = [dd].[value]
             , [image]               = [c].[image]
             , [sortorder]           = [c].[sortorder]
             , [processdefinitionid] = NULL
             , [price]               = NULL
             , [currency]            = NULL
             , [restrictions]        = NULL
        FROM [dbo].[hlsyssvccatcategory] AS [c]
        CROSS APPLY [dbo].[hlsyssvccatcategory_isvisible]([c].[id], @persondefid, @personid) AS [co]
        OUTER APPLY (
            SELECT TOP 1 [d].[displayname]
            FROM [dbo].[hlsys_query_culturepriority](@lcid, 1) AS [cp]
            INNER JOIN [dbo].[hlsysdisplayname] AS [d] ON [c].[displaynameid] = [d].[reposid] AND [cp].[lcid] = [d].[languageid]
            ORDER BY [cp].[index]
        ) AS [dn]
        OUTER APPLY (
            SELECT TOP 1 [t].[value]
            FROM [dbo].[hlsys_query_culturepriority](@lcid, 1) AS [cp]
            INNER JOIN [dbo].[hlsyssvccatcategorydescription] AS [t] ON [t].[categoryid] = [c].[id] AND [cp].[lcid] = [t].[lcid]
            ORDER BY [cp].[index]
        ) AS [dd]
        WHERE ISNULL([c].[parentcategoryid], @rootcategoryid) = @parentcategoryid
        UNION ALL
        SELECT [type]                = CASE [p].[type] WHEN 1   THEN 3 -- Asset
                                                       WHEN 2   THEN 3 -- Asset
                                                       WHEN 3   THEN 3 -- Asset
                                                       WHEN 100 THEN 4 -- Service
                                       END
             , [id]                  = [p].[id]
             , [displayname]         = [dn].[displayname]
             , [description]         = [dd].[value]
             , [image]               = [p].[thumbnail]
             , [sortorder]           = 0
             , [processdefinitionid] = [pp].[processdefid] 
             , [price]               = [p].[price]
             , [currency]            = [p].[currency]
             , [restrictions]        = ISNULL([p].[hideprice], 0) * POWER(2, 0) + [p].[singleorderitem] * POWER(2, 1)
        FROM [dbo].[hlsyssvccatproduct] AS [p]
        INNER JOIN [dbo].[hlsyssvccatproducttocat] AS [pc] ON [p].[id] = [pc].[products_id] AND [pc].[categories_id] = @parentcategoryid
        OUTER APPLY (
            SELECT TOP 1 [d].[displayname]
            FROM [dbo].[hlsys_query_culturepriority](@lcid, 1) AS [cp]
            INNER JOIN [dbo].[hlsysdisplayname] AS [d] ON [p].[displaynameid] = [d].[reposid] AND [cp].[lcid] = [d].[languageid]
            ORDER BY [cp].[index]
        ) AS [dn]
        OUTER APPLY (
            SELECT TOP 1 [t].[value]
            FROM [dbo].[hlsys_query_culturepriority](@lcid, 1) AS [cp]
            INNER JOIN [dbo].[hlsyssvccatproductdescription] AS [t] ON [t].[productid] = [p].[id] AND [cp].[lcid] = [t].[lcid]
            ORDER BY [cp].[index]
        ) AS [dd]
        LEFT JOIN [dbo].[hlsyssvccatproductprocessdefinition] AS [pp] ON [p].[id] = [pp].[productid]
        WHERE GETUTCDATE() BETWEEN ISNULL([p].[validfrom], -53690 /* MinValue */) AND ISNULL([p].[validto], 2958463 /* MaxValue */)
    ) AS [x]
    ORDER BY [x].[type], [x].[sortorder], [x].[displayname]
END