CREATE TABLE [dbo].[hlsystimezoneadjustmentrule]
(
	[hltzid] INT NOT NULL, --<-- pk[1/2]
	[ruleno] TINYINT NOT NULL, --<-- pk[2/2]
	[datestart] DATETIME NOT NULL,
	[dateend] DATETIME NOT NULL,

	[daylighttransitionstartisfixeddaterule] BIT NOT NULL,
	[daylighttransitionstartmonth] TINYINT NOT NULL,
	[daylighttransitionstartday] TINYINT NOT NULL,
	[daylighttransitionstartweek] TINYINT NOT NULL,
	[daylighttransitionstartdayofweek] TINYINT NOT NULL,
	[daylighttransitionstarttimeofday] TIME(7) NOT NULL
    , [daylighttransitionstarttimeofdaysec] INT NOT NULL --as convert(int, datediff(second, convert(datetime, 0), convert(datetime, [daylighttransitionstarttimeofday]))) persisted

	, [daylighttransitionendisfixeddaterule] BIT NOT NULL,
	[daylighttransitionendmonth] TINYINT NOT NULL,
	[daylighttransitionendday] TINYINT NOT NULL,
	[daylighttransitionendweek] TINYINT NOT NULL,
	[daylighttransitionenddayofweek] TINYINT NOT NULL,
	[daylighttransitionendtimeofday] TIME(7) NOT NULL
    , [daylighttransitionendtimeofdaysec] INT NOT NULL --  as convert(int, datediff(second, convert(datetime, 0), convert(datetime, [daylighttransitionendtimeofday]))) persisted

	, [daylightdeltasec] SMALLINT NOT NULL

	, [southern_hemisphere] BIT NOT NULL
	, [northern_hemisphere] BIT NOT NULL
	--,

	--[daylighttransitionstartmonth_txt] as convert(nvarchar, [daylighttransitionstartmonth]) persisted,
	--[daylighttransitionstartday_txt] as convert(nvarchar, [daylighttransitionstartday]) persisted,
	----[daylighttransitionstarttimeofday_txt] as convert(nvarchar, [daylighttransitionstarttimeofday]) persisted,

	--[daylighttransitionstartmonth_txtr] as right('00' + convert(nvarchar, [daylighttransitionstartmonth]),2) persisted,
	--[daylighttransitionstartday_txtr] as right('00' + convert(nvarchar, [daylighttransitionstartday]),2) persisted,

	-- Optimization replicated columns:
	, [timezone_baseutcoffsetsec] INT NOT NULL				--> @lubo in v65up1[2019.oct.23] (is INT NULL & no migration). if migration => change it to NOT NULL --> @ml with 6.5.2 we will do migration, therefor NOT NULL
	, [timezone_supportsdaylightsavingtime] BIT NOT NULL	--> @lubo in v65up1[2019.oct.23] (is INT NULL & no migration). if migration => change it to NOT NULL --> @ml with 6.5.2 we will do migration, therefor NOT NULL

, CONSTRAINT [PK_hlsystimezoneadjustmentrule] PRIMARY KEY CLUSTERED 
(
	 [hltzid] ASC
	,[ruleno] ASC
)
, CONSTRAINT [FK_hlsystimezoneadjustmentrule_timezoneid] FOREIGN KEY([hltzid]) REFERENCES [dbo].[hlsystimezone] ([hltzid])
, CONSTRAINT [UQ_hlsystimezoneadjustmentrule_tzid_start_end] UNIQUE (
    [hltzid] ASC,
	[datestart] ASC,
	[dateend] ASC
)
, CONSTRAINT [FK_hlsystimezoneadjustmentrule_timezoneid_offset_dst] FOREIGN KEY([hltzid],[timezone_baseutcoffsetsec],[timezone_supportsdaylightsavingtime]) REFERENCES [dbo].[hlsystimezone] ([hltzid],[baseutcoffsetsec],[supportsdaylightsavingtime])

)
GO

CREATE UNIQUE NONCLUSTERED INDEX [UQ_hlsystimezoneadjustmentrule_tz_start_end] ON [dbo].[hlsystimezoneadjustmentrule]
(
	[hltzid],
	[datestart]
) INCLUDE([daylightdeltasec], [dateend], [daylighttransitionstartmonth], [daylighttransitionendmonth])
GO
