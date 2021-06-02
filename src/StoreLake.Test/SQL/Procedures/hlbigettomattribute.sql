-- =============================================
-- Author:		Tillmann Eitelberg
-- Create date: 26.01.2016
-- Description:	Lädt alle für die Synchronisation benötigten Informationen zu den Attributen/Spalten
-- =============================================
CREATE PROCEDURE [dbo].[hlbigettomattribute]
	@tableName NVARCHAR(255)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	SELECT DISTINCT [hlbi].[id]
		, [hlbi].[objectdefinition]
		, [hlbi].[attributepath]
		, [attr].[description]
		--, IIF(hlbi.[description] IS NULL or hlbi.[description] = '', hlbi.[columnName], hlbi.[description]) AS ExternalDescription
		, IIF([hlbi].[description] IS NULL OR [hlbi].[description] = N'', hlbi.attributepath, [hlbi].[description]) AS ExternalDescription
		, [hlbi].[tablename] AS tablename
		, attr.ColumnName 
		, [hlbi].[columnname] AS ExternalColumnName
		, systyp.name AS DataType
		, (SELECT HASHBYTES(N'SHA2_512', 
				CONCAT(systab.name, syscol.name, systyp.name, syscol.max_length, syscol.precision, hlbi.attributepath, [hlbi].[description])) AS Hashvalue
				FROM sys.tables AS systab
				INNER JOIN sys.columns AS syscol ON systab.object_id = syscol.object_id
				INNER JOIN sys.systypes AS systyp ON systyp.xusertype = syscol.user_type_id
				WHERE systab.name = @tableName AND syscol.name = hlbi.columnname) AS HashValue
		, attr.CreatedOn
		, attr.ModifiedOn
		, attr.DeletedOn
		, attr.Deleted
		, CASE 
			WHEN attr.HashValue IS NULL THEN N'New'
			WHEN attr.HashValue != (SELECT HASHBYTES(N'SHA2_512', 
				CONCAT(systab.name, syscol.name, systyp.name, syscol.max_length, syscol.precision, hlbi.attributepath, [hlbi].[description])) AS Hashvalue
				FROM sys.tables AS systab
				INNER JOIN sys.columns AS syscol ON systab.object_id = syscol.object_id
				INNER JOIN sys.systypes AS systype ON systyp.xusertype = syscol.user_type_id
				WHERE systab.name = @tableName AND syscol.name = hlbi.columnname) THEN N'Update'
			ELSE N'Unchanged'
		END AS [Status]
		FROM [dbo].[hlbiattributeconfig] AS hlbi
		INNER JOIN sys.tables AS systab
			ON hlbi.tablename = systab.name
		INNER JOIN sys.columns AS syscol
			ON systab.object_id = syscol.object_id
			AND syscol.name = hlbi.columnname
		INNER JOIN sys.systypes AS systyp
			ON systyp.xusertype = syscol.system_type_id
		LEFT JOIN (SELECT xy.attributeconfigid, xy.attributepath,  xy.columnname, xy.createdon, xy.datatype, xy.deleted, xy.deletedon, xy.description, xy.hashvalue, xy.hlbiattributeconfigid, xy.modifiedon, xy.objectdefinition, xy.tablename  FROM [dbo].[hlbitomattribute] AS xy WHERE deleted = 0 AND tablename = @tableName) attr
		--ON hlbi.Id = attr.hlbiattributeconfigId
		ON hlbi.objectdefinition = attr.objectdefinition
		AND hlbi.attributepath = attr.attributepath
		AND hlbi.tablename = attr.tablename
		WHERE hlbi.tablename = @tableName
		UNION
		SELECT hlbiattributeconfigid
			, objectdefinition
			, attributepath
			, [description]
			, [description] AS ExternalDescription
			, tablename AS tablename
			, columnname
			, columnname AS ExternalColumnName
			, datatype
			, hashvalue
			, createdon
			, modifiedon
			, deletedon
			, deleted
			, N'Delete' AS [Status]
		FROM [dbo].[hlbitomattribute] AS hlbi
		WHERE (SELECT COUNT(id) FROM [dbo].[hlbiattributeconfig] AS x WHERE x.attributepath = hlbi.attributepath AND x.tablename = hlbi.tablename AND x.objectdefinition = hlbi.objectdefinition) = 0
		AND deleted = 0
		AND hlbi.tablename = @tableName
END

GO
