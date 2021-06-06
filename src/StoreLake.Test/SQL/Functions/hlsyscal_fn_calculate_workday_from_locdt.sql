/*
	DROP FUNCTION [dbo].[hlsyscal_fn_calculate_workday_from_locdt]
*/
CREATE FUNCTION [dbo].[hlsyscal_fn_calculate_workday_from_locdt]
(
	@calendarid INT,
	@timezoneid INT,
	@timezone_baseutcoffsetsec INT,
	@arg_starttime_loc_dt DATETIME
)
RETURNS TABLE AS RETURN
(
SELECT TOP 1 -- returns the first of all working days that include or follow the specified loc_dt argument.
			@arg_starttime_loc_dt AS arg
		  , cc.dayno_wrk AS dayno_wrk
		  , wrkdaystart_utc_dt = cc.daystart_utc_dt	-- used for IntervalStartOA
		  , wrkdayend_utc_dt   = cc.dayend_utc_dt	-- used for IntervalEndOA
		  , wrkdaystart_loc_dt = cc.daystart_loc_dt	-- used for AddDay/reset_time evaluation
		  
		  , cc.tia_from_wrk_sec, cc.tia_to_wrk_sec 
		  , cc.tib_from_wrk_sec, cc.tib_to_wrk_sec

		  , cc.tia_from_utc_dt, cc.tia_to_utc_dt, cc.tib_from_utc_dt, cc.tib_to_utc_dt
		  , cc.tib_to_loc_dt -- for 'Aof' trace
		  , cc.[day_wrksec_period], cc.[wrksec_seq_period]
		  , cc.[on_endwd_seq]
		  , [on_endwd_cor] = IIF (cc.[on_endwd_seq] > 0 AND (cc.tia_totime = 86399 OR cc.tib_totime = 86399), cc.[on_endwd_seq], IIF(cc.[on_endwd_seq] = 0, 0, cc.[on_endwd_seq] + 1)) -- if this day uses fullday configuration then endwd_seq should be used, if not moved to next day
		  , [is_endwd]     = IIF (cc.tia_totime = 86399 OR cc.tib_totime = 86399, 1, 0) -- used by 'Aof'
		  --, cc.ti_cnt
		  -------------------------------------------
		  -- if a different loc day has been found => reset the starttime for the furher processing. (c++) if ( dtInOut.m_julian > oldDate.m_julian)
		  -- see hlCalendarCalculator.cpp(789)
		  , [dt_inout_loc] = dtinout.[dt_inout_loc]

		  , dt_inout_dts = (SELECT daylightdeltasec FROM [dbo].[hlsyscal_query_localtoutcoffset](@timezoneid, dtinout.[dt_inout_loc]))

		  , dt_inout_utc = DATEADD(SECOND, (-1)*(ISNULL(@timezone_baseutcoffsetsec,0) + ISNULL(/*dst_inout.daylightdeltasec*/
			(SELECT daylightdeltasec FROM [dbo].[hlsyscal_query_localtoutcoffset](@timezoneid, dtinout.[dt_inout_loc]))
		  ,0)), dtinout.[dt_inout_loc])


		  , res.res_key
		  , res.[tia_before]-- = CAST(IIF(rq.arg_starttime_loc_dt < cc.tia_from_loc_dt, 1, 0) AS BIT)
		  , res.[tia_in]-- = --CAST(IIF(rq.arg_starttime_loc_dt >= cc.tia_from_loc_dt AND rq.arg_starttime_loc_dt < cc.tia_to_loc_dt, 1, 0) AS BIT)
		  , res.[tib_before]-- = CAST(IIF(rq.arg_starttime_loc_dt < cc.tib_from_loc_dt, 1, 0) AS BIT)
		  , res.[tib_in] --= CAST(IIF(rq.arg_starttime_loc_dt >= cc.tib_from_loc_dt AND rq.arg_starttime_loc_dt < cc.tib_to_loc_dt, 1, 0) AS BIT)
		  , res.[tib_after]
		  -------------------------------------------
			
		  
		  , is_in_work_interval = res.ti_wrk --CAST(CASE WHEN rq.arg_starttime_loc_dt >= cc.tia_from_loc_dt AND rq.arg_starttime_loc_dt < cc.tia_to_loc_dt THEN 1 -- in A
			--	      WHEN rq.arg_starttime_loc_dt >= cc.tib_from_loc_dt AND rq.arg_starttime_loc_dt < cc.tib_to_loc_dt THEN 1 -- in B
			--		  ELSE 0 END AS BIT) AS is_in_work_interval

		  , res.[StartWD_To_Arg]
		  , res.[Arg_To_EndWD]

		  , day_dff = DATEDIFF(DAY, cc.calendarday_date, rq.arg_starttime_loc_dt) 
		  , tia_from_loc_dt
		  , tib_from_loc_dt

		  , ti_cnt -- used by Aof
		  , ti_seq -- used by Aof
		  , arg_wrk_sec = cc.tia_from_wrk_sec + res.StartWD_To_Arg

		FROM [dbo].[hlsyscalcachecalendarday] AS cc WITH ( NOLOCK )
		CROSS APPLY (
			SELECT [arg_starttime_loc_dt] = CASE                    -- IsTimeIncluded { start <= ? <= end }
					WHEN DATEPART(YEAR, @arg_starttime_loc_dt) < 2000 THEN CAST(36524 AS DATETIME) -- cache start. CAST(36524 AS DATETIME)=>2000-01-01 00:00:00.000
					WHEN DATEPART(YEAR, @arg_starttime_loc_dt) > 2040 THEN CAST(51499 AS DATETIME) -- cache end.   CAST(51499 AS DATETIME)=>2040-12-31 00:00:00.000 
					ELSE @arg_starttime_loc_dt END
		) AS rq
		CROSS APPLY (
			  SELECT res_key = N'befA',  [tia_before] = 1, [tia_in] = 0, [tib_before] = IIF(ti_cnt=2,1,0), [tib_in] = 0, ti_wrk = 0, [tib_after] = 0
				   , [StartWD_To_Arg] = 0
				   , [Arg_To_EndWD] = DATEDIFF(SECOND, cc.tia_from_loc_dt, cc.tia_to_loc_dt) + DATEDIFF(SECOND, cc.tib_from_loc_dt, cc.tib_to_loc_dt)
						WHERE rq.arg_starttime_loc_dt < cc.tia_from_loc_dt --THEN 0 -- before tia_from, before tia_from => wholeA+wholeB
	UNION ALL SELECT res_key = N'inA',  [tia_before] = 0, [tia_in] = IIF(DATEDIFF(SECOND, rq.arg_starttime_loc_dt, cc.tia_to_loc_dt)>0,1,0)
					, [tib_before] = IIF(ti_cnt=2,1,0), [tib_in] = 0, ti_wrk = IIF(DATEDIFF(SECOND, rq.arg_starttime_loc_dt, cc.tia_to_loc_dt)>0,1,0)
					, [tib_after] = IIF(ti_cnt=1 AND rq.arg_starttime_loc_dt >= cc.tib_to_loc_dt, 1, 0) -- only A interval => A=B =>[tib_after] the end of the day
				   , [StartWD_To_Arg] = DATEDIFF(SECOND, cc.tia_from_loc_dt, rq.arg_starttime_loc_dt)
				   -- Arg_To_EndWD = 0 WHEN only A and on Ato_border ELSE diff...
	               , [Arg_To_EndWD] = IIF(ti_cnt=1 AND rq.arg_starttime_loc_dt >= cc.tib_to_loc_dt, 0, DATEDIFF(SECOND, rq.arg_starttime_loc_dt, cc.tia_to_loc_dt) + DATEDIFF(SECOND, cc.tib_from_loc_dt, cc.tib_to_loc_dt))
						WHERE rq.arg_starttime_loc_dt >= cc.tia_from_loc_dt AND rq.arg_starttime_loc_dt <= cc.tia_to_loc_dt -- between tia_from & tia_to
	-- between A and B
	UNION ALL SELECT res_key = N'A_B',  [tia_before] = 0, [tia_in] = 0, [tib_before] = IIF(ti_cnt=2,1,0), [tib_in] = 0, ti_wrk = 0, [tib_after] = 0
				   , [StartWD_To_Arg] = DATEDIFF(SECOND, cc.tia_from_loc_dt, cc.tia_to_loc_dt)
				   , [Arg_To_EndWD]= DATEDIFF(SECOND, cc.tib_from_loc_dt, cc.tib_to_loc_dt)
						WHERE rq.arg_starttime_loc_dt >= cc.tia_to_loc_dt AND rq.arg_starttime_loc_dt < cc.tib_from_loc_dt  			-- between A & B => the whole A interval (can be precached: tia_wrk_sec)

	-- in B (or on border:'tib_after')
	UNION ALL SELECT res_key = N'inB',  [tia_before] = 0, [tia_in] = 0, [tib_before] = 0, [tib_in] = IIF(ti_cnt=2 AND rq.arg_starttime_loc_dt < cc.tib_to_loc_dt, 1, 0), [ti_wrk] = IIF(ti_cnt=2 AND rq.arg_starttime_loc_dt < cc.tib_to_loc_dt, 1, 0)
					, [tib_after] = IIF(rq.arg_starttime_loc_dt >= cc.tib_to_loc_dt, 1, 0)
				   , [StartWD_To_Arg] = DATEDIFF(SECOND, cc.tia_from_loc_dt, cc.tia_to_loc_dt) + DATEDIFF(SECOND, cc.tib_from_loc_dt, rq.arg_starttime_loc_dt)
				   , [Arg_To_EndWD]=IIF(rq.arg_starttime_loc_dt >= cc.tib_to_loc_dt, 0, DATEDIFF(SECOND, rq.arg_starttime_loc_dt, cc.tib_to_loc_dt))
						WHERE rq.arg_starttime_loc_dt >= cc.tib_from_loc_dt AND rq.arg_starttime_loc_dt <= cc.tib_to_loc_dt  -- between tib_from & tib_to
		) AS res
		-- convert dtInOut to UTC
		CROSS APPLY ( 
			-- if a different loc day has been found => reset the starttime for the furher processing. (c++) if ( dtInOut.m_julian > oldDate.m_julian)
			-- see hlCalendarCalculator.cpp(789)
			SELECT [dt_inout_loc] = IIF (DATEDIFF(DAY, cc.daystart_loc_dt, rq.arg_starttime_loc_dt) = 0, rq.[arg_starttime_loc_dt], cc.daystart_loc_dt) 
		) AS dtinout
--		OUTER APPLY [dbo].[hlsyscal_query_localtoutcoffset](@timezoneid, dtinout.[dt_inout_loc]) AS dst_inout

		WHERE cc.calendarid = @calendarid 
		AND cc.day_wrksec_period > 0 --> hasWorkIntervals?
		AND rq.arg_starttime_loc_dt <= cc.tib_to_loc_dt -- IsTimeIncluded { start <= ? <= end }
		ORDER BY cc.[calendarid],cc.[calendarday_date] -- this is the [PK_hlsyscalcachecalendarday]
)
/*
	DROP FUNCTION [dbo].[hlsyscal_fn_calculate_workday_from_locdt]
*/
