CREATE PROCEDURE [dbo].[hlsyscontract_increment_numberofcalls]
	@contractdefid INT
	,@contractid INT
AS
BEGIN
	SELECT CAST(N'### DB ARTIFACTS WERE NOT REBUILT AFTER DEPLOY! ###' AS BIT) -- use Server.RebuildModelApp.exe
	FROM (VALUES(@contractdefid, @contractid)) AS [x]([contractdefid], [contractid])
END