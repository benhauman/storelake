-- @Name CaseInsertInstance
CREATE -- ALTER
PROCEDURE [dbo].[hlsyscase_insert_instance]
    @actioncontextid BIGINT
  , @casedefid       INT
  , @caseid          INT
  , @agentidowner    INT
  , @channel         SMALLINT
  , @controller      UNIQUEIDENTIFIER
  , @workflowdefid   INT
  , @workflowversion INT
  , @creationtime     DATETIME
  , @registrationtime DATETIME
  , @referencenumber NVARCHAR(50)
  , @lastmodified DATETIME
  , @calendarid INT
  , @internalstate INT
  , @reservedby INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

	DECLARE @error_text NVARCHAR(MAX);

	DECLARE @context_actioncontextid BIGINT;
	DECLARE @context_agentid INT;
	SELECT @context_actioncontextid = actioncontextid, @context_agentid = agentid
	FROM [dbo].[hlsysgetusercontext]()

	IF (ISNULL(@context_actioncontextid, -1) <> ISNULL(@actioncontextid, -2) OR NOT EXISTS(SELECT NULL FROM dbo.hlsysactioncontext AS x WHERE x.actioncontextid=@actioncontextid))
	BEGIN
		SET @error_text = CONCAT(N'Invalid context id. @context_actioncontextid=', @context_actioncontextid, N', @actioncontextid=[', @actioncontextid, N']');
		THROW 50000, @error_text, 1;
	END

	IF (NOT EXISTS(SELECT NULL FROM dbo.hlsysinternalstate AS x WHERE x.listitemid=@internalstate))
	BEGIN
		SET @error_text = CONCAT(N'Invalid internal state id. @internalstate=[', @internalstate, N']');
		THROW 50000, @error_text, 1;
	END

	IF (ISNULL(@reservedby,-1)<0)
	BEGIN
		SET @error_text = CONCAT(N'Invalid reservedby. @reservedby=[', @reservedby, N']');
		THROW 50000, @error_text, 1;
	END
	IF (@reservedby > 0 AND NOT EXISTS(SELECT NULL FROM dbo.hlsysagent AS x WHERE x.agentid=@reservedby))
	BEGIN
		SET @error_text = CONCAT(N'Invalid reservedby agentid. @reservedby=[', @reservedby, N']');
		THROW 50000, @error_text, 1;
	END

	IF (NOT EXISTS(SELECT NULL FROM dbo.hlsyscalendar AS x WHERE x.type=2 AND x.calendarid=@calendarid))
	BEGIN
		SET @error_text = CONCAT(N'Invalid service time calendarid. @calendarid=[', @calendarid, N']');
		THROW 50000, @error_text, 1;
	END

    DECLARE @controller_text NVARCHAR(50) = CAST(@controller AS NVARCHAR(50));
    DECLARE @workflowinstance UNIQUEIDENTIFIER = IIF(@controller_text = N'79F2D5E4-0307-44D3-AF55-51D16604C97B', NULL, @controller);

	IF (IIF(@controller_text = N'79F2D5E4-0307-44D3-AF55-51D16604C97B',1,0)=1)
	BEGIN
		SET @error_text = CONCAT(N'Adhoc case controller is no more supported. @casedefid=[', @casedefid, N']');
		THROW 50000, @error_text, 1;
	END

	DECLARE @securitymode TINYINT = (SELECT cd.[securitymode] FROM [dbo].[hlsyscasedef] AS cd WHERE cd.casedefid = @casedefid);

    INSERT INTO [dbo].[hlsyscasesystem]([caseid],[casedefid],[securitymode],[channel],[channelrefvalue],[referencenumber],[owner],[controller],[workflowinstance],[workflowdefid],[workflowversion],[creationtime],[registrationtime],[version],[lastmodified],[internalstate],[calendarid])
    VALUES(@caseid, @casedefid,@securitymode,@channel,@channel,@referencenumber,@agentidowner,@controller_text,@workflowinstance,@workflowdefid,@workflowversion,@creationtime,@registrationtime,1,@lastmodified,@internalstate,@calendarid);
END