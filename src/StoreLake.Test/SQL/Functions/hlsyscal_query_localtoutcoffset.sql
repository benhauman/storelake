-- see https://archive.codeplex.com/?p=tsqltoolboxs
-- DROP FUNCTION [dbo].[hlsyscal_query_localtoutcoffset]
CREATE FUNCTION [dbo].[hlsyscal_query_localtoutcoffset] -- daylight correction
(
    @timezoneid INT
   ,@localdate DATETIME
)
RETURNS TABLE AS RETURN
(
	SELECT [daylightdeltasec] = CONVERT(INT, IIF(clc_dst.[is_dst]=1,[arx].[daylightdeltasec],0))
    FROM [dbo].[hlsystimezoneadjustmentrule] AS [arx] WITH (READUNCOMMITTED)
    OUTER APPLY ( SELECT DATEADD(SECOND, [arx].[daylighttransitionendtimeofdaysec] - [arx].[daylightdeltasec], CONVERT(DATETIME, fnr.result_date)) AS clc_lwim_e
	   FROM [dbo].[hlsyscal_query_lastweekdayinmonth] ([arx].[daylighttransitionenddayofweek], DATEFROMPARTS(YEAR(@localdate)
			 , [arx].[daylighttransitionendmonth], [arx].[daylighttransitionendday])) AS fnr
    ) AS clcb -- lwim_e (LastWeekInMoth from rule_EndDayOfWeek)
    OUTER APPLY ( SELECT DATEADD(SECOND, [arx].[daylighttransitionendtimeofdaysec], CONVERT(DATETIME, CONVERT(DATE, DATEADD(DAY, ([arx].[daylighttransitionendweek] - 1) * 7, fnr.result_date)))) AS clc_fwim_e
	   FROM [dbo].[hlsyscal_query_firstweekdayinmonth] ([arx].[daylighttransitionenddayofweek], DATEFROMPARTS(YEAR(@localdate), [arx].[daylighttransitionendmonth], [arx].[daylighttransitionendday])) AS fnr
    ) AS clcc -- fwim_e
    OUTER APPLY ( SELECT DATEADD(SECOND, [arx].[daylighttransitionstarttimeofdaysec] + [arx].[daylightdeltasec], CONVERT(DATETIME, fnr.result_date)) AS clc_lwim_s
	   FROM [dbo].[hlsyscal_query_lastweekdayinmonth] ([arx].[daylighttransitionstartdayofweek], DATEFROMPARTS(YEAR(@localdate), [arx].[daylighttransitionstartmonth], [arx].[daylighttransitionstartday])) AS fnr
    ) AS clce -- lwim_s
    OUTER APPLY ( SELECT DATEADD(SECOND, [arx].[daylighttransitionstarttimeofdaysec], CONVERT(DATETIME, CONVERT(DATE, DATEADD(DAY, ([arx].[daylighttransitionstartweek] - 1) * 7, fnr.result_date)))) AS fwim_s
	   FROM [dbo].[hlsyscal_query_firstweekdayinmonth] ([arx].[daylighttransitionstartdayofweek], DATEFROMPARTS(YEAR(@localdate), [arx].[daylighttransitionstartmonth], [arx].[daylighttransitionstartday])) AS fnr
    ) AS clcf -- fwim_s
CROSS APPLY (
		SELECT [is_dst] = IIF(
	    -- southern_hemisphere
		  arx.southern_hemisphere = 1
		  AND @localdate >= 
				IIF([arx].[daylighttransitionendisfixeddaterule]   = 1
				    , DATEADD(SECOND, [arx].[daylighttransitionendtimeofdaysec], CONVERT(DATETIME, DATEFROMPARTS(YEAR(@localdate), [arx].[daylighttransitionendmonth], [arx].[daylighttransitionendday]))) -- clc_a
				    , IIF([arx].[daylighttransitionendweek]   = 5, clc_lwim_e, clc_fwim_e))
		  AND @localdate <
				IIF([arx].[daylighttransitionstartisfixeddaterule] = 1
				    , DATEADD(SECOND, [arx].[daylighttransitionstarttimeofdaysec], CONVERT(DATETIME, DATEFROMPARTS(YEAR(@localdate), [arx].[daylighttransitionstartmonth], [arx].[daylighttransitionstartday]))) -- clc_d
				    , IIF([arx].[daylighttransitionstartweek] = 5, clc_lwim_s, fwim_s))
		
		OR  --northern_hemisphere
		  [arx].[northern_hemisphere] = 1 
			 AND @localdate >=
				IIF([arx].[daylighttransitionstartisfixeddaterule] = 1
						  , DATEADD(SECOND, [arx].[daylighttransitionstarttimeofdaysec], CONVERT(DATETIME, DATEFROMPARTS(YEAR(@localdate), [arx].[daylighttransitionstartmonth], [arx].[daylighttransitionstartday]))) -- clc_d
						  , IIF([arx].[daylighttransitionstartweek] = 5, clc_lwim_s, fwim_s))
			 AND @localdate <
				IIF([arx].[daylighttransitionendisfixeddaterule] = 1
				   , DATEADD(SECOND, [arx].[daylighttransitionendtimeofdaysec], CONVERT(DATETIME, DATEFROMPARTS(YEAR(@localdate), [arx].[daylighttransitionendmonth], [arx].[daylighttransitionendday]))) -- clc_a
					   , IIF([arx].[daylighttransitionendweek] = 5, clc_lwim_e, clc_fwim_e))
		
	   , 1, 0)
	) AS clc_dst
	OUTER APPLY (
	-- USE CASE: SELECT CAST('2019-04-07 02:00:00' AS DATETIME) AT TIME ZONE 'Central Standard Time (Mexico)' AT TIME ZONE 'UTC' => 2019-04-07 08:00:00.000 +00:00
		SELECT [dt_invalid] = IIF(@localdate >= fwim_s AND @localdate < DATEADD(SECOND, [arx].[daylightdeltasec], fwim_s), 1, 0)
		WHERE arx.northern_hemisphere=1 AND ISNULL(arx.timezone_baseutcoffsetsec,0)<0 -- north-west hemispehere
		AND [arx].[daylighttransitionstartisfixeddaterule]=0 -- dynamic rule
		AND [arx].[daylighttransitionstartweek] = 1
		AND [arx].[daylighttransitionendweek] = 5
	
	) AS clc_dt_invalid
    WHERE [arx].[hltzid] = @timezoneid AND @localdate BETWEEN [arx].[datestart] AND [arx].[dateend]
     AND ISNULL(clc_dt_invalid.[dt_invalid],0)=0

)