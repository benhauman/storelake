CREATE VIEW [dbo].[hlsysgroupaclglobalvw]
(
	 [globalid]
	,[groupid]
	,[accessmask]
) --WITH SCHEMABINDING 
	AS 
	--SELECT 
	--[groupid],
	--[globalid],
	--[accessmask]
	--FROM [dbo].[hlsysgroupaclglobal]
		SELECT gacl.id, gacl.groupid, gacl.accessmask
		  FROM [dbo].[hlsysglobalacl] AS gacl
		 WHERE gacl.groupid != 1400 /*@HL_GROUPID_SYSTEM*/
	UNION ALL
		SELECT gp.globalid, 1400, 2047 /*@AccessMask_HL_ACCESS_ALL*/
		  FROM [dbo].[hlsysglobalpolicy] AS gp