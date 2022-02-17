-- @Name LoadAgentGlobalSetPermissions
-- @Return ClrTypes:Security.AgentGlobalPermission
CREATE
--OR ALTER
PROCEDURE [dbo].[hlsyssec_load_agentglobalsetpermissions] -- used by 'IRuntimeConfiguration.LoadSecurities()'
(
	  @agentid INT -- not a system agentid : (agentid<1500 AND agentid>2000)
	, @globalids [dbo].[hlsys_udt_idset] READONLY
)
AS
BEGIN
	DECLARE @error_text NVARCHAR(MAX);

	IF (@agentid BETWEEN 1500 AND 2000)
	BEGIN
		SET @error_text = CONCAT(N'System agent is not expected. @agentid=', @agentid);
		;THROW 50000, @error_text, 1;
	END

	-- validate globalids
	DECLARE @invalid_globalid INT;
	SELECT TOP 1 @invalid_globalid = gid.id
	  FROM @globalids AS gid
	LEFT OUTER JOIN dbo.hlsysglobalpolicy AS gp ON gid.id = gp.globalid
	WHERE gp.globalid IS NULL;
  
	IF (@invalid_globalid IS NOT NULL) 
	BEGIN
		SET @error_text = CONCAT(N'Unknown globalid:', @invalid_globalid);
		;THROW 50000, @error_text, 1;
	END

	-- check agentid
	IF (NOT EXISTS (SELECT 1 FROM [dbo].hlsysagent AS agn WHERE agn.agentid = @agentid))
	BEGIN
		SET @error_text = CONCAT(N'Agent could not be found:', @agentid);
		;THROW 50000, @error_text, 1;
	END

	-- collect masks
	IF EXISTS(SELECT 1 FROM @globalids)
	BEGIN
	    SELECT globalid = gid.id, accessmask = CAST(ISNULL(am.accessmask, 0) AS SMALLINT)
	     FROM @globalids AS gid
	     OUTER APPLY [dbo].[hlsyssec_query_agentglobalmsk](@agentid, gid.id) AS am
	END
	ELSE
	BEGIN
	    SELECT globalid = gid.globalid, accessmask = CAST(ISNULL(am.accessmask, 0) AS SMALLINT)
	     FROM [dbo].[hlsysglobalpolicy] AS gid
	     OUTER APPLY [dbo].[hlsyssec_query_agentglobalmsk](@agentid, gid.globalid) AS am
	END
END
