-- DROP TABLE [dbo].[hlsyscalcachecalendarday]
-- EXEC [dbo].[hlsyscal_cache_populate_calendarday]
CREATE TABLE [dbo].[hlsyscalcachecalendarday]
(
	[calendarid] INT NOT NULL,
	[calendarday_date] DATE NOT NULL, -- local date from local datetime (not UTC!)

	[daystart_utc_dt]  DATETIME NOT NULL, -- [calendarday_date]_'00:00:00' => UTC
	[dayend_utc_dt]    DATETIME NOT NULL, -- [calendarday_date]_'23:59:59' => UTC
	[daystart_loc_dt]  DATETIME NOT NULL, -- [calendarday_date]_'00:00:00'
	[dayend_loc_dt]    DATETIME NOT NULL, -- [calendarday_date]_'23:59:59'

	[dayno_utc] INT NOT NULL, -- sequence based
	[dayno_wrk] INT NOT NULL,

	[ti_cnt] TINYINT NOT NULL,
	[ti_seq] INT NOT NULL,

	[tia_from_wrk_sec]  BIGINT NOT NULL,
	[tia_to_wrk_sec]    BIGINT NOT NULL,
	[tib_from_wrk_sec]  BIGINT NOT NULL,
	[tib_to_wrk_sec]    BIGINT NOT NULL,

	[tia_from_utc_dt]   DATETIME NOT NULL,
	[tia_to_utc_dt]     DATETIME NOT NULL,
	[tib_from_utc_dt]   DATETIME NOT NULL,
	[tib_to_utc_dt]     DATETIME NOT NULL,

	--[julian] INT NOT NULL, -- ONLY FOR DEBUG / ASSERT /Cpp-Compare. should be removed after stabilization 
	[day_wrksec_period] INT NOT NULL, -- if (0..86399) => 86399 seconds at period end.  Used to calculate the work seconds in the middle
	[day_wrksec_total]  INT NOT NULL, -- if (0..86399) => 86400 seconds total for the day.

	[tia_from_loc_dt]   DATETIME NOT NULL,
	[tia_to_loc_dt]     DATETIME NOT NULL,
	[tib_from_loc_dt]   DATETIME NOT NULL,
	[tib_to_loc_dt]     DATETIME NOT NULL,

	[tia_fromtime]  INT NOT NULL,
	[tia_totime]    INT NOT NULL,
	[tib_fromtime]  INT NOT NULL,
	[tib_totime]    INT NOT NULL,

	[wrksec_seq_period] BIGINT NOT NULL, -- running total sum of [day_wrksec_period] used for the seconds_in_middle_days calculation. (TD_seq - FD_seq - TD_per)

	[on_endwd_seq]      INT NOT NULL,
	--[locutc_offset] INT NOT NULL, -- (2019.09.16 :: not used)
    CONSTRAINT PK_hlsyscalcachecalendarday PRIMARY KEY([calendarid],[calendarday_date])

    -- VALIDATION
    --, CONSTRAINT CK_cachecalendarday__tia_fromtime_wrk_utc_sec CHECK([tia_fromtime_wrk_utc_sec] = [daystart_utc_sec])
    --, CONSTRAINT CK_cachecalendarday__tia_totime_wrk_utc_sec CHECK([tia_totime_wrk_utc_sec] >)
    --, CONSTRAINT CK_cachecalendarday__tib_fromtime_wrk_utc_sec CHECK([tib_fromtime_wrk_utc_sec] = [tia_totime_wrk_utc_sec])

	-- validates that A_from is BETWEEN day_start AND day_end
	-- not possible because the daystart_utc_dt can be after utc-ed A(from)!!! , CONSTRAINT CK_hlsyscalcachecalendarday_tia_from_utc_sec CHECK([daystart_utc_dt]<=[tia_from_utc_dt] AND [tia_from_utc_dt]<=(DATEADD(SECOND, 86400-1, [daystart_utc_dt])))

	-- validates that A_to is BETWEEN A_from AND B_from
	, CONSTRAINT CK_hlsyscalcachecalendarday_tia_to_utc_sec CHECK(([tia_from_utc_dt]<=[tia_to_utc_dt]) AND [tia_to_utc_dt]<=[tib_from_utc_dt]) -- at least 1 second => '>'  and not '>='. A_from=A_to ONLY when the A_work(from) AND A_work(to) are before timezone hour(s)

	-- validates that B_from is BETWEEN A_to AND B_to
	, CONSTRAINT CK_hlsyscalcachecalendarday_tib_from_utc_sec CHECK([tia_to_utc_dt]<=[tib_from_utc_dt] AND [tib_from_utc_dt]<=[tib_to_utc_dt])

	-- validates that B_to is BETWEEN day_start AND day_end
	-- validates that B_to is AFTER or equal to B_from
	, CONSTRAINT CK_hlsyscalcachecalendarday_tib_to_utc_sec CHECK([tib_from_utc_dt]<=[tib_to_utc_dt])-- AND [tib_to_utc_dt]<=(DATEADD(SECOND, 86400-1, [wrkdaystart_utc_dt])))


	--, CONSTRAINT CK_hlsyscalcachecalendarday_ti_cnt CHECK ([ti_cnt] >= 1 AND [ti_cnt] <= 2) -- only days with work intervals (1 or 2)
	--, CONSTRAINT CK_hlsyscalcachecalendarday_day_wrksec CHECK([day_wrksec]>0) -- only days with non-empty work intervals (1 or 2)
	
)

