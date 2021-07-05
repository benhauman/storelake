-- @Namespace SessionManagement
-- @Name ConnectPortal
-- @Return ClrTypes:SessionManagement.ConnectPortalResponse Mode:Single
CREATE PROCEDURE [dbo].[hlsyssession_connectportal] 
	@personacc NVARCHAR(2000)
	, @session_portalid TINYINT
	, @actioncontextid BIGINT
	, @session_version SMALLINT 
	, @session_lcid INT 
	, @session_timezone INT
	, @session_testmode  BIT
	, @session_seatname  NVARCHAR(510)
AS
BEGIN
	SET NOCOUNT ON;
	SET XACT_ABORT ON;
	
	IF @@TRANCOUNT > 0
	BEGIN
		;THROW 50000, N'An ambient transaction already exists #445.', 1;
	END
	
	DECLARE @session_agentid INT
	, @session_objectid INT
	, @session_objectdefid INT

	DECLARE @ret_agentname NVARCHAR(255)
	, @ret_personmailaddress NVARCHAR(255)
	, @ret_personsurname NVARCHAR(255)
	, @ret_persongivenname NVARCHAR(255)
	, @ret_gs_environmentname NVARCHAR(255)
	, @ret_gs_referencenoformat NVARCHAR(255)
	, @ret_gs_showtaskdesks BIT
	, @ret_gs_portalsessiontimeout_min NVARCHAR(255)
	, @tmp_canexecuteportal BIT

	SELECT @session_agentid = agn.agentid
	, @session_objectid = a2o.objectid
	, @session_objectdefid = a2o.objectdefid
	--Agent Attribute
	, @ret_agentname = agn.name
	--Person Attributes
	, @ret_personsurname = pn.name, @ret_persongivenname = gi.givenname, @ret_personmailaddress = em.emailaddress
	--Global Settings
	, @ret_gs_environmentname = gs1.EnvironmentName
	, @ret_gs_referencenoformat = gs2.ReferenceNoFormat
	, @ret_gs_showtaskdesks = IIF(gs3.ShowTaskDesks > 0, 1, 0)
	, @ret_gs_portalsessiontimeout_min =  ISNULL(gs4.IdleTimePortal, 20) --20:DefaultPortalSessionTimeout
	--Authorization
	, @tmp_canexecuteportal = sec.CanExecutePortal
	  FROM dbo.hlsysaccount AS acc
	  INNER JOIN dbo.hlsysagent AS agn 
	  ON acc.agentid = agn.agentid AND agn.active = 1
	  INNER JOIN dbo.hlsysagenttoobject AS a2o
	  ON acc.agentid = a2o.agentid
	  INNER JOIN [dbo].[hlsyspersonnamevw] AS pn
	  ON pn.personid = a2o.objectid AND pn.persondefid = a2o.objectdefid
	  INNER JOIN [dbo].[hlsyspersongivennamevw] AS gi
	  ON pn.personid = gi.personid AND pn.persondefid = gi.persondefid
	  LEFT OUTER JOIN [dbo].[hlsyspersonemailaddressvw] AS em
	  ON pn.personid = em.personid AND pn.persondefid = em.persondefid AND em.isdefault = 1
	  OUTER APPLY (
		SELECT settingvalue AS EnvironmentName FROM [dbo].[hlsysglobalsetting] WHERE settingid = 15
	  ) AS gs1
	  OUTER APPLY (
		SELECT settingvalue AS ReferenceNoFormat FROM [dbo].[hlsysglobalsetting] WHERE settingid = 5
	  ) AS gs2
	  OUTER APPLY (
		--intValue > 0
		SELECT TRY_CAST(settingvalue AS INT) AS ShowTaskDesks FROM [dbo].[hlsysglobalsetting] WHERE settingid = 17
	  ) AS gs3
	  OUTER APPLY (
		SELECT TRY_CAST(settingvalue AS INT) AS IdleTimePortal FROM [dbo].[hlsysglobalsetting] WHERE settingid = 103
	  ) AS gs4
	  CROSS APPLY (
		SELECT [canexec] AS CanExecutePortal FROM [dbo].[hlsyssec_query_agentglobalprmexec] (a2o.agentid, 784) --784:GLOBAL_SELFHELP
	  ) AS sec
	  WHERE acc.name = @personacc;

	IF(@tmp_canexecuteportal = 1)
	BEGIN
		--Create Session
		DECLARE @sessionid UNIQUEIDENTIFIER = NEWID()
				, @utcnow DATETIME = GETUTCDATE()
		INSERT INTO [dbo].[hlsyssession] (sessionid, agentid, objectid, objectdefid, productid, version, lcid, timezone, seatname, testmode, portalid, logontime, lastvisit)
		VALUES (@sessionid, @session_agentid, @session_objectid, @session_objectdefid, 3 /* HelplineClient.Portal */, @session_version, @session_lcid, @session_timezone, @session_seatname, @session_testmode, @session_portalid, @utcnow, @utcnow)


		--BookSingleLicense
		EXEC [dbo].[hlsyslic_booklicense_sync] @parms_productid = 4 --4:HLProduct.PORTAL 
			, @sessionid = @sessionid
			, @canuseperseatsesseion = 0 --no perseat for portal
			, @parms_agentId = @session_agentid, @parms_userId = @session_objectid, @parms_userDefId = @session_objectdefid
			, @actioncontextid = @actioncontextid
			, @parms_perSeatToken = NULL --not possible for portal


		SELECT 
			AgentId = @session_agentid , AgentName = @ret_agentname
			, PersonId = @session_objectid, PersonDefId = @session_objectdefid
			, PersonSurname = @ret_personsurname, PersonGivenName = @ret_persongivenname, PersonMail = @ret_personmailaddress
			, EnvironmentName = @ret_gs_environmentname, ReferenceNoFormat = @ret_gs_referencenoformat, ShowTaskDesks = @ret_gs_showtaskdesks
			, PortalSessionTimeout = @ret_gs_portalsessiontimeout_min
			, SessionId = @sessionid
			, Result = bookingstate
			, DaysSinceSaasExpiration = [clientwarningdayssincesaasexpiration]
			FROM  [dbo].[hlsyssessionlicense] 
			WHERE sessionid = @sessionid
		END

	ELSE
	BEGIN
		SELECT AgentId = @session_agentid , AgentName = @ret_agentname
			, PersonId = @session_objectid, PersonDefId = @session_objectdefid
			, PersonSurname = @ret_personsurname, PersonGivenName = @ret_persongivenname, PersonMail = @ret_personmailaddress
			, EnvironmentName = @ret_gs_environmentname, ReferenceNoFormat = @ret_gs_referencenoformat, ShowTaskDesks = @ret_gs_showtaskdesks
			, PortalSessionTimeout = @ret_gs_portalsessiontimeout_min --20:DefaultPortalSessionTimeout
			, SessionId = CAST(0x0 AS UNIQUEIDENTIFIER)
			, Result = 4 --4: ReturnValues.InsufficientRights
			, DaysSinceSaasExpiration = NULL
	END
	
END
