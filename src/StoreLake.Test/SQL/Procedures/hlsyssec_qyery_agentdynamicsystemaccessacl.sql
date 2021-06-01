-- @Name QueryAgentDynamicSystemAccessMask
-- @Return ClrTypes:short Mode:SingleOrDefault
CREATE PROCEDURE [dbo].[hlsyssec_qyery_agentsystemitemacl]
	@agentid INT,
	@globalid INT,
	@systemid INT
AS
BEGIN
	SET NOCOUNT ON;
	SET XACT_ABORT ON;

	DECLARE @error_text NVARCHAR(MAX);
	
	IF (ISNULL(@agentid,0)<=0)
	BEGIN
		SET @error_text = CONCAT(N'Invalid agent. @agentid=', @agentid, N', @globalid=',@globalid,N', @systemid=',@systemid);
		;THROW 422001, @error_text, 1 -- 422 Unprocessable Entity
	END

	IF (ISNULL(@globalid,0)<=0)
	BEGIN
		SET @error_text = CONCAT(N'Invalid policy. @agentid=', @agentid, N', @globalid=',@globalid,N', @systemid=',@systemid);
		;THROW 422001, @error_text, 1 -- 422 Unprocessable Entity
	END

	IF (ISNULL(@systemid,0)<=0)
	BEGIN
		SET @error_text = CONCAT(N'Invalid item. @agentid=', @agentid, N', @globalid=',@globalid,N', @systemid=',@systemid);
		;THROW 422001, @error_text, 1 -- 422 Unprocessable Entity
	END

	SELECT [accessmask] FROM [dbo].[hlsyssec_query_agentsystemacl](@agentid, @globalid, @systemid);
END