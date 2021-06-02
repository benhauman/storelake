CREATE PROCEDURE [dbo].[hlbigettablemetadata]
	@tableName NVARCHAR(255)
AS
SELECT	systbl.name AS TableName
		, syscol.name AS ColumnName
		, systyp.name AS TypeName
		, syscol.max_length AS [MaxLength]
		, [syscol].[precision] AS [Precision]
		, HASHBYTES(N'SHA2_512', CONCAT(systbl.name, syscol.name, systyp.name, syscol.max_length, syscol.precision)) AS Hashvalue
	FROM sys.tables AS systbl
	INNER JOIN sys.columns AS syscol ON systbl.object_id = syscol.object_id
	INNER JOIN sys.systypes AS systyp ON systyp.xusertype = syscol.user_type_id
	WHERE systbl.name = @tableName
RETURN 0
