/*
 
   DROP PROCEDURE [dbo].[hlsyscal_calculate_difference_formatted];
 
 */

-- @Namespace Calendar
-- @Name CalendarCalculateDifferenceFormatted
-- @Return ClrTypes:Calendar.CalendarCalculateDifferenceFormattedResult
CREATE PROCEDURE [dbo].[hlsyscal_calculate_difference_formatted]
(
	@calendarid INT,
	@fromtime_utc DATETIME,
	@totime_utc DATETIME
)
AS
BEGIN
	SET NOCOUNT ON;
	SET XACT_ABORT ON;

	IF (@fromtime_utc IS NULL)
	BEGIN
		;THROW 50000, N'Null argument <from>', 1
	END

	IF (@totime_utc IS NULL)
	BEGIN
		;THROW 50000, N'Null argument <to>', 1
	END

	IF (( (YEAR(@fromtime_utc)<=1970) OR (YEAR(@totime_utc)<=1970) ))
	BEGIN
	SELECT [ResultDaysCompensated] = 0
	     , [ResultDiffSecondsCompensated] = 0
		 , [ResultTotalSecondsCompensated] = 0
		 , [ResultNothing] = CAST(1 AS BIT)
	 END
	 ELSE
	 BEGIN
	SELECT [ResultDaysCompensated] = fn.result_days_compensated
		 , [ResultDiffSecondsCompensated] = fn.result_seconds_compensated
		 , [ResultTotalSecondsCompensated] = fn.result_totalsec_compensated
		 , [ResultNothing] = CAST(0 AS BIT)
	  FROM [dbo].[hlsyscal_fn_calculate_difference_formatted](0, @calendarid, IIF(@fromtime_utc<=@totime_utc, @fromtime_utc, @totime_utc), IIF(@fromtime_utc<=@totime_utc, @totime_utc, @fromtime_utc)) AS fn
	 END
END