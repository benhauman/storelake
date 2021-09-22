--@lubo: The suffix 'a' means ANSI and NOT unicode @delimiter und @source parameter types. (like c++ suffices for ANSI)
--       For an unicode type use/define another function: 'hlsys_splitstringw'. (like c++ suffices for UNICODE)
-- @return: column 'id' can be used in the caller ordering expression predicates.
CREATE FUNCTION [dbo].[hlsys_splitstring] 
(
	@delimiter NCHAR(1), 
	@source NVARCHAR(MAX) -- ANSI!!!!
)
RETURNS TABLE AS RETURN 
(
    WITH csvtbl([start], [stop], depth) AS 
    (
	   SELECT [start] = CONVERT(BIGINT, 1)
			, [stop] = CHARINDEX(@delimiter, @source + CONVERT(NVARCHAR(MAX), @delimiter))
			, [depth] = CONVERT(BIGINT, 0)

	   UNION ALL

	   SELECT [start] = [hy].[stop] + 1
		    , [stop] = CHARINDEX(@delimiter, @source + CONVERT(NVARCHAR(MAX), @delimiter), [hy].[stop] + 1)
		    , [depth] = [depth] + 1
	   FROM   csvtbl AS hy
	   WHERE  [hy].[stop] > 0
    )
    SELECT sq.depth AS id
		  , CONVERT(NVARCHAR(MAX), SUBSTRING(@source, sq.[start],
				    CASE WHEN sq.[stop] > 0 THEN sq.[stop] - sq.[start] ELSE 0 END)
		  ) AS value
    FROM   csvtbl AS sq
    WHERE  [stop] > 0
)
