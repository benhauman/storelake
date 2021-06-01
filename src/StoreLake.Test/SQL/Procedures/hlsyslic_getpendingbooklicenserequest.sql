-- @Namespace Licensing
-- @Name GetPendingBookLicenseRequest
-- @Return Licensing.PendingLicenseRequest Mode:SingleOrDefault
CREATE PROCEDURE [dbo].[hlsyslic_getpendingbooklicenserequest] 
AS
BEGIN
	SELECT TOP 1 SessionId = slic.sessionid, LicenseAction = 1 --logon 
				, HlProduct = slic.hlproduct, PerSeatToken = s.seatname
				, UserId = IIF(slic.hlproduct = 4 --Portal
							   OR slic.hlproduct = 13 --WebShop
							   ,  s.objectid, s.agentid)
				, UserDefId = IIF(slic.hlproduct = 4 --Portal
								OR slic.hlproduct = 13 --WebShop
								,  s.objectdefid, 0)
				, LicenseKey = N'' --used anywhere?
				, CanUsePerSeatLicense = IIF(s.productid = 1, 1, 0)
				, CountPortalUser = PortalCount.countPortalUser
	FROM [dbo].[hlsyssessionlicense] AS slic
	INNER JOIN [dbo].[hlsyssession] AS s
	ON slic.sessionid = s.sessionid
	OUTER APPLY (
		SELECT countPortalUser = COUNT(*)
		FROM [dbo].[hlsyssession]
		WHERE slic.hlproduct = 4 -- Portal
			  AND productid = slic.hlproduct 
			  AND objectid = s.objectid AND objectdefid = s.objectdefid
	) AS portalCount
	WHERE slic.bookingstate = 0

END

