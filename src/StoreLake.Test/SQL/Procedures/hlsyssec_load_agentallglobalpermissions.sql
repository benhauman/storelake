-- @Name LoadAgentAllGlobalPermissions
-- @Return ClrTypes:Security.AgentGlobalPermission
CREATE PROCEDURE [dbo].[hlsyssec_load_agentallglobalpermissions] -- used by 'IRuntimeConfiguration.LoadSecurities()'
(
	@agentid INT -- not a system agentid : (agentid<1500 AND agentid>2000)
)
AS
BEGIN
	------------------------------------------------------------------------
	-- ?!? [dbo].[hlsyssec_query_agentallglobalmsk]
	-- 1. Not a system agent
	-- 2. Existing agent
	-- 3. Masks
	------------------------------------------------------------------------
	
	DECLARE @error_text NVARCHAR(MAX);

	-- 1. Not system agent
	IF (@agentid BETWEEN 1500 AND 2000)
	BEGIN
		SET @error_text = CONCAT(N'System agent is not expected. @agentid=', @agentid);
		;THROW 50000, @error_text, 1;
	END

	-- check agentid
	IF (NOT EXISTS (SELECT 1 FROM [dbo].hlsysagent AS agn WHERE agn.agentid = @agentid))
	BEGIN
		SET @error_text = CONCAT(N'Agent could not be found. @agentid=', @agentid);
		;THROW 50000, @error_text, 1;
	END

	-- collect masks
	SELECT gp.globalid, CAST(ISNULL(am.accessmask, 0) AS SMALLINT) AS accessmask
	  FROM dbo.hlsysglobalpolicy AS gp
	 LEFT OUTER JOIN [dbo].[hlsysagentaclglobalvw] AS am ON am.globalid = gp.globalid AND am.agentid = @agentid;
END
