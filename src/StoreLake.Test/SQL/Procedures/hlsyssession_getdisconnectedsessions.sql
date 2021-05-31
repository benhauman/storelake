-- @Namespace SessionManagement
-- @Name GetDisconnectedSessions
-- @Return ClrTypes:System.Guid
CREATE PROCEDURE [dbo].[hlsessionmanagement_getdisconnectedsessions]
AS
BEGIN
	 ; WITH timeouts AS (
		SELECT HelplineClient = settingid - 100, Timeout = settingvalue
		FROM [dbo].[hlsysglobalsetting]
		WHERE settingid > 100 AND settingid < 120 --100:Server --> not needed
	 )

	SELECT SessionId = s.sessionid
	FROM [dbo].[hlsyssession] AS s WITH (NOLOCK)
	LEFT OUTER JOIN timeouts AS t ON s.productid = t.HelplineClient
	WHERE s.[lastvisit] < DATEADD(minute, -1 * ISNULL(t.Timeout, 20) --Default Value is 20 minutes
			, GETUTCDATE())
END
