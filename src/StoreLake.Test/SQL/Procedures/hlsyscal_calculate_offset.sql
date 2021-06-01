/*
	DROP PROCEDURE [dbo].[hlsyscal_calculate_offset];
*/
CREATE PROCEDURE [dbo].[hlsyscal_calculate_offset] 
	  @calendarid INT
	, @fromtime_utc DATETIME
	, @do_add BIT -- cannot be null
	, @wrk_offset_sec BIGINT
	, @wrk_offset_day INT  -- cannot be null
	--------------------------------------
	, @expected_datetime_utc DATETIME = NULL -- NULLABLE , only for tests
	, @show_details TINYINT = NULL -- NULLABLE , only for tests
AS 
BEGIN
	SET NOCOUNT ON;
	SET XACT_ABORT ON;

	DECLARE @error_text NVARCHAR(MAX);

	IF (@calendarid IS NULL)
	BEGIN
		SET @error_text = CONCAT(N'@calendarid is null.', N'');
		;THROW 50004, @error_text , 1;
	END

	IF (@fromtime_utc IS NULL)
	BEGIN
		SET @error_text = CONCAT(N'@fromtime_utc is null.', N'');
		;THROW 50004, @error_text , 1;
	END

	IF (@do_add IS NULL)
	BEGIN
		SET @error_text = CONCAT(N'@do_add is null.', N'');
		;THROW 50004, @error_text , 1;
	END

	IF (@wrk_offset_sec IS NULL)
	BEGIN
		SET @error_text = CONCAT(N'@wrk_offset_sec is null.', N'');
		;THROW 50004, @error_text , 1;
	END
	IF (@wrk_offset_sec < 0)
	BEGIN
		SET @error_text = CONCAT(N'@wrk_offset_sec is negative:', @wrk_offset_sec, N'');
		;THROW 50004, @error_text , 1;
	END

	IF (@wrk_offset_day IS NULL)
	BEGIN
		SET @error_text = CONCAT(N'@wrk_offset_day is null.', N'');
		;THROW 50004, @error_text , 1;
	END
	IF (@wrk_offset_day < 0)
	BEGIN
		SET @error_text = CONCAT(N'@wrk_offset_day is negative:', @wrk_offset_day, N'');
		;THROW 50004, @error_text , 1;
	END

	IF NOT EXISTS(SELECT * FROM [dbo].[hlsyscalcachecalendarday] WITH ( NOLOCK ) WHERE calendarid = @calendarid)
	BEGIN
		SET @error_text = CONCAT(N'Missing calendar:', @calendarid, N'');
		;THROW 50004, @error_text , 1;
	END

	DECLARE @calendar_timezone INT;
	DECLARE @timezone_baseutcoffsetsec INT;
	DECLARE @fromtime_loc DATETIME;
	SELECT @calendar_timezone = cal.timezone, @timezone_baseutcoffsetsec = cal.timezone_baseutcoffsetsec
		  ,@fromtime_loc = DATEADD(SECOND, (@timezone_baseutcoffsetsec + ISNULL(fdx_dst_start.daylightdeltasec,0)), @fromtime_utc)
	  FROM [dbo].[hlsyscalendar] AS cal WITH ( NOLOCK )
	  OUTER APPLY [dbo].[hlsyscal_query_utctolocaloffset](cal.timezone, @fromtime_utc) AS fdx_dst_start
	  WHERE cal.calendarid = @calendarid;

	  SET @timezone_baseutcoffsetsec = ISNULL(@timezone_baseutcoffsetsec, 0);

	IF (@calendar_timezone IS NULL)
	BEGIN
		SET @error_text = CONCAT(N'@calendarid is not valid. @calendarid:', @calendarid, N'');
		;THROW 50004, @error_text , 1;
	END

	--PRINT CONCAT(N'@calendar_timezone:', @calendar_timezone);
	--PRINT CONCAT(N'@timezone_baseutcoffsetsec:', @timezone_baseutcoffsetsec);
	-- <ADD> calculate/locate 'fd'. see {#'fd' resolver.#}
	-- <ADD> if dayoffset specified calculate next_dayno_wrk and find the doff
	-- <ADD> prepare next day nd_arg_wrk_sec
	-- <ADD> find 'pd' without compensation -- potential 'to' day 
	-- <ADD> calculate the pt_arg_wrk_sec with compensation
	-- <ADD> find 'td'
	-- <ADD> build the result
	DECLARE @result_datetime_loc DATETIME -- cannot be null
	DECLARE @result_datetime_utc DATETIME -- cannot be null

	DECLARE @arg_fromtime_loc_dt DATETIME = @fromtime_loc; -- cannot be null
	DECLARE @wrk_offset_x_day INT = 0; -- @wrk_offset_x_day

	DECLARE @dfd1_dayno_wrk INT; -- can be null
	DECLARE @dfd1_arg_loc NVARCHAR(MAX); -- can be null
	DECLARE @dfd1_dt_inout_loc DATETIME -- can be null
	DECLARE @dfd1_tia_before BIT; -- cannot be null
	--DECLARE @dfd1_seconds_from_StartLoc_To_Arg INT; -- cannot be null
	DECLARE @dfd1_diff_sec_wrkdaystart_loc_dt_to_dt_inout_loc INT; -- cannot be null if DaysAdd;

	DECLARE @reset_time INT; -- cannot be null if DaysAdd;
	DECLARE @change_time DATETIME;-- can be null
	DECLARE @dmd1_StartWD_To_Arg INT; -- can be null
	DECLARE @dmd1_Arg_To_EndWD INT; -- can be null
	DECLARE @dmd1_dayno_wrk INT; -- can be null
	DECLARE @dmd1_arg_wrk_sec BIGINT; -- can be null
	DECLARE @dmd1_arg_loc NVARCHAR(MAX); -- can be null
	DECLARE @dmd1_dt_inout_raw_loc DATETIME; -- can be null
	DECLARE @dmd1_dt_inout_loc DATETIME; -- can be null

	--(+) {#'fd' resolver.#}
	DECLARE @fd_dayno_wrk INT;         -- returned by the 'fd' resolver. 
	DECLARE @fd_dt_inout_loc DATETIME; -- returned by the 'fd' resolver.
	DECLARE @fd_tia_from_wrk_sec BIGINT;
	DECLARE @fd_tib_to_wrk_sec BIGINT;
	DECLARE @fd_StartWD_To_Arg INT; -- cannot be null
	DECLARE @fd_Arg_To_EndWD INT; -- cannot be null
	DECLARE @fd_ti_cnt TINYINT; -- cannot be null
	DECLARE @fd_tia_before BIT; -- cannot be null
	DECLARE @fd_tia_in BIT; -- cannot be null
	DECLARE @fd_tib_in BIT; -- cannot be null
	DECLARE @fd_tib_after BIT; -- cannot be null
	DECLARE @fd_on_endwd_seq INT; -- cannot be null
	DECLARE @fd_is_endwd BIT; -- cannot be null
	DECLARE @fd_on_endwd_cor INT; -- cannot be null
	DECLARE @fd_tib_to_loc_dt DATETIME; -- cannot be null
	DECLARE @fd_ti_seq INT; -- cannot be null;
	DECLARE @fd_arg_wrk_sec BIGINT; -- cannot be null;
	DECLARE @fd_arg_loc NVARCHAR(MAX); -- cannot be null
	--(-) {#'fd' resolver.#}
	DECLARE @fd_next_dayno_wrk INT; -- used to locate the next work day based on 'fd' and specified '@wrk_offset_day'. Will be the same as @fd_dayno_wrk if '@wrk_offset_day' is null or zero.
	DECLARE @md_next_dayno_wrk INT; -- used to locate the next work day based on 'fd' and specified '@wrk_offset_day'. Will be the same as @fd_dayno_wrk if '@wrk_offset_day' is null or zero.
	DECLARE @cc_dayno_wrk INT; --used to locate the td work day

	DECLARE @md1_dayno_wrk INT; -- cannot be null
	DECLARE @md2_dayno_wrk INT; -- CAN be null
	DECLARE @md_use2 BIT; -- CAN be null
	DECLARE @md_dayno_wrk INT; -- cannot be null
	DECLARE @md_ti_seq INT; -- cannot be null;
	DECLARE @md_arg_wrk_sec BIGINT; -- cannot be null;
	DECLARE @md_tia_before BIT; -- cannot be null
	DECLARE @md_tib_after BIT; -- cannot be null
	DECLARE @md1_arg_loc NVARCHAR(MAX); -- cannot be null
	DECLARE @md2_arg_loc NVARCHAR(MAX); -- cannot be null
	DECLARE @md_arg_loc NVARCHAR(MAX); -- cannot be null
	DECLARE @md_Arg_To_EndWD INT; -- cannot be null
	DECLARE @md_tib_to_wrk_sec BIGINT; -- cannot be null

	--DECLARE @doff_tia_from_wrk_sec BIGINT; -- can be null
	--DECLARE @doff_StartWD_To_Arg INT; -- can be nullm
	DECLARE @md_dt_inout_loc DATETIME; -- cannot be null
	DECLARE @md_StartWD_To_Arg INT; -- cannot be null
	DECLARE @md_on_endwd_seq INT; -- cannot be null
	DECLARE @expr1x BIGINT; -- cannot be null
	DECLARE @cc_tis1_txt NVARCHAR(MAX);
	DECLARE @cc_tis1_key NVARCHAR(MAX);
	--DECLARE @expr_corr INT;
	DECLARE @expr2x BIGINT; -- cannot be null

	IF (@wrk_offset_day > 0)
	BEGIN
--			DROP PROCEDURE [dbo].[hlsyscal_calculate_offset];

		-- see: line(747) void CalculatorImpl::addDay(...)
		---> 1) calculateNextWorkday(calendar, @arg_fromtime_loc_dt, @wrk_offset_day) & Calendar_findWorkDay
		---> 2) arg_loc = CHK_AFTER => reset the time and 'calculateNextWorkday' again

		SELECT @dfd1_dayno_wrk = dayno_wrk
			 , @dfd1_dt_inout_loc = dt_inout_loc
			 , @dfd1_diff_sec_wrkdaystart_loc_dt_to_dt_inout_loc = DATEDIFF(SECOND, dfd1.wrkdaystart_loc_dt, dt_inout_loc)
			 , @dfd1_tia_before = tia_before
			 , @dfd1_arg_loc = CASE WHEN dfd1.arg_wrk_sec < dfd1.tia_from_wrk_sec THEN N'TBA' -- BeforeA
									WHEN dfd1.arg_wrk_sec = dfd1.tia_from_wrk_sec THEN N'TSA' -- StartA
									WHEN dfd1.arg_wrk_sec > dfd1.tia_from_wrk_sec AND dfd1.arg_wrk_sec < dfd1.tia_to_wrk_sec THEN N'TIA' -- InA
									WHEN dfd1.arg_wrk_sec = dfd1.tia_to_wrk_sec THEN N'TEA' -- EndA
									WHEN dfd1.ti_cnt = 2 AND dfd1.arg_wrk_sec > dfd1.tia_to_wrk_sec AND dfd1.arg_wrk_sec < dfd1.tib_from_wrk_sec THEN N'TXP'
									WHEN dfd1.ti_cnt = 2 AND dfd1.arg_wrk_sec = dfd1.tib_from_wrk_sec THEN N'TSB' -- StartB
									WHEN dfd1.ti_cnt = 2 AND dfd1.arg_wrk_sec > dfd1.tib_from_wrk_sec AND dfd1.arg_wrk_sec < dfd1.tib_to_wrk_sec THEN N'TIB' -- InB
									WHEN dfd1.ti_cnt = 2 AND dfd1.arg_wrk_sec = dfd1.tib_to_wrk_sec THEN N'TEB' -- EndB
									ELSE N'AWD' END -- AfterWD
		FROM [dbo].[hlsyscal_fn_calculate_workday_from_locdt](@calendarid, @calendar_timezone, @timezone_baseutcoffsetsec, @arg_fromtime_loc_dt) AS dfd1

		SELECT @dmd1_dayno_wrk = dayno_wrk
			 , @dmd1_dt_inout_raw_loc = arg_starttime_loc_dt
			 , @dmd1_StartWD_To_Arg = seconds_From_StartWD_To_Arg
			 , @dmd1_Arg_To_EndWD = seconds_From_Arg_To_EndWD
			 , @dmd1_arg_wrk_sec = dmd1.arg_wrk_sec
			 , @dmd1_arg_loc = CASE WHEN dmd1.arg_wrk_sec  < dmd1.tia_from_wrk_sec THEN N'TBA' -- BeforeA
									WHEN dmd1.arg_wrk_sec  = dmd1.tia_from_wrk_sec THEN N'TSA' -- StartA
									WHEN dmd1.arg_wrk_sec  > dmd1.tia_from_wrk_sec AND dmd1.arg_wrk_sec < dmd1.tia_to_wrk_sec THEN N'TIA' -- InA
									WHEN dmd1.arg_wrk_sec  = dmd1.tia_to_wrk_sec THEN N'TEA' -- EndA
									WHEN dmd1.arg_wrk_sec > dmd1.tia_to_wrk_sec AND dmd1.arg_wrk_sec < dmd1.tib_from_wrk_sec THEN N'TXP'
									WHEN dmd1.arg_wrk_sec = dmd1.tib_from_wrk_sec THEN N'TSB' -- StartB
									WHEN dmd1.arg_wrk_sec > dmd1.tib_from_wrk_sec AND dmd1.arg_wrk_sec < dmd1.tib_to_wrk_sec THEN N'TIB' -- InB
									WHEN dmd1.arg_wrk_sec = dmd1.tib_to_wrk_sec THEN N'TEB' -- EndB
									ELSE N'AWD' END -- AfterWD
		FROM [dbo].[hlsyscal_fn_calculate_workday_from_wrkdayno](@calendarid, @dfd1_dayno_wrk + @wrk_offset_day, DATEPART(HOUR, @dfd1_dt_inout_loc), DATEPART(MINUTE, @dfd1_dt_inout_loc), DATEPART(SECOND, @dfd1_dt_inout_loc)) AS dmd1

		-- if there is at least one day in the middle that says: 'the arg location is after my EndOfWD' => add a day & reset the argtime to 00:00:00
		SET @reset_time = ISNULL((SELECT TOP 1 [chk_after_dayno_wrk] 
											FROM (
												SELECT [chk_after_dayno_wrk] = IIF((DATEADD(SECOND, @dfd1_diff_sec_wrkdaystart_loc_dt_to_dt_inout_loc, rd.daystart_loc_dt) > tib_to_loc_dt),rd.dayno_wrk,0)
												FROM [dbo].[hlsyscalcachecalendarday] AS rd WHERE rd.calendarid = @calendarid AND (@dfd1_tia_before = 0) AND ((@dmd1_dayno_wrk - @dfd1_dayno_wrk) >= 2) AND rd.dayno_wrk BETWEEN (@dfd1_dayno_wrk+1) AND (@dmd1_dayno_wrk)
											) AS in_wd
											WHERE [chk_after_dayno_wrk] > 0)
										,0);

		-- SET @reset_time = 0;
		IF (@reset_time > 0)
		BEGIN
			-- .\projects\Common\CalendarSystem\hlCalenderCalculator.cpp Line:762
			SET @dmd1_dt_inout_loc = DATEADD(DAY, 1, @dmd1_dt_inout_raw_loc);
			SET @dmd1_dt_inout_loc = DATETIMEFROMPARTS(YEAR(@dmd1_dt_inout_loc), MONTH(@dmd1_dt_inout_loc), DAY(@dmd1_dt_inout_loc), 0, 0, 0, 0);
		END
		ELSE
		BEGIN
			-- No reset but IF there is a day in the middle for that argument and the argument is before tia_from THEN change the arg to this tia_from
			-- evaluate @change_time
			DECLARE @ssss INT = DATEPART(HOUR,@dmd1_dt_inout_raw_loc)*3600 + DATEPART(MINUTE,@dmd1_dt_inout_raw_loc)*60 + DATEPART(SECOND,@dmd1_dt_inout_raw_loc);
			SET @change_time = (SELECT TOP 1 tia_from_loc_dt 
								  FROM 
								  (
										SELECT  dayno_wrk, tia_from_loc_dt 
												,[before_tia_from] = IIF((DATEADD(SECOND, @ssss, rd.daystart_loc_dt) < tia_from_loc_dt),rd.tia_from_loc_dt,tia_from_loc_dt)
												,[before_tia_XXXX] = IIF((DATEADD(SECOND, @ssss, rd.daystart_loc_dt) < tia_from_loc_dt),1,0)
												,[dff] = DATEDIFF(SECOND, DATEADD(SECOND, @ssss, rd.daystart_loc_dt) , tia_from_loc_dt)
												--, tia_from_loc_dt
											FROM [dbo].[hlsyscalcachecalendarday] AS rd 
											WHERE rd.calendarid = @calendarid AND (@dfd1_tia_before = 0) AND ((@dmd1_dayno_wrk - @dfd1_dayno_wrk) >= 2) AND rd.dayno_wrk BETWEEN (@dfd1_dayno_wrk+1) AND (@dmd1_dayno_wrk)
								  ) AS bfr
								  WHERE bfr.dff > 0
								  ORDER BY dff DESC
								);	

		-- if (@change_time IS AFTER dmd1_endwd in the @dmd1_dt_inout_raw_loc day.
			
			DECLARE @dff_chg INT = (DATEPART(HOUR, @change_time)*3600+DATEPART(MINUTE, @change_time)*60+DATEPART(SECOND, @change_time))
								 - (DATEPART(HOUR, @dmd1_dt_inout_raw_loc)*3600+DATEPART(MINUTE, @dmd1_dt_inout_raw_loc)*60+DATEPART(SECOND, @dmd1_dt_inout_raw_loc));
			 IF (@dff_chg > 0)
			 BEGIN
				-- if (@change_time IS AFTER dmd1_endwd?
				SET @dmd1_dt_inout_loc = DATETIMEFROMPARTS(
											DATEPART(YEAR, @dmd1_dt_inout_raw_loc)
										  , DATEPART(MONTH, @dmd1_dt_inout_raw_loc)
										  , DATEPART(DAY, @dmd1_dt_inout_raw_loc)
										  , DATEPART(HOUR, @change_time)
										  , DATEPART(MINUTE, @change_time)
										  , DATEPART(SECOND, @change_time)
										  , 0);
			 END
			 ELSE
			 BEGIN
				SET @dmd1_dt_inout_loc = @dmd1_dt_inout_raw_loc;
			 END 

			
		END

		IF (@dmd1_arg_loc = N'TEB' OR @dmd1_arg_loc = N'AWD')
		BEGIN
			SET @error_text = CONCAT(N'Offset add <DAY> is not implemented. Calendar:', @calendarid, N'');
			;THROW 50004, @error_text , 1;
		END
		ELSE
		BEGIN
			SET @arg_fromtime_loc_dt = @dmd1_dt_inout_loc;
		END
		

--			DROP PROCEDURE [dbo].[hlsyscal_calculate_offset];

		--SET @error_text = CONCAT(N'Offset add <DAY> is not implemented. Calendar:', @calendarid, N'');
		--;THROW 50004, @error_text , 1;
	END

	--(+) {#'fd' resolver.#}
	SELECT @fd_dayno_wrk = fdx.[dayno_wrk]
	     , @fd_dt_inout_loc = fdx.[dt_inout_loc]
		 , @fd_tia_from_wrk_sec = fdx.tia_from_wrk_sec
		 , @fd_tib_to_wrk_sec = fdx.tib_to_wrk_sec
		 , @fd_StartWD_To_Arg = fdx.StartWD_To_Arg
		 , @fd_Arg_To_EndWD = fdx.Arg_To_EndWD
		 , @fd_ti_cnt = fdx.ti_cnt
		 , @fd_tia_before = fdx.tia_before
		 , @fd_tia_in = fdx.tia_in
		 , @fd_tib_in = fdx.tib_in
		 , @fd_tib_after = fdx.tib_after
		 , @fd_tib_to_loc_dt = fdx.tib_to_loc_dt
		 , @fd_on_endwd_seq = fdx.on_endwd_seq
		 , @fd_is_endwd = fdx.[is_endwd]
		 , @fd_on_endwd_cor = fdx.[on_endwd_cor]
		 , @fd_ti_seq = fdx.ti_seq
		 , @fd_arg_wrk_sec = fdx.arg_wrk_sec

					    , @fd_arg_loc = CASE WHEN fdx.arg_wrk_sec < fdx.tia_from_wrk_sec THEN N'TBA' -- BeforeA
											WHEN fdx.arg_wrk_sec = fdx.tia_from_wrk_sec THEN N'TSA' -- StartA
											WHEN fdx.arg_wrk_sec > fdx.tia_from_wrk_sec AND fdx.arg_wrk_sec < fdx.tia_to_wrk_sec THEN N'TIA' -- InA
											WHEN fdx.arg_wrk_sec = fdx.tia_to_wrk_sec THEN N'TEA' -- EndA
											WHEN fdx.ti_cnt = 2 AND fdx.arg_wrk_sec > fdx.tia_to_wrk_sec AND fdx.arg_wrk_sec < fdx.tib_from_wrk_sec THEN N'TXP'
											WHEN fdx.ti_cnt = 2 AND fdx.arg_wrk_sec = fdx.tib_from_wrk_sec THEN N'TSB' -- StartB
											WHEN fdx.ti_cnt = 2 AND fdx.arg_wrk_sec > fdx.tib_from_wrk_sec AND fdx.arg_wrk_sec < fdx.tib_to_wrk_sec THEN N'TIB' -- InB
											WHEN fdx.ti_cnt = 2 AND fdx.arg_wrk_sec = fdx.tib_to_wrk_sec THEN N'TEB' -- EndB
											ELSE N'TAB' END -- AfterB
	FROM [dbo].[hlsyscal_fn_calculate_workday_from_locdt](@calendarid, @calendar_timezone, @timezone_baseutcoffsetsec, @arg_fromtime_loc_dt) AS fdx
	--(-) {#'fd' resolver.#}
	
	--    DROP PROCEDURE [dbo].[hlsyscal_calculate_offset];

	IF (@wrk_offset_x_day > 0)
	BEGIN
		-- dayoffset_value
		SET @fd_next_dayno_wrk = @fd_dayno_wrk + @wrk_offset_x_day; -- - IIF (@fd_arg_loc = N'TEB', 1, 0); -- remove a day if it already jumped on the next one due to end of EndOfDay. TCxAst_323166_20120229120001__d4_h0_m0_s0

		SELECT @md_next_dayno_wrk = md.dayno_wrk
			 --, @md_arg_wrk_sec = md.arg_wrk_sec
			 , @md1_dayno_wrk = md1.dayno_wrk
			 , @md2_dayno_wrk = md2.dayno_wrk
			 , @md_use2 = use2
			 , @md_dayno_wrk = md.dayno_wrk
			 , @md_tia_before = md.tia_before
			 , @md_StartWD_To_Arg = md.seconds_From_StartWD_To_Arg
			 , @md_on_endwd_seq = md.on_endwd_seq
			 , @md_tib_after = md.tib_after
			 , @md_ti_seq = md.ti_seq
			 , @md_Arg_To_EndWD = md.seconds_From_Arg_To_EndWD
			 , @md_tib_to_wrk_sec = md.tib_to_wrk_sec
			 , @md_dt_inout_loc = md.arg_starttime_loc_dt
			 , @md_arg_wrk_sec = CASE WHEN md_arg_loc = N'TAB' THEN md.tib_to_wrk_sec + 1 + IIF(use2=1, md2.day_wrksec_period, 0) -- due time reset --  25200
									  ELSE md_arg_wrk_sec END

					    , @md_arg_loc = md_arg_loc
						, @md1_arg_loc = md1_arg_loc
						, @md2_arg_loc = md2_arg_loc

		--nd_dayno_wrk = nd.dayno_wrk, nd_tia_from_wrk_sec = nd.tia_from_wrk_sec, nd_tia_from_utc_dt = nd.tia_from_utc_dt, nd_StartWD_To_Arg = nd.seconds_From_StartWD_To_Arg
			FROM [dbo].[hlsyscal_fn_calculate_workday_from_wrkdayno](@calendarid, @fd_next_dayno_wrk, DATEPART(HOUR, @fd_dt_inout_loc), DATEPART(MINUTE, @fd_dt_inout_loc), DATEPART(SECOND, @fd_dt_inout_loc)) AS md1 -- if md1.TAB => use md2
			CROSS APPLY (
				SELECT md1_arg_wrk_sec = ( md1.tia_from_wrk_sec + md1.seconds_From_StartWD_To_Arg + IIF(md1.tib_after = 1 AND @wrk_offset_sec = 0, 1, 0) )
			) AS clc_m1_x1
			CROSS APPLY (
				SELECT md1_arg_loc = CASE WHEN md1_arg_wrk_sec < md1.tia_from_wrk_sec THEN N'TBA' -- BeforeA
										  WHEN md1_arg_wrk_sec = md1.tia_from_wrk_sec THEN N'TSA' -- StartA
										  WHEN md1_arg_wrk_sec > md1.tia_from_wrk_sec AND md1_arg_wrk_sec < md1.tia_to_wrk_sec THEN N'TIA' -- InA
										  WHEN md1_arg_wrk_sec = md1.tia_to_wrk_sec THEN N'TEA' -- EndA
										  WHEN md1.ti_cnt = 2 AND md1_arg_wrk_sec > md1.tia_to_wrk_sec AND md1_arg_wrk_sec < md1.tib_from_wrk_sec THEN N'TXP'
										  WHEN md1.ti_cnt = 2 AND md1_arg_wrk_sec = md1.tib_from_wrk_sec THEN N'TSB' -- StartB
										  WHEN md1.ti_cnt = 2 AND md1_arg_wrk_sec > md1.tib_from_wrk_sec AND md1_arg_wrk_sec < md1.tib_to_wrk_sec THEN N'TIB' -- InB
										  WHEN md1.ti_cnt = 2 AND md1_arg_wrk_sec = md1.tib_to_wrk_sec THEN N'TEB' -- EndB
										  ELSE N'TAB' END -- AfterB
			) AS clc_m1_x2
			CROSS APPLY (
				SELECT use2 = CAST(CASE WHEN md1.ti_cnt = 1 AND md1.tia_to_wrk_sec <= md1_arg_wrk_sec THEN 1
										WHEN md1.ti_cnt = 2 AND md1.tib_to_wrk_sec <= md1_arg_wrk_sec THEN 1
										ELSE 0 END AS BIT)
			) AS clc_use
			OUTER APPLY (
				SELECT dayno_wrk, ti_cnt, tia_before, tib_after, tia_to_wrk_sec, tia_from_wrk_sec, tib_from_wrk_sec, tib_to_wrk_sec, ti_seq, on_endwd_seq
						, seconds_From_StartWD_To_Arg, seconds_From_Arg_To_EndWD, arg_starttime_loc_dt, day_wrksec_period
					   , md2_arg_wrk_sec = md2.tia_from_wrk_sec + md2.seconds_From_StartWD_To_Arg + IIF(md2.tib_after = 1 AND @wrk_offset_sec = 0, 1, 0)

				FROM [dbo].[hlsyscal_fn_calculate_workday_from_wrkdayno](@calendarid, @fd_next_dayno_wrk+1, 0, 0, 0) AS md2 -- reset the time
				WHERE use2 = 1
			) AS md2
			OUTER APPLY (
				SELECT md2_arg_loc = CASE WHEN md2_arg_wrk_sec < md2.tia_from_wrk_sec THEN N'TBA' -- BeforeA
										  WHEN md2_arg_wrk_sec = md2.tia_from_wrk_sec THEN N'TSA' -- StartA
										  WHEN md2_arg_wrk_sec > md2.tia_from_wrk_sec AND md2_arg_wrk_sec < md2.tia_to_wrk_sec THEN N'TIA' -- InA
										  WHEN md2_arg_wrk_sec = md2.tia_to_wrk_sec THEN N'TEA' -- EndA
										  WHEN md2.ti_cnt = 2 AND md2_arg_wrk_sec > md2.tia_to_wrk_sec AND md2_arg_wrk_sec < md2.tib_from_wrk_sec THEN N'TXP'
										  WHEN md2.ti_cnt = 2 AND md2_arg_wrk_sec = md2.tib_from_wrk_sec THEN N'TSB' -- StartB
										  WHEN md2.ti_cnt = 2 AND md2_arg_wrk_sec > md2.tib_from_wrk_sec AND md2_arg_wrk_sec < md2.tib_to_wrk_sec THEN N'TIB' -- InB
										  WHEN md2.ti_cnt = 2 AND md2_arg_wrk_sec = md2.tib_to_wrk_sec THEN N'TEB' -- EndB
										  ELSE N'TAB' END -- AfterB
				WHERE use2 = 1
			) AS clc_md_x2
			CROSS APPLY (
				SELECT dayno_wrk		            = IIF(use2=1, md2.dayno_wrk, md1.dayno_wrk)
					 , tia_before		            = IIF(use2=1, md2.tia_before, md1.tia_before)
				     , tib_after		            = IIF(use2=1, md2.tib_after, md1.tib_after)
					 , tia_from_wrk_sec	            = IIF(use2=1, md2.tia_from_wrk_sec, md1.tia_from_wrk_sec)
					 , tia_to_wrk_sec	            = IIF(use2=1, md2.tia_to_wrk_sec, md1.tia_to_wrk_sec)
					 , tib_from_wrk_sec	            = IIF(use2=1, md2.tib_from_wrk_sec, md1.tib_from_wrk_sec)
					 , tib_to_wrk_sec	            = IIF(use2=1, md2.tib_to_wrk_sec, md1.tib_to_wrk_sec)
					 , ti_cnt			            = IIF(use2=1, md2.ti_cnt, md1.ti_cnt)
					 , on_endwd_seq		            = IIF(use2=1, md2.on_endwd_seq, md1.on_endwd_seq)
					 , ti_seq			            = IIF(use2=1, md2.ti_seq, md1.ti_seq)
					 , seconds_From_StartWD_To_Arg  = IIF(use2=1, md2.seconds_From_StartWD_To_Arg, md1.seconds_From_StartWD_To_Arg)
					 , seconds_From_Arg_To_EndWD	= IIF(use2=1, md2.seconds_From_Arg_To_EndWD, md1.seconds_From_Arg_To_EndWD)
					 , arg_starttime_loc_dt			= IIF(use2=1, md2.arg_starttime_loc_dt, md1.arg_starttime_loc_dt)
					 , md_arg_wrk_sec	            = IIF(use2=1, md2_arg_wrk_sec, md1_arg_wrk_sec)
					 , md_arg_loc                   = IIF(use2=1, md2_arg_loc, md1_arg_loc)
			) AS md

			--CROSS APPLY (
			--	SELECT arg_loc = CASE WHEN md_arg_wrk_sec < md.tia_from_wrk_sec THEN N'TBA' -- BeforeA
			--								WHEN md_arg_wrk_sec = md.tia_from_wrk_sec THEN N'TSA' -- StartA
			--								WHEN md_arg_wrk_sec > md.tia_from_wrk_sec AND md_arg_wrk_sec < md.tia_to_wrk_sec THEN N'TIA' -- InA
			--								WHEN md_arg_wrk_sec = md.tia_to_wrk_sec THEN N'TEA' -- EndA
			--								WHEN md.ti_cnt = 2 AND md_arg_wrk_sec > md.tia_to_wrk_sec AND md_arg_wrk_sec < md.tib_from_wrk_sec THEN N'TXP'
			--								WHEN md.ti_cnt = 2 AND md_arg_wrk_sec = md.tib_from_wrk_sec THEN N'TSB' -- StartB
			--								WHEN md.ti_cnt = 2 AND md_arg_wrk_sec > md.tib_from_wrk_sec AND md_arg_wrk_sec < md.tib_to_wrk_sec THEN N'TIB' -- InB
			--								WHEN md.ti_cnt = 2 AND md_arg_wrk_sec = md.tib_to_wrk_sec THEN N'TEB' -- EndB
			--								ELSE N'TAB' END -- AfterB
			--	
			--) AS loc
			WHERE @wrk_offset_x_day > 0;

				--    DROP PROCEDURE [dbo].[hlsyscal_calculate_offset];
	END --: (@wrk_offset_x_day > 0)
	ELSE
	BEGIN
		SET @md_next_dayno_wrk = @fd_dayno_wrk;
		SET @md_dayno_wrk = @fd_dayno_wrk;
		SET @md_arg_wrk_sec = @fd_arg_wrk_sec;
		SET @md_dt_inout_loc = @arg_fromtime_loc_dt; --@fd_dt_inout_loc; go to the next if... -- TCxAof_103704_20150311230000_86399
		SET @md_tia_before = @fd_tia_before;
		SET @md_StartWD_To_Arg = @fd_StartWD_To_Arg;
		SET @md_on_endwd_seq = @fd_on_endwd_seq;
		SET @md_tib_after = @fd_tib_after;
		SET @md_arg_loc = @fd_arg_loc;
		SET @md_Arg_To_EndWD = @fd_Arg_To_EndWD;
		SET @md_ti_seq = @fd_ti_seq;
		SET @md_tib_to_wrk_sec = @fd_tib_to_wrk_sec;
	END

	DECLARE @td_dayno_wrk INT; -- cannot be null
	DECLARE @td_tia_from_wrk_sec BIGINT;  -- cannot be null
	DECLARE @td_tia_to_wrk_sec BIGINT  -- cannot be null
	DECLARE @td_tib_from_wrk_sec BIGINT;  -- cannot be null
	DECLARE @td_tib_to_wrk_sec BIGINT;  -- cannot be null
	DECLARE @td_tia_from_loc_dt DATETIME; -- cannot be null
	DECLARE @td_tia_to_loc_dt DATETIME; -- cannot be null
	DECLARE @td_tib_from_loc_dt DATETIME; -- cannot be null
	DECLARE @td_tib_to_loc_dt DATETIME; -- cannot be null
	DECLARE @td_StartWD_To_Arg BIGINT; -- cannot be null;
	DECLARE @td_Arg_To_EndWD BIGINT; -- cannot be null;
	DECLARE @td_in_a BIT; -- cannot be null;
	DECLARE @td_in_b BIT; -- cannot be null;
	DECLARE @td_ti_seq INT; -- cannot be null;
	DECLARE @td_res_inA_loc DATETIME; -- cannot be null
	DECLARE @td_res_inB_loc DATETIME; -- cannot be null
	DECLARE @td_arg_loc NVARCHAR(MAX); -- debug only

	DECLARE @ti_endwd_cor BIGINT; --cannot be null

	IF (@do_add = 1)
	BEGIN
		SET @expr1x = @md_arg_wrk_sec + @wrk_offset_sec;
			
	END
	ELSE
	BEGIN
		SET @expr1x = @md_arg_wrk_sec - ISNULL(@wrk_offset_sec, 0) + 1 -- 1:reducer

	END

	IF (@do_add = 1)
	BEGIN
		--    DROP PROCEDURE [dbo].[hlsyscal_calculate_offset];

			SELECT @cc_tis1_txt = CONCAT(ISNULL(cor_key,N'<null>')
				  --, N' cor(',ti_endwd_cor,N')'
				  , N' @fd_arg_loc=', @fd_arg_loc, N''
				  , N' @md_arg_loc=', @md_arg_loc, N''
				  , N' cc_arg_loc:',cc_arg_loc
				  , N' dff_days:', dff_days

				  , N' cc_dayno:', cc.dayno_wrk
				  
				  --, N' days_e_tis:', days_e_tis
				  , N' days_tis:', days_tis
				  , N' days:',(cc.dayno_wrk - @md_dayno_wrk)
				  , N' tis:', (cc.ti_seq - @md_ti_seq)
				  , N' cc_non_endwd:', cc_non_endwd
				  --tia_before 
				  --, N',tia_in:', IIF(@expr1x >= cc.tia_from_wrk_sec AND @expr1x < cc.tib_to_wrk_sec, N'1', N'0')
				  --, N',tib_in:', IIF(@expr1x >= cc.tib_from_wrk_sec AND @expr1x < cc.tib_to_wrk_sec, N'1', N'0')
				  --, N',tib_af:', IIF(@expr1x = tib_to_wrk_sec, N'1',N'0')
				  , N',onBtY:', onBtY
				  --, N',cc_rng_bt:', cc_rng_bt
				  , N',dff_ti_seq:', dff_ti_seq
				  , N',dff_endwd:', dff_endwd
				  --, N',nxt_tib_to:', nxt_tib_to
				  --, N',remainx:', remainx
				  --, N',cc.ti_cnt:', cc.ti_cnt
				  
				  --, N',corr_wh03:', IIF((remainwrksec = 1) AND (@nd_is_endwd=0), 1, 0)
				  
				  , N',remainwrksec:', remainwrksec
				  )
				  ,@cc_tis1_key=cor_key

				, @ti_endwd_cor = (dff_ti_seq) - (dff_endwd) + ISNULL(cor.ti_endwd_cor, 0)
				, @cc_dayno_wrk = cc.dayno_wrk

--    DROP PROCEDURE [dbo].[hlsyscal_calculate_offset];

			FROM [dbo].[hlsyscalcachecalendarday] AS cc
			CROSS APPLY (
				SELECT dff_ti_seq = cc.ti_seq - @md_ti_seq
				     , dff_endwd = cc.on_endwd_seq - @md_on_endwd_seq
					 , dff_days = (cc.dayno_wrk - @md_next_dayno_wrk)
					 , days_tis = (cc.dayno_wrk - @md_dayno_wrk) - (cc.ti_seq - @md_ti_seq)
					 , cc_non_endwd = (cc.dayno_wrk - @md_dayno_wrk) - (cc.on_endwd_seq - @md_on_endwd_seq)
					 --?--, days_e_tis = IIF((cc.dayno_wrk - @md_dayno_wrk) = (cc.ti_seq - @md_ti_seq), 1, 0)
					 --?--, nxt_tib_to = (cc.ti_seq - @md_ti_seq) - (cc.on_endwd_seq - @nd_on_endwd_seq) --> dff_ti_seq - dff_endwd -- next drops before/on/after tib_to (Less/Equal/Greater)

					 --?--, cc_rng_bt = cc.tib_to_wrk_sec - @expr1x -- 0, 1,>1   range to the of the cc.day 

					 --?--, cc_tia_in = IIF(@expr1x >= cc.tia_from_wrk_sec AND @expr1x<cc.tia_to_wrk_sec, 1, 0)
					 --?--, cc_tib_in = IIF(cc.ti_cnt = 2 AND @expr1x >= cc.tib_from_wrk_sec AND @expr1x<cc.tib_to_wrk_sec, 1, 0)
					 , remainwrksec = (@wrk_offset_sec - @md_Arg_To_EndWD)
					 , onBtY = (@expr1x-cc.tib_to_wrk_sec)
					 --?--, remainx = (@wrk_offset_sec - @nd_Arg_To_EndWD) - cc.day_wrksec_period -- cc day is enough or not for the remains
					    , cc_arg_loc = CASE WHEN @expr1x < cc.tia_from_wrk_sec THEN N'TBA' -- BeforeA
											WHEN @expr1x = cc.tia_from_wrk_sec THEN N'TSA' -- StartA
											WHEN @expr1x > cc.tia_from_wrk_sec AND @expr1x < cc.tia_to_wrk_sec THEN N'TIA' -- InA
											WHEN @expr1x = cc.tia_to_wrk_sec THEN N'TEA' -- EndA
											WHEN cc.ti_cnt = 2 AND @expr1x > cc.tia_to_wrk_sec AND @expr1x < cc.tib_from_wrk_sec THEN N'TXP'
											WHEN cc.ti_cnt = 2 AND @expr1x = cc.tib_from_wrk_sec THEN N'TSB' -- StartB
											WHEN cc.ti_cnt = 2 AND @expr1x > cc.tib_from_wrk_sec AND @expr1x < cc.tib_to_wrk_sec THEN N'TIB' -- InB
											WHEN cc.ti_cnt = 2 AND @expr1x = cc.tib_to_wrk_sec THEN N'TEB' -- EndB
											ELSE N'TAB' END -- AfterB

			) AS calc
			OUTER APPLY ( -- aec : (A)of (E)xpression (C)orrection
			SELECT cor_key, ti_endwd_cor FROM (
			VALUES(NULL, NULL, 0)
		,(N'TIA_TSA_X_0_LE1_GRX', (-2)    , IIF(( @md_arg_loc=N'TIA' AND cc_arg_loc=N'TSA' AND (dff_days>0) AND (days_tis)=(0) AND (cc_non_endwd) =(1) AND (onBtY) >(-86398)),1,0))
		,(N'TIA_TIA_X_0_GR1_GRX', (-1)    , IIF(( @md_arg_loc=N'TIA' AND cc_arg_loc=N'TIA' AND (dff_days>0) AND (days_tis)=(0) AND (cc_non_endwd) >(1) AND (onBtY) >(-86398)),1,0))
		,(N'TIA_TSA_X_0_EQ0_LEX', (-1)    , IIF(( @md_arg_loc=N'TIA' AND cc_arg_loc=N'TSA' AND (dff_days>0) AND (days_tis)=(0) AND (cc_non_endwd) =(0) AND (onBtY)<=(-86398)),1,0))
		,(N'TIA_TEA_X_0_LE1_GRX', (-1)    , IIF(( @md_arg_loc=N'TIA' AND cc_arg_loc=N'TEA' AND (dff_days>0) AND (days_tis)=(0) AND (cc_non_endwd)=(1) AND (onBtY) >(-86398)),1,0))
		,(N'TSB_TEB_0_0_LE1_GRX', (+2)    , IIF(( @md_arg_loc=N'TSB' AND cc_arg_loc=N'TEB' AND (dff_days=0) AND (days_tis)=(0) AND (cc_non_endwd)=(0) AND (onBtY) >(-86398)),1,0))
		,(N'TIB_TEB_0_0_LE1_GRX', (+2)    , IIF(( @md_arg_loc=N'TIB' AND cc_arg_loc=N'TEB' AND (dff_days=0) AND (days_tis)=(0) AND (cc_non_endwd)=(0) AND (onBtY) >(-86398) AND (onBtY != 0) ),1,0))
		,(N'TIB_TEB_0_0_LE1_EQ0_GR1', (+1), IIF(( @md_arg_loc=N'TIB' AND cc_arg_loc=N'TEB' AND (dff_days=0) AND (days_tis)=(0) AND (cc_non_endwd)=(0) AND (onBtY) =(0) AND @wrk_offset_sec > 1 ),1,0)) -- +1:TCxAof1006, +1:TCxAst_2602_20150303185959__d0_h2_m0_s1
		,(N'TSB_TIB_X_L_LE1_GRX', (+2)    , IIF(( @md_arg_loc=N'TSB' AND cc_arg_loc=N'TIB' AND (dff_days>0) AND (days_tis)<(0) AND (cc_non_endwd)=(1) AND (onBtY) >(-86398)),1,0)) -- BUG_sw88x36000x_Aof
		,(N'TIB_TEB_0_0_EQ0_EQ0_EQ1_EQ1', (+0), IIF(( @md_arg_loc=N'TIB' AND cc_arg_loc=N'TEB' AND (dff_days=0) AND (days_tis)=(0) AND (cc_non_endwd)=(0) AND (onBtY) =(0) AND @wrk_offset_sec = 1 AND @fd_is_endwd=1),1,0)) -- +2:TCxAst_2601_20150303135959__d0_h0_m0_s1, 0:TCxAst_103747_20120229225959__d1_h0_m0_s1
		,(N'TIB_TEB_0_0_EQ0_EQ0_EQ1_EQ0', (+1), IIF(( @md_arg_loc=N'TIB' AND cc_arg_loc=N'TEB' AND (dff_days=0) AND (days_tis)=(0) AND (cc_non_endwd)=(0) AND (onBtY) =(0) AND @wrk_offset_sec = 1 AND @fd_is_endwd=0),1,0)) -- +2:TCxAst_2601_20150303135959__d0_h0_m0_s1, 0:TCxAst_103747_20120229225959__d1_h0_m0_s1

				) AS mtrx(cor_key, ti_endwd_cor, cnd)
				WHERE cnd=1
		
--    DROP PROCEDURE [dbo].[hlsyscal_calculate_offset];

			) AS cor

			WHERE calendarid = @calendarid
			AND @expr1x BETWEEN cc.tia_from_wrk_sec AND cc.tib_to_wrk_sec;

			IF (@cc_dayno_wrk < @md_next_dayno_wrk)
			BEGIN 
				SET @error_text = CONCAT(N'arg dayno <ADD> wrong. @md_next_dayno_wrk:', @md_next_dayno_wrk , N', @cc_dayno_wrk:', @cc_dayno_wrk);
				;THROW 50004, @error_text , 1;
			END

			---SELECT [@ti_endwd_cor] = @ti_endwd_cor;

			SET @expr2x = @expr1x + @ti_endwd_cor;

			SELECT @td_tia_from_wrk_sec = cc.tia_from_wrk_sec
			     , @td_tia_to_wrk_sec   = cc.tia_to_wrk_sec
			     , @td_tib_from_wrk_sec = cc.tib_from_wrk_sec
				 , @td_tib_to_wrk_sec   = cc.tib_to_wrk_sec
			     , @td_tia_from_loc_dt  = cc.tia_from_loc_dt
				 , @td_tia_to_loc_dt    = cc.tia_to_loc_dt
				 , @td_tib_from_loc_dt  = cc.tib_from_loc_dt
				 , @td_tib_to_loc_dt    = cc.tib_to_loc_dt
				 , @td_dayno_wrk        = cc.dayno_wrk 
				 , @td_StartWD_To_Arg   = @expr2x - cc.tia_from_wrk_sec
				 , @td_Arg_To_EndWD     = cc.tib_to_wrk_sec - @expr2x
				 , @td_in_a = IIF(@expr2x BETWEEN cc.tia_from_wrk_sec AND cc.tia_to_wrk_sec, 1, 0)
				 , @td_in_b = IIF(@expr2x BETWEEN cc.tib_from_wrk_sec AND cc.tib_to_wrk_sec, 1, 0)
				 , @td_ti_seq = cc.ti_seq

					, @td_arg_loc = CASE WHEN @expr2x < cc.tia_from_wrk_sec THEN N'TBA' -- BeforeA
										WHEN @expr2x = cc.tia_from_wrk_sec THEN N'TSA' -- StartA
										WHEN @expr2x > cc.tia_from_wrk_sec AND @expr2x < cc.tia_to_wrk_sec THEN N'TIA' -- InA
										WHEN @expr2x = cc.tia_to_wrk_sec THEN N'TEA' -- EndA
										WHEN cc.ti_cnt = 2 AND @expr2x > cc.tia_to_wrk_sec AND @expr2x < cc.tib_from_wrk_sec THEN N'TXP'
										WHEN cc.ti_cnt = 2 AND @expr2x = cc.tib_from_wrk_sec THEN N'TSB' -- StartB
										WHEN cc.ti_cnt = 2 AND @expr2x > cc.tib_from_wrk_sec AND @expr2x < cc.tib_to_wrk_sec THEN N'TIB' -- InB
										WHEN cc.ti_cnt = 2 AND @expr2x = cc.tib_to_wrk_sec THEN N'TEB' -- EndB
										ELSE N'TAB' END -- AfterB
				
		  FROM [dbo].[hlsyscalcachecalendarday] AS cc
		  WHERE calendarid = @calendarid
			AND @expr2x BETWEEN cc.tia_from_wrk_sec AND cc.tib_to_wrk_sec;

		SET @td_res_inA_loc = DATEADD(SECOND, (@expr2x - @td_tia_from_wrk_sec), @td_tia_from_loc_dt) -- @td_in_a=1
		SET @td_res_inB_loc = DATEADD(SECOND, (@expr2x - @td_tib_from_wrk_sec), @td_tib_from_loc_dt) -- @td_in_b=1
		


		SELECT @result_datetime_loc= CASE 
					WHEN @td_arg_loc = N'TBA' THEN @td_tia_from_loc_dt
					WHEN @td_arg_loc = N'TSA' THEN @td_tia_from_loc_dt
					WHEN @td_arg_loc = N'TIA' THEN @td_res_inA_loc
					WHEN @td_arg_loc = N'TEA' THEN @td_tia_to_loc_dt
					WHEN @td_arg_loc = N'TXP' THEN @td_tib_from_loc_dt
					WHEN @td_arg_loc = N'TSB' THEN @td_tib_from_loc_dt
					WHEN @td_arg_loc = N'TIB' THEN @td_res_inB_loc
					WHEN @td_arg_loc = N'TEB' THEN @td_tib_to_loc_dt
					--WHEN @td_arg_loc = N'TAB' THEN @td_tib_to_loc_dt
					ELSE @td_tib_to_loc_dt -- N'TAB'
					END

		
		--SET @error_text = CONCAT(N'Offset <ADD> is not implemented. Calendar:', @calendarid, N'');
		--;THROW 50004, @error_text , 1;
	END
	ELSE
	BEGIN
		--SET @error_text = CONCAT(N'Offset <SUB> is not implemented. Calendar:', @calendarid, N'');
		--;THROW 50004, @error_text , 1;
		SELECT @result_datetime_loc = NULL;

	END

	DECLARE @result_dst INT = (SELECT daylightdeltasec FROM [dbo].[hlsyscal_query_localtoutcoffset](@calendar_timezone, @result_datetime_loc)); -- can be null

	SET @result_datetime_utc = DATEADD(SECOND, (-1)*(
	
			ISNULL(@timezone_baseutcoffsetsec,0) 
		  + ISNULL(@result_dst,0)
		  ), @result_datetime_loc)

	DECLARE @expected_datetime_dst INT = (SELECT daylightdeltasec FROM [dbo].[hlsyscal_query_localtoutcoffset](@calendar_timezone, @expected_datetime_utc));
	DECLARE @expected_datetime_loc DATETIME = DATEADD(SECOND, (+1)*(
			ISNULL(@timezone_baseutcoffsetsec,0) 
		  + ISNULL(@expected_datetime_dst,0)
		  ), @expected_datetime_utc)

	DECLARE @TEST_DFF INT = DATEDIFF(SECOND, @expected_datetime_utc, @result_datetime_utc);

	SELECT [TEST] = CASE WHEN @TEST_DFF= 0 THEN N'Ok'
						 WHEN @TEST_DFF=+1 THEN N'Warning+1' -- see 'AreEqualDT_Apprx1sec'
						 WHEN @TEST_DFF=-1 THEN N'Warning-1' -- see 'AreEqualDT_Apprx1sec'
						 WHEN @TEST_DFF=-3 THEN N'Warning-3' -- 'MIBA_sw002_Aof'
						 --WHEN @TEST_DFF=+3599 THEN N'Warning+3599' -- TCxAof_103704_20150317030000_595169
						 WHEN @TEST_DFF=+3600 THEN N'Warning+3600' -- TCxAof_103704_20150317030000_595169

						 ELSE N'Failed' END
	     , TEST_DFF = @TEST_DFF
		 , timezone = CONCAT(N'', @timezone_baseutcoffsetsec, N'_', @result_dst)
	     , [result_datetime_utc]    = @result_datetime_utc -- THE RESULT
		 , [@result_datetime_loc] = @result_datetime_loc
		 , [@expected_datetime_utc] = @expected_datetime_utc
		 , [@expected_datetime_loc] = @expected_datetime_loc
		 , [@expected_datetime_dst] = IIF(@expected_datetime_dst>0,1, 0)
	IF (ISNULL(@show_details, 1) = 1)
	BEGIN

	SELECT [ ] = N'dfd1'
			, [@dfd1_dayno_wrk] = @dfd1_dayno_wrk
			, [@dfd1_arg_loc] = @dfd1_arg_loc
			, [@dfd1_dt_inout_loc] = @dfd1_dt_inout_loc
			, [@dfd1_tia_before] = @dfd1_tia_before
			, [@dfd1_diff_sec_wrkdaystart_loc_dt_to_dt_inout_loc] = @dfd1_diff_sec_wrkdaystart_loc_dt_to_dt_inout_loc

	SELECT [ ] = N'dmd1'
			, [@dmd1_dayno_wrk]		= @dmd1_dayno_wrk
			, [@dmd1_arg_wrk_sec]   = @dmd1_arg_wrk_sec
			, [@dmd1_arg_loc]		= @dmd1_arg_loc
			, [@dmd1_dt_inout_raw_loc] = @dmd1_dt_inout_raw_loc
			, [@reset_time] = @reset_time
			, [@change_time] = @change_time
			, [@dmd1_dt_inout_loc]	= @dmd1_dt_inout_loc
			, [@dmd1_StartWD_To_Arg] = @dmd1_StartWD_To_Arg
			, [@dmd1_Arg_To_EndWD]	 = @dmd1_Arg_To_EndWD
			

	SELECT [ ] = N' '
	     , [@fd_dt_inout_loc] = @fd_dt_inout_loc
	     , [@fd_dayno_wrk] = @fd_dayno_wrk
		 , [fd_remain] = @wrk_offset_sec - @fd_Arg_To_EndWD  -- aof:end_wd. If sameday negative or zero
		 , [@fd_StartWD_To_Arg] = @fd_StartWD_To_Arg
		 , [@fd_Arg_To_EndWD] = @fd_Arg_To_EndWD
		 , [@fd_arg_wrk_sec] = @fd_arg_wrk_sec
		 , [@fd_tia_from_wrk_sec] = @fd_tia_from_wrk_sec
		 , [@fd_tib_to_wrk_sec] = @fd_tib_to_wrk_sec
		 , [@fd_ti_cnt] = @fd_ti_cnt
		 , [@fd_arg_loc] = @fd_arg_loc
		 , [@fd_tia_before] = @fd_tia_before
		 , [@fd_tia_in] = @fd_tia_in
		 , [@fd_tib_in] = @fd_tib_in
		 , [@fd_tib_after] = @fd_tib_after
		 , [@fd_tib_to_loc_dt] = @fd_tib_to_loc_dt
		 , [@fd_is_endwd] = @fd_is_endwd
		 , [@fd_on_endwd_cor] = @fd_on_endwd_cor

	SELECT [ ] = N' '
		 , [@fd_next_dayno_wrk] = @fd_next_dayno_wrk
		 , [@md1_dayno_wrk] = @md1_dayno_wrk
		 , [@md2_dayno_wrk] = @md2_dayno_wrk
		 , [@md_use2] = @md_use2
		 , [@md_dayno_wrk] = @md_dayno_wrk
	     , [@md_next_dayno_wrk] = @md_next_dayno_wrk
		 
		, [@md_ti_seq] = @md_ti_seq
		, [@md_arg_wrk_sec] = @md_arg_wrk_sec
		, [@md_tia_before] = @md_tia_before
		, [@md_tib_after] = @md_tib_after
		, [@md1_arg_loc] = @md1_arg_loc
		, [@md2_arg_loc] = @md2_arg_loc
		, [@md_arg_loc] = @md_arg_loc
		, [@md_Arg_To_EndWD] = @md_Arg_To_EndWD
		, [@md_dt_inout_loc] = @md_dt_inout_loc
		, [@md_tib_to_wrk_sec] = @md_tib_to_wrk_sec

	SELECT [ ] = N' '
		 , [@cc_dayno_wrk] = @cc_dayno_wrk
		 , [@cc_tis1_key] = @cc_tis1_key
		 , [@cc_tis1_txt] = @cc_tis1_txt

	SELECT [ ] = N' '

		 , [@td_dayno_wrk] = @td_dayno_wrk
		 , [@expr1x] = @expr1x
		 , [@expr2x] = @expr2x
		 , [@td_arg_loc] = @td_arg_loc

		 , [@td_res_inA_loc] = @td_res_inA_loc--, [dff_dt2] = DATEDIFF(SECOND, @expected_datetime_loc, @td_res_inA_loc)
		 , [@td_res_inB_loc] = @td_res_inB_loc--, [dff_dt3] = DATEDIFF(SECOND, @expected_datetime_loc, @td_res_inB_loc)
		 --, [@res_dt4_loc] = @res_dt4_loc
		 --, [@res_dt5_loc] = @res_dt5_loc
		 --, [@res_dt6_loc] = @res_dt6_loc

		 , [@td_StartWD_To_Arg] = @td_StartWD_To_Arg
		 , [@td_Arg_To_EndWD] = @td_Arg_To_EndWD
		 , [@td_in_a] = @td_in_a
		 , [@td_in_b] = @td_in_b
		 --, td_a_arg = @expr2x - @td_tia_from_wrk_sec


END
END
/*
	DROP PROCEDURE [dbo].[hlsyscal_calculate_offset];
*/
