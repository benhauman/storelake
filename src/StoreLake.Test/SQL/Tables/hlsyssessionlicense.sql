CREATE TABLE [dbo].[hlsyssessionlicense]
(
	[sessionid] UNIQUEIDENTIFIER NOT NULL
	, [hlproduct] INT NOT NULL
	, [bookingstate] TINYINT NOT NULL --see ReturnValues Enum
	, [clientwarningdayssincesaasexpiration] INT NULL --10 - 21 --> clientWarningTrue 
	, [processedcontextid] BIGINT NULL
	, [licactionid] BIGINT NULL
	, [licactiontype] INT NULL
	, [licno] INT NULL
	, [processtext] NVARCHAR(MAX) NULL
	
  , CONSTRAINT [PK_hlsyssessionlicense] PRIMARY KEY ([sessionid], [hlproduct])
  , CONSTRAINT [CK_hlsyssessionlicense_bookingstate] CHECK (   [bookingstate] = 1  --1:Allowed/Successful
															OR [bookingstate] = 2  --2:Denied
															OR [bookingstate] = 3) --3:Failed
  , CONSTRAINT [CK_hlsyssessionlicense_hlproduct] CHECK (
		   [hlproduct] = 3  --  3:ConcurrentUse
		OR [hlproduct] = 4  --  4:Portal
		OR [hlproduct] = 5  --  5:PocketDesk
		OR [hlproduct] = 7  --  7:DashBoard
		OR [hlproduct] = 8  --  8:PerSeat
		OR [hlproduct] = 9  --  9:Telephony
		) 
  , CONSTRAINT [CK_hlsyssessionlicense_processedcontextid] CHECK	(([bookingstate] = 1 --1:Booked
																		AND [processedcontextid] IS NOT NULL
																		AND [licactionid] IS NOT NULL
																		AND [licactiontype] IS NOT NULL
																		AND [licno] IS NOT NULL
																		)
																	OR ( ([bookingstate] = 2 OR [bookingstate] = 3) --2:Denied; 3:Exception
																		AND [processedcontextid] IS NOT NULL
																		AND [licactionid] IS NULL
																		AND [licactiontype] IS NULL
																		AND [licno] IS NULL
																		)) 
  , CONSTRAINT [CK_hlsyssessionlicense_bookingstatewithtext] CHECK	( [bookingstate] <> 3 
																	  OR (
																		[bookingstate] = 3 --3:Exception
																		AND [processtext] IS NOT NULL)
																	  ) -- Exceptiontext has to be there
  , CONSTRAINT [CK_hlsyssessionlicense_clientwarningdaystofinalsaasreject] CHECK (
																	(ISNULL(licactiontype, 0) = 3 AND  --3:Saas
																					(
																							(  [clientwarningdayssincesaasexpiration] >= 10 
																							   AND [clientwarningdayssincesaasexpiration] <= 21) 
																							OR [clientwarningdayssincesaasexpiration] IS NULL
																							))
																	OR ISNULL(licactiontype, 0) <> 3 AND [clientwarningdayssincesaasexpiration] IS NULL)
  , CONSTRAINT [FK_hlsyssessionlicense_hlproduct] FOREIGN KEY ([hlproduct]) REFERENCES [dbo].[hlsyslicproduct] ([hlproduct])
  , CONSTRAINT [FK_hlsyssessionlicense_sessionid] FOREIGN KEY ([sessionid]) REFERENCES [dbo].[hlsyssession] ([sessionid])
  , CONSTRAINT [FK_hlsyssessionlicense_processedcontextid] FOREIGN KEY ([processedcontextid]) REFERENCES [dbo].[hlsysactioncontext] ([actioncontextid])
  , CONSTRAINT [FK_hlsyssessionlicense_licaction] FOREIGN KEY ([licactionid], [licactiontype], [licno], [hlproduct]) REFERENCES [dbo].[hlsyslicaction] ([id], [lictype], [licno], [hlproduct])

)
GO
CREATE UNIQUE NONCLUSTERED INDEX [UQ_hlsyssessionlicense_licactionid] ON [dbo].[hlsyssessionlicense] ([licactionid]) WHERE ([licactionid] IS NOT NULL AND [hlproduct] <> 4) -- portal can reuse licenses --> duplicate licactionids

