-- @Namespace SessionManagement
-- @Name ConnectAddon
-- @Return ClrTypes:SessionManagement.ConnectAddonResponse Mode:Single
CREATE PROCEDURE [dbo].[hlsyssession_connectaddon] 
	@sessionid UNIQUEIDENTIFIER
	, @actioncontextid BIGINT
	, @hlclientid INT
AS
BEGIN
	SET NOCOUNT ON;
	SET XACT_ABORT ON;
		
	DECLARE 
		@agentId INT
		, @userId INT
		, @userDefId INT

	DECLARE
		@perSeatToken NVARCHAR(800)
	  , @isIn2Desk BIT  = NULL
	  , @productid INT 
	  , @canUsePerSeatSession BIT
   
   SELECT TOP 1 
	  @perSeatToken = s.seatname --needed on addonConnect ??
	, @isIn2Desk = IIF(s.productid = 2, 1, 0) --2:In2Desk
	, @canUsePerSeatSession = 0 --addon cannot use it
	, @productid = c2p.hlproduct
	, @agentId = agentid
	, @userId = objectid
	, @userDefId = objectdefid
   FROM [dbo].[hlsyssession] AS s
   LEFT JOIN [dbo].[hlsyslicclient] AS c2p
   ON @hlclientid = c2p.hlclient
   WHERE s.sessionid = @sessionid
   
   DECLARE @error_text NVARCHAR(MAX)
   IF (@isIn2Desk IS NULL)
   BEGIN
		SET @error_text = CONCAT(N'Session Not Found for connectAddon booklicense request <@sessionid>:', @sessionid);
		;THROW 50001, @error_text, 1;
	END

	IF NOT (@productid = 7		-- DASHBOARD
			OR @productid = 9	-- TELEPHONY
			OR @productid = 13	-- PORTALWEBSHOP
			)
	BEGIN
		SET @error_text = CONCAT(N'No addon connect supported <@hlclientid>:', @hlclientid, N' <@productid>: ', @productid);
		;THROW 50001, @error_text, 1;
	END

	IF (@productid = 13)	-- PORTALWEBSHOP
	BEGIN 
		-- only check liccounter enabled
		DECLARE @dt_now DATETIME = GETUTCDATE();
		IF EXISTS (SELECT 1 FROM [dbo].[hlsysliccounter] AS lc WHERE (hlproduct =13) --13:HLProduct.WebShop
				AND enabled = 1 
				AND (  
						(lictype = 4 AND @dt_now >= [starts] AND @dt_now <= [expires] ) --4:LicKind.Eval need to check time
					OR	(lictype = 1) -- 1:Corporate
				))						
		BEGIN
			SELECT 1 AS Result, NULL AS DaysToFinalSaasExpiration  --1:Denied
		END
		ELSE
		BEGIN
			SELECT 2 AS Result, NULL AS DaysToFinalSaasExpiration  --2:Successful
		END
	END
	ELSE --7:Dashboard; 9:Telephony
	BEGIN
		--check for already booked license
		IF EXISTS (SELECT 1 FROM [dbo].[hlsyssessionlicense] WHERE sessionid = @sessionid AND hlproduct = @productid)
		BEGIN
			--Already logged on for that product
			SELECT [bookingstate] AS Result, NULL AS DaysToFinalSaasExpiration FROM [dbo].[hlsyssessionlicense] WHERE sessionid = @sessionid AND hlproduct = @productid
		END
		ELSE
		BEGIN
			--BookSingleLicense
			EXEC [dbo].[hlsyslic_booklicense_sync] @parms_productid = @productid , @sessionid = @sessionid, @canuseperseatsesseion = @canUsePerSeatSession
				, @parms_agentId = @agentId, @parms_userId = @userId, @parms_userDefId = @userDefId, @actioncontextid = @actioncontextid, @parms_perSeatToken = @perSeatToken;

			--only one row
			SELECT TOP 1 [bookingstate] AS Result, [clientwarningdayssincesaasexpiration] AS DaysToFinalSaasExpiration 
			FROM  [dbo].[hlsyssessionlicense] 
			WHERE sessionid = @sessionid AND hlproduct = @productid 
		END
	END
END
