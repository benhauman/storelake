CREATE
--OR ALTER
FUNCTION [dbo].[hlsys_splitstring](@delimiter NVARCHAR(10), @source NVARCHAR(MAX))
RETURNS TABLE AS RETURN 
(
    WITH [cte]([start], [stop], [depth], [length]) AS 
    (
       SELECT [start]  = CONVERT(BIGINT, 1)
            , [stop]   = CHARINDEX(@delimiter, @source + CONVERT(NVARCHAR(MAX), @delimiter))
            , [depth]  = CONVERT(BIGINT, 0)
            , [length] = IIF([x].[length] = 0, CAST(N'Empty delimiter' AS INT), [x].[length])
       FROM (VALUES (DATALENGTH(@delimiter) / 2 /* Unicode */)) AS [x]([length])
     --FROM (VALUES (LEN(@delimiter))) AS [x]([length]) -- LEN does not include trailing spaces, therefore this function won't work with ' ' as a delimiter, for example

       UNION ALL

       SELECT [start]  = [x].[stop] + [x].[length]
            , [stop]   = CHARINDEX(@delimiter, @source + CONVERT(NVARCHAR(MAX), @delimiter), [x].[stop] + [x].[length])
            , [depth]  = [x].[depth] + 1
            , [length] = [x].[length]
       FROM [cte] AS [x]
       WHERE [x].[stop] > 0
    )
    SELECT [id]    = [x].[depth]
         , [value] = CONVERT(NVARCHAR(MAX), SUBSTRING(@source, [x].[start], IIF([x].[stop] > 0, [x].[stop] - [x].[start], 0)))
    FROM [cte] AS [x]
    WHERE [x].[stop] > 0
)