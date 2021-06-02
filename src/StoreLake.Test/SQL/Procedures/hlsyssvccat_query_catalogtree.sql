-- @Namespace Runtime.Legacy
-- @Name GetTreeCatalog
-- @Return ClrTypes:Runtime.Legacy.CatalogCategory Name:Categories
-- @Return ClrTypes:Runtime.Legacy.CatalogProduct Name:Products
-- @GeneratedResultTypeName Runtime.Legacy.CatalogTreeResult
CREATE PROCEDURE [dbo].[hlsyssvccat_query_catalogtree] 
(
	@parentcategoryid UNIQUEIDENTIFIER NULL
  , @personid INT
  , @persondefid INT
  , @lcid INT
  , @filter NVARCHAR(1000)
  , @skip INT
  , @take INT
  , /* @ClrType Runtime.Legacy.SortColumn */ @sortcolumn TINYINT
  , /* @ClrType Runtime.Legacy.SortDirection */ @sortdirection TINYINT
)
AS
BEGIN
    SET NOCOUNT ON

	--DECLARE @parentcategoryid UNIQUEIDENTIFIER
	--DECLARE @personid INT = 100063
	--DECLARE @persondefid INT = 100307
	--DECLARE @lcid INT = 1033
	--DECLARE @filter NVARCHAR(1000) = N'TL_Name_'
	--DECLARE @skip INT = 0
	--DECLARE @take INT = 1000
	--DECLARE @sortcolumn TINYINT = 4 -- CreationTime
	--DECLARE @sortdirection TINYINT = 1 -- Descending
	
	DECLARE @filterset BIT

	IF NULLIF(@filter, N'') IS NULL
	BEGIN
		SET @filterset = 0
	END
	ELSE
	BEGIN
		SET @filterset = 1
		SET @filter = CONCAT(N'%', @filter, N'%')
	END

	-- Categories
	-- Security filtered
	DECLARE @categories TABLE([categoryid] UNIQUEIDENTIFIER NOT NULL, [parentcategoryid] UNIQUEIDENTIFIER NULL, [selectproducts] BIT NOT NULL, PRIMARY KEY([categoryid]))
	;WITH [categorytree_down] AS
	(
		SELECT [cp].[id] AS [categoryid], [cp].[parentcategoryid], IIF(@parentcategoryid IS NULL OR [cp].[id] = @parentcategoryid, 1, 0) AS [selectproducts]
		FROM [dbo].[hlsyssvccatcategory] AS [cp]
		CROSS APPLY [dbo].[hlsyssvccatcategory_isvisible]([cp].[id], @persondefid, @personid) AS [cpo]
		WHERE [cp].[parentcategoryid] IS NULL
		UNION ALL
		SELECT [cc].[id] AS [categoryid], [cc].[parentcategoryid], IIF([cp].[selectproducts] = 1 OR @parentcategoryid IS NULL OR [cc].[id] = @parentcategoryid, 1, 0) AS [selectproducts]
		FROM [dbo].[hlsyssvccatcategory] AS [cc]
		INNER JOIN [categorytree_down] AS [cp] ON [cc].[parentcategoryid] = [cp].[categoryid]
		CROSS APPLY [dbo].[hlsyssvccatcategory_isvisible]([cc].[id], @persondefid, @personid) AS [cpo]
	)
	INSERT INTO @categories ([categoryid], [parentcategoryid], [selectproducts])
	SELECT [categoryid], [parentcategoryid], [selectproducts]
	FROM [categorytree_down]

	-- Second recursion counting products (direct + indirect)
	;WITH [categorytree] AS
	(
		SELECT [cp].[categoryid]
			 , [cp].[parentcategoryid]
			 , (SELECT COUNT(*) 
				FROM [dbo].[hlsyssvccatproducttocat] AS [pc] 
				INNER JOIN [dbo].[hlsyssvccatproduct] AS [p] ON [pc].[products_id] = [p].[id]
				OUTER APPLY (
					SELECT TOP 1 [d].[displayname]
					FROM [dbo].[hlsys_query_culturepriority](@lcid, 1) AS [cp]
					INNER JOIN [dbo].[hlsysdisplayname] AS [d] ON [p].[displaynameid] = [d].[reposid] AND [cp].[lcid] = [d].[languageid]
					ORDER BY [cp].[index]
				) AS [dn]
				WHERE [pc].[categories_id] = [cp].[categoryid]
				  AND GETUTCDATE() BETWEEN ISNULL([p].[validfrom], -53690 /* MinValue */) AND ISNULL([p].[validto], 2958463 /* MaxValue */)
				  AND (@filterset = 0 
					OR ([dn].[displayname]             LIKE @filter
					 OR ISNULL([p].[brand], N'')       LIKE @filter
					 OR ISNULL([p].[tags], N'')        LIKE @filter 
					 OR ISNULL([p].[productcode], N'') LIKE @filter))
				) AS [productcount]
		FROM @categories AS [cp]
		UNION ALL
		SELECT [cc].[categoryid], [cc].[parentcategoryid], [cp].[productcount]
		FROM @categories AS [cc]
		INNER JOIN [categorytree] AS [cp] ON [cc].[categoryid] = [cp].[parentcategoryid]
	)
	SELECT [id]           = [ct].[categoryid]
	     , [parentid]     = [ct].[parentcategoryid]
		 , [displayname]  = MIN([dn].[displayname])
		 , [productcount] = SUM([ct].[productcount])
	FROM [categorytree] AS [ct]
	INNER JOIN [dbo].[hlsyssvccatcategory] AS [c] ON [ct].[categoryid] = [c].[id]
	OUTER APPLY (
		SELECT TOP 1 [d].[displayname]
		FROM [dbo].[hlsys_query_culturepriority](@lcid, 1) AS [cp]
		INNER JOIN [dbo].[hlsysdisplayname] AS [d] ON [c].[displaynameid] = [d].[reposid] AND [cp].[lcid] = [d].[languageid]
		ORDER BY [cp].[index]
	) AS [dn]
	WHERE [productcount] > 0
	GROUP BY [ct].[categoryid], [ct].[parentcategoryid]
	ORDER BY MIN([c].[sortorder])

	-- Products
	;WITH [products] AS (
		SELECT DISTINCT [pc].[products_id] AS [productid]
		FROM [dbo].[hlsyssvccatproducttocat] AS [pc]
		INNER JOIN @categories AS [c] ON [pc].[categories_id] = [c].[categoryid] AND [c].[selectproducts] = 1
	)
	SELECT [p].[id]
		 , CASE [p].[type] WHEN 1   THEN 0 -- Asset
		   		           WHEN 2   THEN 0 -- Asset
		   		           WHEN 3   THEN 0 -- Asset
		   		           WHEN 100 THEN 1 -- Service
		   END AS [kind]
		 , [displayname] = [dn].[displayname]
		 , [description] = [dd].[value]
		 , [p].[thumbnail]
		 , [processdefinitionid] = [pp].[processdefid]
		 , [isbundle] = IIF([p].[type] = 2, 1, 0)
		 , [p].[price]
		 , [p].[currency]
		 , [restrictions] = ISNULL([p].[hideprice], 0) * POWER(2, 0) + [p].[singleorderitem] * POWER(2, 1)
	FROM [products] AS [cp]
	INNER JOIN [dbo].[hlsyssvccatproduct] AS [p] ON [cp].[productid] = [p].[id]
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
	  AND (@filterset = 0 
		OR ([dn].[displayname]             LIKE @filter
         OR ISNULL([p].[brand], N'')       LIKE @filter
         OR ISNULL([p].[tags], N'')        LIKE @filter 
         OR ISNULL([p].[productcode], N'') LIKE @filter))
	ORDER BY CASE WHEN @sortcolumn = 1 AND @sortdirection = 0 THEN [dn].[displayname] END ASC
		   , CASE WHEN @sortcolumn = 1 AND @sortdirection = 1 THEN [dn].[displayname] END DESC
		   , CASE WHEN @sortcolumn = 2 AND @sortdirection = 0 THEN [p].[price]        END ASC
		   , CASE WHEN @sortcolumn = 2 AND @sortdirection = 1 THEN [p].[price]        END DESC
		   , CASE WHEN @sortcolumn = 3 AND @sortdirection = 0 THEN [p].[brand]        END ASC
		   , CASE WHEN @sortcolumn = 3 AND @sortdirection = 1 THEN [p].[brand]        END DESC
		   , CASE WHEN @sortcolumn = 4 AND @sortdirection = 0 THEN [p].[creationtime] END ASC
		   , CASE WHEN @sortcolumn = 4 AND @sortdirection = 1 THEN [p].[creationtime] END DESC
	OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY
END