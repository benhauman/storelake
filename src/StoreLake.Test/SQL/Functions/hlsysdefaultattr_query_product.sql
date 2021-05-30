CREATE FUNCTION [dbo].[hlsysdefaultattr_query_product](@productdefid INT, @productid INT, @lcid INT)
RETURNS TABLE
RETURN
    SELECT CAST([x].[a] AS INT)           AS [defaultattrpathid]
		 , CAST([x].[a] AS INT)           AS [productdefid]
		 , CAST([x].[a] AS INT)           AS [productid]
		 , CAST([x].[a] AS NVARCHAR(200)) AS [defaultvalue]
	FROM (VALUES(@productdefid, @productid, @lcid, CAST(N'### DB ARTIFACTS WERE NOT REBUILT AFTER DEPLOY! ###' AS BIT))) AS [x]([productdefid], [productid], [lcid], [a]) -- use Server.RebuildModelApp.exe