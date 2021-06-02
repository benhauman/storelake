-- =============================================
-- Author:		Tillmann Eitelberg
-- Create date: 26.01.2016
-- Description:	Die Prozedur liefert die für die Synchronisation einet Tabelle benötigen Informationen zurück
-- =============================================
CREATE PROCEDURE [dbo].[hlbigettomtable]
	@tablename NVARCHAR(255)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	DECLARE @TableMetadata TABLE (TableName NVARCHAR(255), ColumnName NVARCHAR(255), TypeName NVARCHAR(255), MaxLenght INT, [Precision] INT, Hashvalue VARBINARY(8000))

	INSERT INTO @TableMetadata (TableName, ColumnName, TypeName, MaxLenght, Precision, Hashvalue)
	EXEC [dbo].[hlbigettablemetadata] @tableName = @tablename

	DECLARE @HashValue VARBINARY(8000) = (HASHBYTES(N'SHA2_512', SUBSTRING((SELECT N', ' + ST1.ColumnName AS [text()] FROM @TableMetadata AS ST1 FOR XML PATH (N'')), 2, 1000)))

	SELECT DISTINCT ST2.TableName AS tablename,
		SUBSTRING(
					(
						SELECT N', ' + ST1.ColumnName AS [text()]
						FROM @TableMetadata AS ST1
						FOR XML PATH (N'')
					), 2, 1000) AS Columns,
		N'SELECT ' + 
		SUBSTRING(
					(
						SELECT N', ' + ST1.ColumnName AS [text()]
						FROM @TableMetadata AS ST1
						FOR XML PATH (N'')
					), 2, 1000) 
					+ N' FROM ' + ST2.TableName
					Query
		, @HashValue AS HashValue
		, tab.CreatedOn
		, tab.ModifiedOn
		, tab.DeletedOn
		, tab.Deleted
		, CASE 
			WHEN (SELECT COUNT(DISTINCT x.tablename) AS A FROM [dbo].[hlbiattributeconfig] AS x WHERE x.tablename = @tablename) = 0 THEN N'Delete'
			WHEN tab.HashValue IS NULL THEN N'New'
			WHEN tab.HashValue != @HashValue THEN N'Update'
			ELSE N'Unchanged'
			END AS [Status]
	FROM @TableMetadata AS ST2
	LEFT JOIN (SELECT tablename, hashvalue, createdon, modifiedon, deletedon, deleted FROM [dbo].[hlbitomtable] WHERE Deleted = 0) AS tab
	ON ST2.TableName = tab.tablename
	UNION
	SELECT x.tablename AS tablename
	, [columns]
	, [query]
	, [hashvalue]
	, [createdon]
	, [modifiedon]
	, [deletedon]
	, [deleted]
	, N'Delete' AS [Status]
	FROM dbo.hlbitomtable AS x 
	WHERE NOT EXISTS (SELECT DISTINCT x.tablename FROM [dbo].[hlbiattributeconfig] AS z WHERE z.tablename = x.tablename)
	AND Deleted = 0
END

GO
