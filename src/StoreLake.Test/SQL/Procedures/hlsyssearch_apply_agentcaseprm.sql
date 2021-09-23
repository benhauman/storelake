-- @Name SearchApplyAgentCasePermissions
-- @Return ClrTypes:Security.SecureObjectIdentity
CREATE PROCEDURE [dbo].[hlsyssearch_apply_agentcaseprm]
	  @agentid INT
	, @ids [dbo].[hlsys_udt_inttwoset] READONLY
AS
BEGIN
	DECLARE @error_text NVARCHAR(MAX);

	IF (@agentid BETWEEN 1500 AND 2000)
	BEGIN
		SET @error_text = CONCAT(N'System agent is not expected. @agentid=', @agentid);
		;THROW 50000, @error_text, 1;
	END

	SELECT objectid = ids.va, objectdefid=ids.vb
	 FROM @ids AS ids 
	 CROSS APPLY [dbo].[hlsyssec_query_agentcaseprmreadsearch](@agentid, ids.va, ids.vb) AS sec 
	 WHERE sec.[canreadandsearch] = 1;
END