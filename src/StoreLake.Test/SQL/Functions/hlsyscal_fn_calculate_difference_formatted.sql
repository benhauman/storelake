/*

DROP FUNCTION [dbo].[hlsyscal_fn_calculate_difference_formatted]

*/
CREATE FUNCTION [dbo].[hlsyscal_fn_calculate_difference_formatted]
(
	@caseid_casedefid INT = 0,
	@calendarid INT,
	@fromtime_utc DATETIME,
	@totime_utc DATETIME
)
RETURNS TABLE AS RETURN
(
    -- ==========================================
    -- ==== GLOSSARY =============================
	-- ==========================================
    -- 'fd': from day, 'td': to day
	--- (n):compe(N)sated, p:com(P)leted => dn:days compensated, dp:days completed
	-- 'totl': total
	-- 'dffn' : DiFF seconds compeNsated
	-- 'dffp' : DiFF seconds comPleted
	-- 'totl' : 'TOTaL' seconds in FD and TD without seconds_in_the_middle. 
	-- 'dn': correction for Days compeNsated
	-- 'dp': correction for Days comPleted

    SELECT calendarid = @calendarid

		, fd.dayno_wrk AS fd_dayno_wrk
		, td.dayno_wrk AS td_dayno_wrk

--, xx.[debug_scenario_text] -- can be used for copy/paste development
	    , [debug_scenario_text] = CONCAT(N'' -- DECISION MATRIX
			,IIF(xx.sid <> 0, CONCAT(N'##:', xx.sid),N'')
			,CONCAT(N'######'
					   ,CONCAT(N' WHEN ('
					, IIF(fd.dayno_wrk = td.dayno_wrk, N'fd.dayno_wrk = td.dayno_wrk', N'')
					, IIF(fd.dayno_wrk < td.dayno_wrk, N'fd.dayno_wrk < td.dayno_wrk', N'')

					, IIF(fd.StartWD_To_Arg > 0, N' AND fd.StartWD_To_Arg > 0', N'')
					, IIF(fd.StartWD_To_Arg = 0, N' AND fd.StartWD_To_Arg = 0', N'')
				
					, IIF(fd.Arg_To_EndWD = 0, N' AND fd.Arg_To_EndWD = 0', N'')
					, IIF(fd.Arg_To_EndWD > 0, N' AND fd.Arg_To_EndWD > 0', N'')

					, IIF(td.StartWD_To_Arg = 0 , N' AND td.StartWD_To_Arg = 0', N'')
					, IIF(td.StartWD_To_Arg > 0 , N' AND td.StartWD_To_Arg > 0', N'')

					, IIF(td.Arg_To_EndWD = 0 , N' AND td.Arg_To_EndWD = 0', N'')
					, IIF(td.Arg_To_EndWD > 0 , N' AND td.Arg_To_EndWD > 0', N'')

				    , IIF(td.StartWD_To_Arg = fd.StartWD_To_Arg, N' AND td.StartWD_To_Arg = fd.StartWD_To_Arg', N'')
					, IIF(td.StartWD_To_Arg > fd.StartWD_To_Arg, N' AND td.StartWD_To_Arg > fd.StartWD_To_Arg', N'')
				    , IIF(td.StartWD_To_Arg < fd.StartWD_To_Arg, N' AND td.StartWD_To_Arg < fd.StartWD_To_Arg', N'')

				    , IIF(td.Arg_To_EndWD = fd.Arg_To_EndWD, N' AND td.Arg_To_EndWD = fd.Arg_To_EndWD', N'')
					, IIF(td.Arg_To_EndWD > fd.Arg_To_EndWD, N' AND td.Arg_To_EndWD > fd.Arg_To_EndWD', N'')
				    , IIF(td.Arg_To_EndWD < fd.Arg_To_EndWD, N' AND td.Arg_To_EndWD < fd.Arg_To_EndWD', N'')
					, N') THEN x',scenario_id,N'x --> ver_4 {', scenario_id, N'}' -- version: adjustments of the scenarion expressions
				)))

		--- CalculateCaseTimesCache:
		, [result_totalsec_compensated] = CAST(ISNULL(IIF(fd.dayno_wrk = td.dayno_wrk,0,(td.wrksec_seq_period - fd.wrksec_seq_period - td.day_wrksec_period))  + xx.totl, -60606) AS BIGINT) -- totalseconds_in_middle + totl ? - (td.dayno_wrk - fd.dayno_wrk)
		, [result_totalsec_completed]   = CAST(ISNULL(IIF(fd.dayno_wrk = td.dayno_wrk,0,(td.wrksec_seq_period - fd.wrksec_seq_period - td.day_wrksec_period))  + xx.totl, -60606) AS BIGINT) -- totalseconds_in_middle + totl ? - (td.dayno_wrk - fd.dayno_wrk)
		, [result_seconds_compensated]	= CAST(ISNULL(xx.dffn, -60606) AS INT) -- if null => error
		, [result_seconds_completed]	= CAST(ISNULL(xx.dffp, -60606) AS INT) -- if null => error
		, [result_days_completed]		= CAST(ISNULL(IIF(td.dayno_wrk - fd.dayno_wrk > 1, td.dayno_wrk - fd.dayno_wrk - 1, 0) + xx.dp, -60606) AS INT) --xx.[result_totaldays] -- sd.[middle_wrk_days]
		, [result_days_compensated]		= CAST(ISNULL(IIF(td.dayno_wrk - fd.dayno_wrk > 1, td.dayno_wrk - fd.dayno_wrk - 1, 0) + xx.dn, -60606) AS INT) --xx.[result_totaldays] -- sd.[middle_wrk_days]

		, [result_firstday_seconds] = CAST(IIF(fd.dayno_wrk < td.dayno_wrk AND ( fd.StartWD_To_Arg > 0 ) AND ( td.StartWD_To_Arg >= fd.StartWD_To_Arg ), fd.StartWD_To_Arg + fd.Arg_To_EndWD, 0) AS INT) -- see '(c++)HoursInFromDay' ? see 'outFirstDayCompensationNeeded'
		, sc_res.sc_txt
		, [result_firstday_interval_start_utc_dt] = sc_res.ti_s -- see '(c++)IntervalStartOA'
		, [result_firstday_interval_end_utc_dt] = sc_res.ti_e -- see '(c++)IntervalEndOA'
		, [result_firstday_interval_iswork] = CAST(sc_res.ti_w AS BIT)--CAST(IIF(@fromtime_utc<@totime_utc, fd.is_in_work_interval, td.is_in_work_interval) AS BIT) -- see '(c++)IntervalIsWork'

	FROM [dbo].[hlsyscalendar] AS cal WITH ( NOLOCK )
	OUTER APPLY dbo.[hlsyscal_query_utctolocaloffset](cal.timezone, @fromtime_utc) AS dst_start
	OUTER APPLY dbo.[hlsyscal_query_utctolocaloffset](cal.timezone, @totime_utc) AS dst_end
	CROSS APPLY [dbo].[hlsyscal_fn_calculate_workday_from_locdt](@calendarid, cal.timezone, cal.timezone_baseutcoffsetsec, DATEADD(SECOND, (ISNULL(cal.timezone_baseutcoffsetsec,0) + ISNULL(dst_start.daylightdeltasec,0)), @fromtime_utc)) AS fd
	CROSS APPLY [dbo].[hlsyscal_fn_calculate_workday_from_locdt](@calendarid, cal.timezone, cal.timezone_baseutcoffsetsec, DATEADD(SECOND, (ISNULL(cal.timezone_baseutcoffsetsec,0) + ISNULL(dst_end.daylightdeltasec,0)), @totime_utc)    ) AS td


	OUTER APPLY ( -- see '(c++)IntervalIsWork', '(c++)IntervalStartOA', '(c++)IntervalEndOA'
			  SELECT sc_txt=N' <A', ti_w=0, ti_s=fd.wrkdaystart_utc_dt	, ti_e = fd.tia_from_utc_dt						WHERE fd.tia_before=1 AND fd.tia_in=0 --AND fd.tib_before=0 AND fd.tib_in=0	-- before A
	UNION ALL SELECT sc_txt=N'>A<', ti_w=1, ti_s=fd.tia_from_utc_dt		, ti_e = fd.tia_to_utc_dt						WHERE fd.tia_before=0 AND fd.tia_in=1 --AND fd.tib_before=0 AND fd.tib_in=0	-- in A
	UNION ALL SELECT sc_txt=N'A.B', ti_w=0, ti_s=fd.tia_to_utc_dt		, ti_e = fd.tib_from_utc_dt						WHERE fd.tia_before=0 AND fd.tia_in=0 AND fd.tib_before=1 --AND fd.tib_in=0	-- between
	UNION ALL SELECT sc_txt=N'>B<', ti_w=1, ti_s=fd.tib_from_utc_dt		, ti_e = fd.tib_to_utc_dt						WHERE fd.tia_before=0 AND fd.tia_in=0 AND fd.tib_before=0 AND fd.tib_in=1	-- in B 
	UNION ALL SELECT sc_txt=N' >B', ti_w=0, ti_s=fd.tib_to_utc_dt		, ti_e = DATEADD(DAY, 1, fd.wrkdaystart_utc_dt) WHERE fd.tia_before=0 AND fd.tia_in=0 AND fd.tib_before=0 AND fd.tib_in=0	-- after B
	) AS sc_res

	
	CROSS APPLY 
	(
		SELECT --[middle_wrk_days] = IIF(td.dayno_wrk - fd.dayno_wrk > 1, td.dayno_wrk - fd.dayno_wrk - 1, 0)

		  [scenario_id] = CASE-- DECISION MATRIX
			WHEN (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg = 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg = 0 AND td.Arg_To_EndWD > 0 AND td.StartWD_To_Arg = fd.StartWD_To_Arg AND td.Arg_To_EndWD < fd.Arg_To_EndWD) THEN 808001 --> ver_4 {808001}
			WHEN (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg = 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg = 0 AND td.Arg_To_EndWD > 0 AND td.StartWD_To_Arg = fd.StartWD_To_Arg AND td.Arg_To_EndWD > fd.Arg_To_EndWD) THEN 808002 --> ver_4 {808002}
			WHEN (fd.dayno_wrk = td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > 0 AND td.Arg_To_EndWD = 0 AND td.StartWD_To_Arg > fd.StartWD_To_Arg AND td.Arg_To_EndWD < fd.Arg_To_EndWD) THEN 808003 --> ver_4 {808003}
			WHEN (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg = 0 AND td.Arg_To_EndWD > 0 AND td.StartWD_To_Arg < fd.StartWD_To_Arg AND td.Arg_To_EndWD > fd.Arg_To_EndWD) THEN 808004 --> ver_4 {808004}
			WHEN (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > 0 AND td.Arg_To_EndWD > 0 AND td.StartWD_To_Arg < fd.StartWD_To_Arg AND td.Arg_To_EndWD > fd.Arg_To_EndWD) THEN 808005 --> ver_4 {808005}
			WHEN (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg = 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > 0 AND td.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > fd.StartWD_To_Arg AND td.Arg_To_EndWD < fd.Arg_To_EndWD) THEN 808006 --> ver_4 {808006}
			WHEN (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > 0 AND td.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > fd.StartWD_To_Arg AND td.Arg_To_EndWD < fd.Arg_To_EndWD) THEN 808007 --> ver_4 {808007}
			WHEN (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD = 0 AND td.StartWD_To_Arg = 0 AND td.Arg_To_EndWD > 0 AND td.StartWD_To_Arg < fd.StartWD_To_Arg AND td.Arg_To_EndWD > fd.Arg_To_EndWD) THEN 808008 --> ver_4 {808008}
			WHEN (fd.dayno_wrk = td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD = 0 AND td.StartWD_To_Arg > 0 AND td.Arg_To_EndWD = 0 AND td.StartWD_To_Arg = fd.StartWD_To_Arg AND td.Arg_To_EndWD = fd.Arg_To_EndWD) THEN 808009 --> ver_4 {808009}
			WHEN (fd.dayno_wrk = td.dayno_wrk AND fd.StartWD_To_Arg = 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > 0 AND td.Arg_To_EndWD = 0 AND td.StartWD_To_Arg > fd.StartWD_To_Arg AND td.Arg_To_EndWD < fd.Arg_To_EndWD) THEN 808010 --> ver_4 {808010}
			WHEN (fd.dayno_wrk = td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > 0 AND td.Arg_To_EndWD > 0 AND td.StartWD_To_Arg = fd.StartWD_To_Arg AND td.Arg_To_EndWD = fd.Arg_To_EndWD) THEN 808011 --> ver_4 {808011}
			WHEN (fd.dayno_wrk = td.dayno_wrk AND fd.StartWD_To_Arg = 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > 0 AND td.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > fd.StartWD_To_Arg AND td.Arg_To_EndWD < fd.Arg_To_EndWD) THEN 808012 --> ver_4 {808012}
			WHEN (fd.dayno_wrk = td.dayno_wrk AND fd.StartWD_To_Arg = 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg = 0 AND td.Arg_To_EndWD > 0 AND td.StartWD_To_Arg = fd.StartWD_To_Arg AND td.Arg_To_EndWD = fd.Arg_To_EndWD) THEN 808013 --> ver_4 {808013}
			WHEN (fd.dayno_wrk = td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > 0 AND td.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > fd.StartWD_To_Arg AND td.Arg_To_EndWD < fd.Arg_To_EndWD) THEN 808014 --> ver_4 {808014}
			WHEN (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg = 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg = 0 AND td.Arg_To_EndWD > 0 AND td.StartWD_To_Arg = fd.StartWD_To_Arg AND td.Arg_To_EndWD = fd.Arg_To_EndWD) THEN 808015 --> ver_4 {808015}
			WHEN (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > 0 AND td.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > fd.StartWD_To_Arg AND td.Arg_To_EndWD > fd.Arg_To_EndWD) THEN 808016 --> ver_4 {808016}
			WHEN (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > 0 AND td.Arg_To_EndWD > 0 AND td.StartWD_To_Arg = fd.StartWD_To_Arg AND td.Arg_To_EndWD = fd.Arg_To_EndWD) THEN 808017 --> ver_4 {808017}
			WHEN (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg = 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > 0 AND td.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > fd.StartWD_To_Arg AND td.Arg_To_EndWD > fd.Arg_To_EndWD) THEN 808018 --> ver_4 {808018}
			WHEN (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > 0 AND td.Arg_To_EndWD > 0 AND td.StartWD_To_Arg = fd.StartWD_To_Arg AND td.Arg_To_EndWD > fd.Arg_To_EndWD) THEN 808019 --> ver_4 {808019}
			WHEN (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > 0 AND td.Arg_To_EndWD > 0 AND td.StartWD_To_Arg < fd.StartWD_To_Arg AND td.Arg_To_EndWD < fd.Arg_To_EndWD) THEN 808020 --> ver_4 {808020}
			WHEN (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > 0 AND td.Arg_To_EndWD = 0 AND td.StartWD_To_Arg > fd.StartWD_To_Arg AND td.Arg_To_EndWD < fd.Arg_To_EndWD) THEN 808021 --> ver_4 {808021}
			WHEN (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > 0 AND td.Arg_To_EndWD > 0 AND td.StartWD_To_Arg = fd.StartWD_To_Arg AND td.Arg_To_EndWD < fd.Arg_To_EndWD) THEN 808022 --> ver_4 {808022}
			WHEN (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg = 0 AND td.Arg_To_EndWD > 0 AND td.StartWD_To_Arg < fd.StartWD_To_Arg AND td.Arg_To_EndWD < fd.Arg_To_EndWD) THEN 808023 --> ver_4 {808023}
			WHEN (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > 0 AND td.Arg_To_EndWD > 0 AND td.StartWD_To_Arg < fd.StartWD_To_Arg AND td.Arg_To_EndWD = fd.Arg_To_EndWD) THEN 808024 --> ver_4 {808024}
			WHEN (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > 0 AND td.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > fd.StartWD_To_Arg AND td.Arg_To_EndWD = fd.Arg_To_EndWD) THEN 808025 --> ver_4 {808025}
			WHEN (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg = 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > 0 AND td.Arg_To_EndWD = 0 AND td.StartWD_To_Arg > fd.StartWD_To_Arg AND td.Arg_To_EndWD < fd.Arg_To_EndWD) THEN 808026 --> ver_4 {808026}
			WHEN (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD = 0 AND td.StartWD_To_Arg > 0 AND td.Arg_To_EndWD = 0 AND td.StartWD_To_Arg = fd.StartWD_To_Arg AND td.Arg_To_EndWD = fd.Arg_To_EndWD) THEN 808027 --> ver_4 {808027}
			WHEN (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD = 0 AND td.StartWD_To_Arg > 0 AND td.Arg_To_EndWD > 0 AND td.StartWD_To_Arg < fd.StartWD_To_Arg AND td.Arg_To_EndWD > fd.Arg_To_EndWD) THEN 808028 --> ver_4 {808028}
			WHEN (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg = 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > 0 AND td.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > fd.StartWD_To_Arg AND td.Arg_To_EndWD = fd.Arg_To_EndWD) THEN 808029 --> ver_4 {808029}
			WHEN (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > 0 AND td.Arg_To_EndWD = 0 AND td.StartWD_To_Arg < fd.StartWD_To_Arg AND td.Arg_To_EndWD < fd.Arg_To_EndWD) THEN 808030 --> ver_4 {808030}
			WHEN (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg = 0 AND td.Arg_To_EndWD > 0 AND td.StartWD_To_Arg < fd.StartWD_To_Arg AND td.Arg_To_EndWD = fd.Arg_To_EndWD) THEN 808031 --> ver_4 {808031}
			WHEN (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD = 0 AND td.StartWD_To_Arg > 0 AND td.Arg_To_EndWD = 0 AND td.StartWD_To_Arg < fd.StartWD_To_Arg AND td.Arg_To_EndWD = fd.Arg_To_EndWD) THEN 808032 --> ver_4 {808032}
			WHEN (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD = 0 AND td.StartWD_To_Arg > 0 AND td.Arg_To_EndWD = 0 AND td.StartWD_To_Arg > fd.StartWD_To_Arg AND td.Arg_To_EndWD = fd.Arg_To_EndWD) THEN 808033 --> ver_4 {808033}
			WHEN (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD = 0 AND td.StartWD_To_Arg > 0 AND td.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > fd.StartWD_To_Arg AND td.Arg_To_EndWD > fd.Arg_To_EndWD) THEN 808034 --> ver_4 {808034}
			WHEN (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD = 0 AND td.StartWD_To_Arg > 0 AND td.Arg_To_EndWD > 0 AND td.StartWD_To_Arg = fd.StartWD_To_Arg AND td.Arg_To_EndWD > fd.Arg_To_EndWD) THEN 808035 --> ver_4 {808035}
			WHEN (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > 0 AND td.Arg_To_EndWD = 0 AND td.StartWD_To_Arg = fd.StartWD_To_Arg AND td.Arg_To_EndWD < fd.Arg_To_EndWD) THEN 808036 --> ver_4 {808036}
			ELSE 0 END

		--, [totalseconds_in_middle] = (td.tia_from_wrk_sec - fd.tib_to_wrk_sec)

	) AS sd
	OUTER APPLY 
	(
			  SELECT [sid]=808001, [dp]=1, [dn]=1, [totl]=fd.Arg_To_EndWD						, [dffn]=td.StartWD_To_Arg									, [dffp]=							0 + td.StartWD_To_Arg							WHERE (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg = 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg = 0  AND td.Arg_To_EndWD > 0  AND td.StartWD_To_Arg = fd.StartWD_To_Arg AND td.Arg_To_EndWD < fd.Arg_To_EndWD) -- THEN 808001 --> ver_4 {808001}
	UNION ALL SELECT [sid]=808002, [dp]=1, [dn]=1, [totl]=fd.Arg_To_EndWD						, [dffn]=td.StartWD_To_Arg									, [dffp]=							0 + td.StartWD_To_Arg				WHERE (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg = 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg = 0  AND td.Arg_To_EndWD > 0  AND td.StartWD_To_Arg = fd.StartWD_To_Arg AND td.Arg_To_EndWD > fd.Arg_To_EndWD) -- THEN 808002 --> ver_4 {808002}
	UNION ALL SELECT [sid]=808003, [dp]=0, [dn]=0, [totl]=fd.Arg_To_EndWD						, [dffn]=fd.Arg_To_EndWD									, [dffp]=fd.Arg_To_EndWD + 0											WHERE (fd.dayno_wrk = td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > 0  AND td.Arg_To_EndWD = 0  AND td.StartWD_To_Arg > fd.StartWD_To_Arg AND td.Arg_To_EndWD < fd.Arg_To_EndWD) -- THEN 808003 --> ver_4 {808003}
	UNION ALL SELECT [sid]=808004, [dp]=0, [dn]=0, [totl]=fd.Arg_To_EndWD + td.StartWD_To_Arg	, [dffn]=fd.Arg_To_EndWD									, [dffp]=fd.Arg_To_EndWD + 0											WHERE (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg = 0  AND td.Arg_To_EndWD > 0  AND td.StartWD_To_Arg < fd.StartWD_To_Arg AND td.Arg_To_EndWD > fd.Arg_To_EndWD) -- THEN 808004 --> ver_4 {808004}
	UNION ALL SELECT [sid]=808005, [dp]=0, [dn]=0, [totl]=fd.Arg_To_EndWD + td.StartWD_To_Arg	, [dffn]=fd.Arg_To_EndWD + td.StartWD_To_Arg	, [dffp]=fd.Arg_To_EndWD + td.StartWD_To_Arg				WHERE (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > 0  AND td.Arg_To_EndWD > 0  AND td.StartWD_To_Arg < fd.StartWD_To_Arg AND td.Arg_To_EndWD > fd.Arg_To_EndWD) -- THEN 808005 --> ver_4 {808005}
	UNION ALL SELECT [sid]=808006, [dp]=1, [dn]=1, [totl]=fd.Arg_To_EndWD + td.StartWD_To_Arg	, [dffn]=td.StartWD_To_Arg								, [dffp]=                           0 + td.StartWD_To_Arg				WHERE (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg = 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > 0  AND td.Arg_To_EndWD > 0  AND td.StartWD_To_Arg > fd.StartWD_To_Arg AND td.Arg_To_EndWD < fd.Arg_To_EndWD) -- THEN 808006 --> ver_4 {808006}
	UNION ALL SELECT [sid]=808007, [dp]=0, [dn]=1, [totl]=fd.Arg_To_EndWD + td.StartWD_To_Arg	, [dffn]=td.StartWD_To_Arg - fd.StartWD_To_Arg, [dffp]=fd.Arg_To_EndWD + td.StartWD_To_Arg				WHERE (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > 0  AND td.Arg_To_EndWD > 0  AND td.StartWD_To_Arg > fd.StartWD_To_Arg AND td.Arg_To_EndWD < fd.Arg_To_EndWD) -- 808007
	UNION ALL SELECT [sid]=808008, [dp]=0, [dn]=0, [totl]=fd.Arg_To_EndWD + td.StartWD_To_Arg	, [dffn]=fd.Arg_To_EndWD + td.StartWD_To_Arg	, [dffp]=fd.Arg_To_EndWD + td.StartWD_To_Arg				WHERE (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD = 0 AND td.StartWD_To_Arg = 0  AND td.Arg_To_EndWD > 0  AND td.StartWD_To_Arg < fd.StartWD_To_Arg AND td.Arg_To_EndWD > fd.Arg_To_EndWD) -- THEN 808008 --> ver_4 {808008}
	UNION ALL SELECT [sid]=808009, [dp]=0, [dn]=0, [totl]=fd.Arg_To_EndWD						, [dffn]=fd.Arg_To_EndWD									, [dffp]=fd.Arg_To_EndWD + 0											WHERE (fd.dayno_wrk = td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD = 0 AND td.StartWD_To_Arg > 0  AND td.Arg_To_EndWD = 0  AND td.StartWD_To_Arg = fd.StartWD_To_Arg AND td.Arg_To_EndWD = fd.Arg_To_EndWD) -- THEN 808009 --> ver_4 {808009}
	UNION ALL SELECT [sid]=808010, [dp]=1, [dn]=1, [totl]=fd.Arg_To_EndWD						, [dffn]=0													, [dffp]=                           0 + 0								WHERE (fd.dayno_wrk = td.dayno_wrk AND fd.StartWD_To_Arg = 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > 0  AND td.Arg_To_EndWD = 0  AND td.StartWD_To_Arg > fd.StartWD_To_Arg AND td.Arg_To_EndWD < fd.Arg_To_EndWD) -- THEN 808010 --> ver_4 {808010}
	UNION ALL SELECT [sid]=808011, [dp]=0, [dn]=0, [totl]=td.StartWD_To_Arg - fd.StartWD_To_Arg	, [dffn]=0													, [dffp]=						    0 + 0								WHERE (fd.dayno_wrk = td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > 0  AND td.Arg_To_EndWD > 0  AND td.StartWD_To_Arg = fd.StartWD_To_Arg AND td.Arg_To_EndWD = fd.Arg_To_EndWD) -- THEN 808011 --> ver_4 {808011}
	UNION ALL SELECT [sid]=808012, [dp]=0, [dn]=0, [totl]=td.StartWD_To_Arg						, [dffn]=td.StartWD_To_Arg									, [dffp]=                           0 + td.StartWD_To_Arg	WHERE (fd.dayno_wrk = td.dayno_wrk AND fd.StartWD_To_Arg = 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > 0  AND td.Arg_To_EndWD > 0  AND td.StartWD_To_Arg > fd.StartWD_To_Arg AND td.Arg_To_EndWD < fd.Arg_To_EndWD) -- THEN 808012 --> ver_4 {808012}
	UNION ALL SELECT [sid]=808013, [dp]=0, [dn]=0, [totl]=td.StartWD_To_Arg						, [dffn]=td.StartWD_To_Arg									, [dffp]=							0 + 0								WHERE (fd.dayno_wrk = td.dayno_wrk AND fd.StartWD_To_Arg = 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg = 0  AND td.Arg_To_EndWD > 0  AND td.StartWD_To_Arg = fd.StartWD_To_Arg AND td.Arg_To_EndWD = fd.Arg_To_EndWD) -- THEN 808013 --> ver_4 {808013}
	UNION ALL SELECT [sid]=808014, [dp]=0, [dn]=0, [totl]=td.StartWD_To_Arg - fd.StartWD_To_Arg	, [dffn]=td.StartWD_To_Arg - fd.StartWD_To_Arg, [dffp]=fd.Arg_To_EndWD - td.Arg_To_EndWD		WHERE (fd.dayno_wrk = td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > 0  AND td.Arg_To_EndWD > 0  AND td.StartWD_To_Arg > fd.StartWD_To_Arg AND td.Arg_To_EndWD < fd.Arg_To_EndWD) -- THEN 808014 --> ver_4 {808014}
	UNION ALL SELECT [sid]=808015, [dp]=1, [dn]=1, [totl]=fd.Arg_To_EndWD						, [dffn]=td.StartWD_To_Arg									, [dffp]=							0 + 0								WHERE (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg = 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg = 0  AND td.Arg_To_EndWD > 0  AND td.StartWD_To_Arg = fd.StartWD_To_Arg AND td.Arg_To_EndWD = fd.Arg_To_EndWD) -- THEN 808015 --> ver_4 {808015}
	UNION ALL SELECT [sid]=808016, [dp]=0, [dn]=1, [totl]=fd.Arg_To_EndWD + td.StartWD_To_Arg	, [dffn]=td.StartWD_To_Arg - fd.StartWD_To_Arg, [dffp]=fd.Arg_To_EndWD + td.StartWD_To_Arg	WHERE (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > 0  AND td.Arg_To_EndWD > 0  AND td.StartWD_To_Arg > fd.StartWD_To_Arg AND td.Arg_To_EndWD > fd.Arg_To_EndWD) -- THEN 808016 --> ver_4 {808016}
	UNION ALL SELECT [sid]=808017, [dp]=0, [dn]=1, [totl]=fd.Arg_To_EndWD + td.StartWD_To_Arg	, [dffn]=0																, [dffp]=fd.Arg_To_EndWD + td.StartWD_To_Arg	WHERE (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > 0  AND td.Arg_To_EndWD > 0  AND td.StartWD_To_Arg = fd.StartWD_To_Arg AND td.Arg_To_EndWD = fd.Arg_To_EndWD) -- THEN 808017 --> ver_4 {808017}
	UNION ALL SELECT [sid]=808018, [dp]=1, [dn]=1, [totl]=fd.Arg_To_EndWD + td.StartWD_To_Arg	, [dffn]=td.StartWD_To_Arg									, [dffp]=                           0 + td.StartWD_To_Arg	WHERE (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg = 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > 0  AND td.Arg_To_EndWD > 0  AND td.StartWD_To_Arg > fd.StartWD_To_Arg AND td.Arg_To_EndWD > fd.Arg_To_EndWD) -- THEN 808018 --> ver_4 {808018}
	UNION ALL SELECT [sid]=808019, [dp]=0, [dn]=1, [totl]=fd.Arg_To_EndWD + td.StartWD_To_Arg	, [dffn]=0																, [dffp]=fd.Arg_To_EndWD + td.StartWD_To_Arg	WHERE (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > 0  AND td.Arg_To_EndWD > 0  AND td.StartWD_To_Arg = fd.StartWD_To_Arg AND td.Arg_To_EndWD > fd.Arg_To_EndWD) -- THEN 808019 --> ver_4 {808019}
	UNION ALL SELECT [sid]=808020, [dp]=0, [dn]=0, [totl]=fd.Arg_To_EndWD + td.StartWD_To_Arg	, [dffn]=fd.Arg_To_EndWD + td.StartWD_To_Arg	, [dffp]=fd.Arg_To_EndWD + td.StartWD_To_Arg	WHERE (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > 0  AND td.Arg_To_EndWD > 0  AND td.StartWD_To_Arg < fd.StartWD_To_Arg AND td.Arg_To_EndWD < fd.Arg_To_EndWD) -- THEN 808020 --> ver_4 {808020}
	UNION ALL SELECT [sid]=808021, [dp]=0, [dn]=1, [totl]=fd.Arg_To_EndWD + td.StartWD_To_Arg	, [dffn]=fd.Arg_To_EndWD									, [dffp]=fd.Arg_To_EndWD + td.StartWD_To_Arg	WHERE (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > 0  AND td.Arg_To_EndWD = 0  AND td.StartWD_To_Arg > fd.StartWD_To_Arg AND td.Arg_To_EndWD < fd.Arg_To_EndWD) -- THEN 808021 --> ver_4 {808021}
	UNION ALL SELECT [sid]=808022, [dp]=0, [dn]=1, [totl]=fd.Arg_To_EndWD + td.StartWD_To_Arg	, [dffn]=0																, [dffp]=fd.Arg_To_EndWD + td.StartWD_To_Arg	WHERE (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > 0  AND td.Arg_To_EndWD > 0  AND td.StartWD_To_Arg = fd.StartWD_To_Arg AND td.Arg_To_EndWD < fd.Arg_To_EndWD) -- THEN 808022 --> ver_4 {808022}
	UNION ALL SELECT [sid]=808023, [dp]=0, [dn]=0, [totl]=fd.Arg_To_EndWD + td.StartWD_To_Arg	, [dffn]=fd.Arg_To_EndWD									, [dffp]=fd.Arg_To_EndWD + 0								WHERE (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg = 0  AND td.Arg_To_EndWD > 0  AND td.StartWD_To_Arg < fd.StartWD_To_Arg AND td.Arg_To_EndWD < fd.Arg_To_EndWD) -- THEN 808023 --> ver_4 {808023}
	UNION ALL SELECT [sid]=808024, [dp]=0, [dn]=0, [totl]=fd.Arg_To_EndWD + td.StartWD_To_Arg	, [dffn]=fd.Arg_To_EndWD + td.StartWD_To_Arg	, [dffp]=fd.Arg_To_EndWD + td.StartWD_To_Arg	WHERE (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > 0  AND td.Arg_To_EndWD > 0  AND td.StartWD_To_Arg < fd.StartWD_To_Arg AND td.Arg_To_EndWD = fd.Arg_To_EndWD) -- THEN 808024 --> ver_4 {808024}
	UNION ALL SELECT [sid]=808025, [dp]=0, [dn]=1, [totl]=fd.Arg_To_EndWD + td.StartWD_To_Arg	, [dffn]=td.StartWD_To_Arg - fd.StartWD_To_Arg, [dffp]=fd.Arg_To_EndWD + td.StartWD_To_Arg	WHERE (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > 0  AND td.Arg_To_EndWD > 0  AND td.StartWD_To_Arg > fd.StartWD_To_Arg AND td.Arg_To_EndWD = fd.Arg_To_EndWD) -- THEN 808025 --> ver_4 {808025}
	UNION ALL SELECT [sid]=808026, [dp]=1, [dn]=1, [totl]=fd.Arg_To_EndWD + td.StartWD_To_Arg	, [dffn]=td.StartWD_To_Arg - fd.StartWD_To_Arg, [dffp]=fd.Arg_To_EndWD + 0								WHERE (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg = 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > 0  AND td.Arg_To_EndWD = 0  AND td.StartWD_To_Arg > fd.StartWD_To_Arg AND td.Arg_To_EndWD < fd.Arg_To_EndWD) -- THEN 808026 --> ver_4 {808026}
	UNION ALL SELECT [sid]=808027, [dp]=0, [dn]=1, [totl]=fd.Arg_To_EndWD + td.StartWD_To_Arg	, [dffn]=td.StartWD_To_Arg - fd.StartWD_To_Arg, [dffp]=fd.Arg_To_EndWD + td.StartWD_To_Arg	WHERE (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD = 0 AND td.StartWD_To_Arg > 0  AND td.Arg_To_EndWD = 0  AND td.StartWD_To_Arg = fd.StartWD_To_Arg AND td.Arg_To_EndWD = fd.Arg_To_EndWD) -- THEN 808027 --> ver_4 {808027}
	UNION ALL SELECT [sid]=808028, [dp]=0, [dn]=0, [totl]=fd.Arg_To_EndWD + td.StartWD_To_Arg	, [dffn]=fd.Arg_To_EndWD + td.StartWD_To_Arg  , [dffp]=fd.Arg_To_EndWD + td.StartWD_To_Arg	WHERE (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD = 0 AND td.StartWD_To_Arg > 0  AND td.Arg_To_EndWD > 0  AND td.StartWD_To_Arg < fd.StartWD_To_Arg AND td.Arg_To_EndWD > fd.Arg_To_EndWD) -- THEN 808028 --> ver_4 {808028}		
	UNION ALL SELECT [sid]=808029, [dp]=1, [dn]=1, [totl]=fd.Arg_To_EndWD + td.StartWD_To_Arg	, [dffn]=td.StartWD_To_Arg									, [dffp]=                           0 + td.StartWD_To_Arg	WHERE (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg = 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > 0 AND td.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > fd.StartWD_To_Arg AND td.Arg_To_EndWD = fd.Arg_To_EndWD) -- THEN 808029 --> ver_4 {808006}
	UNION ALL SELECT [sid]=808030, [dp]=0, [dn]=0, [totl]=fd.Arg_To_EndWD + td.StartWD_To_Arg	, [dffn]=fd.Arg_To_EndWD + td.StartWD_To_Arg				, [dffp]=fd.Arg_To_EndWD + td.StartWD_To_Arg				WHERE (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > 0 AND td.Arg_To_EndWD = 0 AND td.StartWD_To_Arg < fd.StartWD_To_Arg AND td.Arg_To_EndWD < fd.Arg_To_EndWD) -- THEN 808030 --> ver_4 {808030}
	UNION ALL SELECT [sid]=808031, [dp]=0, [dn]=0, [totl]=fd.Arg_To_EndWD + td.StartWD_To_Arg	, [dffn]=fd.Arg_To_EndWD									, [dffp]=fd.Arg_To_EndWD + 0								WHERE (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg = 0 AND td.Arg_To_EndWD > 0 AND td.StartWD_To_Arg < fd.StartWD_To_Arg AND td.Arg_To_EndWD = fd.Arg_To_EndWD) -- THEN 808031 --> ver_4 {808031}
	UNION ALL SELECT [sid]=808032, [dp]=0, [dn]=0, [totl]=fd.Arg_To_EndWD + td.StartWD_To_Arg	, [dffn]=fd.Arg_To_EndWD + td.StartWD_To_Arg				, [dffp]=fd.Arg_To_EndWD + td.StartWD_To_Arg				WHERE (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD = 0 AND td.StartWD_To_Arg > 0 AND td.Arg_To_EndWD = 0 AND td.StartWD_To_Arg < fd.StartWD_To_Arg AND td.Arg_To_EndWD = fd.Arg_To_EndWD) -- THEN 808032 --> 808032 {0}
	UNION ALL SELECT [sid]=808033, [dp]=0, [dn]=1, [totl]=fd.Arg_To_EndWD + td.StartWD_To_Arg	, [dffn]=td.StartWD_To_Arg - fd.StartWD_To_Arg				, [dffp]=fd.Arg_To_EndWD + td.StartWD_To_Arg				WHERE (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD = 0 AND td.StartWD_To_Arg > 0 AND td.Arg_To_EndWD = 0 AND td.StartWD_To_Arg > fd.StartWD_To_Arg AND td.Arg_To_EndWD = fd.Arg_To_EndWD) -- THEN 808033 --> ver_4 {808033}
	UNION ALL SELECT [sid]=808034, [dp]=0, [dn]=1, [totl]=fd.Arg_To_EndWD + td.StartWD_To_Arg	, [dffn]=td.StartWD_To_Arg - fd.StartWD_To_Arg				, [dffp]=td.StartWD_To_Arg									WHERE (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD = 0 AND td.StartWD_To_Arg > 0 AND td.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > fd.StartWD_To_Arg AND td.Arg_To_EndWD > fd.Arg_To_EndWD) -- THEN 808034 --> ver_4 {808034}
	UNION ALL SELECT [sid]=808035, [dp]=0, [dn]=1, [totl]=fd.Arg_To_EndWD + td.StartWD_To_Arg	, [dffn]=td.StartWD_To_Arg - fd.StartWD_To_Arg				, [dffp]=fd.Arg_To_EndWD + td.StartWD_To_Arg				WHERE (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD = 0 AND td.StartWD_To_Arg > 0 AND td.Arg_To_EndWD > 0 AND td.StartWD_To_Arg = fd.StartWD_To_Arg AND td.Arg_To_EndWD > fd.Arg_To_EndWD) -- THEN 808035 --> ver_4 {808035}
	UNION ALL SELECT [sid]=808036, [dp]=0, [dn]=1, [totl]=fd.Arg_To_EndWD + td.StartWD_To_Arg	, [dffn]=td.StartWD_To_Arg - fd.StartWD_To_Arg				, [dffp]=fd.Arg_To_EndWD + td.StartWD_To_Arg				WHERE (fd.dayno_wrk < td.dayno_wrk AND fd.StartWD_To_Arg > 0 AND fd.Arg_To_EndWD > 0 AND td.StartWD_To_Arg > 0 AND td.Arg_To_EndWD = 0 AND td.StartWD_To_Arg = fd.StartWD_To_Arg AND td.Arg_To_EndWD < fd.Arg_To_EndWD) -- THEN 808036 --> ver_4 {808036}
	
	) AS xx

	WHERE cal.calendarid=@calendarid--WHERE tz.hltzid IN (SELECT cal.timezone FROM [dbo].[hlsyscalendar] AS cal WITH ( NOLOCK ) WHERE cal.calendarid=@calendarid)
	  --not possible due to 1970 AND IIF(@fromtime_utc<=@totime_utc, 0, CAST(CONCAT(N'reversed arguments specified:', @fromtime_utc, N', ', @totime_utc) AS INT)) = 0
	  AND IIF(DATEPART(MS, @fromtime_utc) = 0, 0, CAST(CONCAT(N'miliseconds specified <f>:', @fromtime_utc) AS INT)) = 0
	  AND IIF(DATEPART(MS, @totime_utc) = 0, 0, CAST(CONCAT(N'miliseconds specified <t>:', @totime_utc) AS INT)) = 0
	  AND IIF(@fromtime_utc IS NOT NULL, 0, CAST(N'Null specified <f>' AS INT)) = 0
	  AND IIF(@totime_utc IS NOT NULL, 0, CAST(N'Null specified <t>' AS INT)) = 0
	  AND DATEPART(YEAR, @fromtime_utc) > 1970 -- the caller should check for such values and skip the call to this function
	  AND DATEPART(YEAR, @totime_utc  ) > 1970 -- the caller should check for such values and skip the call to this function
	  --AND IIF(DATEPART(YEAR, @fromtime_utc) > 1970, 0, CAST(CONCAT(N'year out of range<f>:', @fromtime_utc) AS INT)) = 0
	  --AND IIF(DATEPART(YEAR, @totime_utc  ) > 1970, 0, CAST(CONCAT(N'year out of range<t>:', @totime_utc  ) AS INT)) = 0
)

/*

DROP FUNCTION [dbo].[hlsyscal_fn_calculate_difference_formatted]

*/