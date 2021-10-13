CREATE VIEW [dbo].[hlsysagentaclglobalvw] ([agentid],[globalid],[accessmask]
	--,[rcnt]
	) --WITH SCHEMABINDING 
	AS 
	WITH query_agent_global_group_permissions(agentid, globalid, groupid, perm01, perm02, perm03, perm04, perm05, perm06, perm07, perm08, perm09, perm10, perm11, perm12) AS 
	(
		SELECT ag.agentid, gp.globalid, ag.groupid
			, IIF(gp.accessmask & 0001 = 0001, 1, 0) -- 0x0001 : FIRST - @AccessMask_HL_ACCESS_READ
			, IIF(gp.accessmask & 0002 = 0002, 1, 0) -- 0x0002 : 
			, IIF(gp.accessmask & 0004 = 0004, 1, 0) -- 0x0004 : 
			, IIF(gp.accessmask & 0008 = 0008, 1, 0) -- 0x0008 : 
			, IIF(gp.accessmask & 0016 = 0016, 1, 0) -- 0x0010 : 
			, IIF(gp.accessmask & 0032 = 0032, 1, 0) -- 0x0020 : 
			, IIF(gp.accessmask & 0064 = 0064, 1, 0) -- 0x0040 : 
			, IIF(gp.accessmask & 0128 = 0128, 1, 0) -- 0x0080 : 
			, IIF(gp.accessmask & 0256 = 0256, 1, 0) -- 0x0100 : 
			, IIF(gp.accessmask & 0512 = 0512, 1, 0) -- 0x0200 : 
			, IIF(gp.accessmask & 1024 = 1024, 1, 0) -- 0x0400 : LAST @AccessMask_HL_ACCESS_HISTORY
			, IIF(gp.accessmask & 2048 = 2048, 1, 0) -- 0x0400 : not used
			--) as smallint) as accessmask
			--, COUNT_BIG(*) as [rcnt] -- @lubo:  COUNT_BIG is needed due to GROOUP BY. see http://stackoverflow.com/questions/6030143/count-big-in-indexed-view. Rules: https://technet.microsoft.com/en-us/library/ms191432.aspx
	FROM [dbo].[hlsysagenttogroup] AS ag
	JOIN [dbo].[hlsysgroupaclglobalvw] AS gp ON gp.groupid = ag.groupid
	--group by ag.agentid, gp.globalid
	)
	SELECT aprm.agentid, aprm.globalid
		,  CAST(0
			+ MAX(aprm.perm01)* 1
			+ MAX(aprm.perm02)*2
			+ MAX(aprm.perm03)*4
			+ MAX(aprm.perm04)*8
			+ MAX(aprm.perm05)*16
			+ MAX(aprm.perm06)*32
			+ MAX(aprm.perm07)*64
			+ MAX(aprm.perm08)*128
			+ MAX(aprm.perm09)*256
			+ MAX(aprm.perm10)*512
			+ MAX(aprm.perm11)*1024
			+ MAX(aprm.perm12)*2048
			AS SMALLINT) AS accessmask
		 FROM query_agent_global_group_permissions AS aprm
	GROUP BY aprm.agentid, aprm.globalid
