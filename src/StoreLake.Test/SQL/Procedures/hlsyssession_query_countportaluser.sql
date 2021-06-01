-- @Namespace SessionManagement
-- @Name CountPortalUser
-- @Return ClrTypes:int Mode:Single
CREATE PROCEDURE [dbo].[hlsyssession_query_countportaluser] @productid SMALLINT, @objectid INT, @objectdefid INT
AS
	SELECT COUNT(*)
	FROM [dbo].[hlsyssession]
	WHERE productid = @productid AND objectid = @objectid AND objectdefid = @objectdefid