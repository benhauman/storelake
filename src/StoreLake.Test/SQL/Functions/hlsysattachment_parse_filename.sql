CREATE 
--OR ALTER
FUNCTION [dbo].[hlsysattachment_parse_filename](@filename NVARCHAR(510))
RETURNS TABLE AS RETURN 
(
    SELECT [filename]    = [x].[filename]
         , [contentid]   = CAST(IIF([c].[ismatch] = 1, [c].[contentid], NULL) AS UNIQUEIDENTIFIER)
         , [isembedded]  = CAST(IIF([c].[ismatch] = 1, [c].[isembedded], 0) AS BIT)
         , [displayname] = CAST(IIF([c].[ismatch] = 1, [c].[filename], [x].[filename]) AS NVARCHAR(510))
    FROM (VALUES (@filename)) AS [x]([filename])
    CROSS APPLY (
        SELECT [ismatch]    = IIF([first] IS NOT NULL AND [third] IS NOT NULL AND [fourth] IS NOT NULL, 1, 0)
             , [contentid]  = [c].[second]
             , [isembedded] = [c].[third]
             , [filename]   = [c].[fourth]
        FROM (
            SELECT [first]  = MAX(CASE WHEN [c].[id] = 0 AND [c].[value] = N'0D4543028AF6417fB85C11902032DF59'      THEN [c].[value] END)
                 , [second] = MAX(CASE WHEN [c].[id] = 1 AND TRY_CONVERT(UNIQUEIDENTIFIER, [c].[value]) IS NOT NULL THEN [c].[value] END)
                 , [third]  = MAX(CASE WHEN [c].[id] = 2 AND ISNULL([c].[value], -1) IN (0, 1)                      THEN [c].[value] END)
                 , [fourth] = MAX(CASE WHEN [c].[id] = 3                                                            THEN [c].[value] END)
            FROM [dbo].[hlsys_splitstring](N'#', [x].[filename]) AS [c]
        ) AS [c]
    ) AS [c]
)
/*
GO
SELECT [c].*
FROM (VALUES (N'some image.png')
           , (N'0D4543028AF6417fB85C11902032DF59#a288ccb5-1e91-41f7-bece-de8204897e75#1#UX Review UP2 S3.jpg')
           , (N'0D4543028AF6417fB85C11902032DF59#a288ccb5-1e91-41f7-bece-de8204897e75#2#UX Review UP2 S3.jpg')
           , (N'0D4543028AF6417fB85C11902032DF59#a288ccb5-1e91-41f7- bece-de8204897e75#1#UX Review UP2 S3.jpg')
           , (N'10D4543028AF6417fB85C11902032DF59#a288ccb5-1e91-41f7-bece-de8204897e75#1#UX Review UP2 S3.jpg')
           , (N'10D4543028AF6417fB85C11902032DF59#a288ccb5-1e91-41f7-bece-de8204897e75#2#UX Review UP2 S3.jpg')
           , (N'10D4543028AF6417fB85C11902032DF59#a288ccb5-1e91-41f7- bece-de8204897e75#1#UX Review UP2 S3.jpg')
           , (N'abc.jpg#wtflol')) AS [x]([filename])
CROSS APPLY [dbo].[hlsysattachment_parse_filename]([x].[filename]) AS [c]
*/