CREATE FUNCTION [dbo].[hlsyssec_query_agentsystemacl]
(
	@agentid INT,
	@globalid INT,
	@systemid INT
)
RETURNS @rtnTable TABLE(
	  accessmask SMALLINT NOT NULL
	, errorcode INT NOT NULL
	, errortext NVARCHAR(255)
) AS
BEGIN
--declare @agentid INT = 103348
--declare @globalid INT = 760
--declare @systemid INT = 104270
	IF (@agentid = 0)
	BEGIN
		--RAISERROR (N'Invalid agentid.', 16, 1)
		INSERT INTO @rtnTable(accessmask, errorcode, errortext) VALUES(0, 160001, N'Invalid agentid.')
		RETURN
	END

	IF (@globalid = 0)
	BEGIN
		--RAISERROR (N'Invalid globalid.', 16, 2)
		INSERT INTO @rtnTable(accessmask, errorcode, errortext) VALUES(0, 160002, N'Invalid globalid.')
		RETURN
	END

	IF (@systemid = 0)
	BEGIN
		--RAISERROR (N'Invalid systemid.', 16, 3)
		INSERT INTO @rtnTable(accessmask, errorcode, errortext) VALUES(0, 160003, N'Invalid systemid.')
		RETURN
	END

	DECLARE @result_accessmask SMALLINT = NULL

	DECLARE @AccessMask_HL_ACCESS_ALL SMALLINT = 2047 --0x7FF:2047 -- 4095:0xFFF;

	IF (@agentid BETWEEN 1500 AND 1560) -- is virtual agent?
	BEGIN
		INSERT INTO @rtnTable(accessmask, errorcode, errortext) VALUES(@AccessMask_HL_ACCESS_ALL, 0, N'')
		RETURN -- @AccessMask_HL_ACCESS_ALL; -- virtual agent!
	END

	-- check agentid
	IF (NOT EXISTS (SELECT 1 FROM [dbo].[hlsysagent] AS agn WHERE agn.agentid = @agentid))
	BEGIN
		--RAISERROR (N'Agent could not be found:%d.', 16, 4, @agentid)
		INSERT INTO @rtnTable(accessmask, errorcode, errortext) VALUES(0, 160004, N'Agent not found.')
		RETURN
	END

	--DECLARE @GlobalDefids_GLOBAL_GLOBAL_STATISTICS INT = 760;
	--DECLARE @GlobalDefids_GLOBAL_PERSONAL_STATISTICS INT = 761
	DECLARE @GlobalDefids_GLOBAL_INFOPROVIDER INT = 751;
	--DECLARE @GlobalDefids_GLOBAL_REPORTS INT = 752;
	DECLARE @GlobalDefids_GLOBAL_EXPORT INT = 753;
	DECLARE @GlobalDefids_GLOBAL_TEMPLATES INT = 754;
	--DECLARE @GlobalDefids_GLOBAL_ADHOCSEARCH INT = 755;
	DECLARE @GlobalDefids_GLOBAL_GLOBAL_SEARCHES INT = 756;
	DECLARE @GlobalDefids_GLOBAL_PERSONAL_SEARCHES INT = 757;
	DECLARE @GlobalDefids_GLOBAL_MANAGE_SYSTEMSEARCHES INT = 845;
	DECLARE @GlobalDefids_GLOBAL_SLAS INT = 791;

	DECLARE @Tbl_systemids TABLE(globalid INT, systemid INT, PRIMARY KEY(globalid, systemid))

	-- check globalid-systemid existence
	IF (@globalid = @GlobalDefids_GLOBAL_INFOPROVIDER)
	BEGIN
		; WITH query_infoprovider(systemid) AS
		(
			SELECT pg.pubgroupid--, 'g-ip-group', pg.name 
			  FROM [dbo].[hlsyspubgroup] AS pg
			 WHERE pg.pubtype = 10 AND ISNULL(pg.agentid,0) = 0 -- 10 : InfoproviderGroup
			   AND pg.pubgroupid = @systemid

			UNION ALL

			SELECT ip.infoproviderid--, 'g-ip-item', ip.filename
			  FROM [dbo].[hlsysinfoprovider] AS ip
			 WHERE ip.infoproviderid = @systemid
		)
		INSERT INTO @Tbl_systemids (globalid, systemid)
		  SELECT @globalid, infp.systemid--, infp.what, infp.name 
			FROM query_infoprovider AS infp

		IF (NOT EXISTS(SELECT 1 FROM @Tbl_systemids WHERE systemid = @systemid)) 
		BEGIN
			--RAISERROR (N'Item could not be found. Infoprovider.Id:%d', 16, 13, @systemid)
			INSERT INTO @rtnTable(accessmask, errorcode, errortext) VALUES(0, 160013, N'Infoprovider not found.')
			RETURN
		END

	END -- @GlobalDefids_GLOBAL_INFOPROVIDER
	ELSE 
	IF (@globalid = @GlobalDefids_GLOBAL_EXPORT)
	BEGIN
		; WITH query_export(systemid) AS
		(
		SELECT eg.pubgroupid--, 'g-ex-group', eg.name 
		   FROM [dbo].[hlsyspubgroup] AS eg
		  WHERE eg.pubtype = 11 AND ISNULL(eg.agentid,0)= 0 -- 11 : ExportGroup
		    AND eg.pubgroupid = @systemid
		UNION ALL
		 SELECT ei.exportid--, 'g-ex-item', ei.name
		   FROM [dbo].[hlsysexport] AS ei
		   WHERE ei.exportid = @systemid
		)
		INSERT INTO @Tbl_systemids (globalid, systemid)
		  SELECT @globalid, exrt.systemid--, exrt.what, exrt.name 
			FROM query_export AS exrt

		IF (NOT EXISTS(SELECT 1 FROM @Tbl_systemids WHERE systemid = @systemid)) 
		BEGIN
			--RAISERROR (N'Item could not be found. Export.Id:%d', 16, 15, @systemid)
			INSERT INTO @rtnTable(accessmask, errorcode, errortext) VALUES(0, 160015, N'Export not found.')
			RETURN
		END

	END -- @GlobalDefids_GLOBAL_EXPORT
	ELSE
	IF (@globalid = @GlobalDefids_GLOBAL_TEMPLATES)
	BEGIN
		; WITH query_template(systemid) AS
		(
		SELECT tmplg.pubgroupid--, 'g-tmpl-group', tmplg.name 
		   FROM [dbo].[hlsyspubgroup] AS tmplg
		  WHERE tmplg.pubtype = 13 AND ISNULL(tmplg.agentid,0) = 0 -- 13 : TemplateGroup
		    AND tmplg.pubgroupid = @systemid
		UNION ALL
		 SELECT tmpli.templateid--, 'g-tmpl-item', tmpli.name
		   FROM [dbo].[hlsystemplate] AS tmpli
		   WHERE tmpli.templateid = @systemid
		)
		INSERT INTO @Tbl_systemids (globalid, systemid)
		  SELECT @globalid, stat.systemid--, stat.what, stat.name 
			FROM query_template AS stat

		IF (NOT EXISTS(SELECT 1 FROM @Tbl_systemids WHERE systemid = @systemid)) 
		BEGIN
			--RAISERROR (N'Item could not be found. Template.Id:%d', 16, 16, @systemid)
			INSERT INTO @rtnTable(accessmask, errorcode, errortext) VALUES(0, 160016, N'Template not found.')
			RETURN
		END
	END -- @GlobalDefids_GLOBAL_TEMPLATES
	ELSE
	IF (@globalid = @GlobalDefids_GLOBAL_GLOBAL_SEARCHES)
	BEGIN
		; WITH query_global_global_search(systemid) AS
		(
		 SELECT gsrchg.pubgroupid--, 'g-srch-group', srchg.name 
		   FROM [dbo].[hlsyspubgroup] AS gsrchg
		  WHERE gsrchg.pubtype = 14 AND ISNULL(gsrchg.agentid,0) = 0 -- 14 : GlobalSearchGroup
			AND gsrchg.pubgroupid = @systemid
		UNION ALL
		 SELECT gsrchi.searchid--, 'g-srch-item', srchi.name
		   FROM [dbo].[hlsyssearch] AS gsrchi
		   WHERE gsrchi.searchid = @systemid
		)
		INSERT INTO @Tbl_systemids (globalid, systemid)
		  SELECT @globalid, stat.systemid--, stat.what, stat.name 
			FROM query_global_global_search AS stat

		IF (NOT EXISTS(SELECT 1 FROM @Tbl_systemids WHERE systemid = @systemid)) 
		BEGIN
			--RAISERROR (N'Item could not be found. GlobalSearch.Id:%d', 16, 17, @systemid)
			INSERT INTO @rtnTable(accessmask, errorcode, errortext) VALUES(0, 160017, N'Global global search not found.')
			RETURN
		END
	END -- @GlobalDefids_GLOBAL_GLOBAL_SEARCHES
	ELSE
	IF (@globalid = @GlobalDefids_GLOBAL_PERSONAL_SEARCHES)
	BEGIN
		; WITH query_global_personal_search(systemid) AS
		(
			SELECT gsrchg.pubgroupid--, 'g-srch-group', srchg.name 
			   FROM [dbo].[hlsyspubgroup] AS gsrchg
			  WHERE gsrchg.pubtype = 16 AND ISNULL(gsrchg.agentid,0) = @agentid -- 16 : PersonalSearchGroup
				AND gsrchg.pubgroupid = @systemid
		UNION ALL
			 SELECT gsrchi.searchid--, 'g-srch-item', srchi.name
			   FROM [dbo].[hlsyssearch] AS gsrchi
			   WHERE gsrchi.searchid = @systemid
		)
		INSERT INTO @Tbl_systemids (globalid, systemid)
		  SELECT @globalid, stat.systemid--, stat.what, stat.name 
			FROM query_global_personal_search AS stat

		IF (NOT EXISTS(SELECT 1 FROM @Tbl_systemids WHERE systemid = @systemid)) 
		BEGIN
			--RAISERROR (N'Item could not be found. GlobalSearch.Id:%d', 16, 17, @systemid)
			INSERT INTO @rtnTable(accessmask, errorcode, errortext) VALUES(0, 160017, N'Global personal search not found.')
			RETURN
		END
	END -- @GlobalDefids_GLOBAL_PERSONAL_SEARCHES
	ELSE
	IF (@globalid = @GlobalDefids_GLOBAL_MANAGE_SYSTEMSEARCHES)
	BEGIN
		; WITH query_global_system_search(systemid) AS
		(
			SELECT gsrchg.pubgroupid--, 'g-srch-group', srchg.name 
			   FROM [dbo].[hlsyspubgroup] AS gsrchg
			  WHERE gsrchg.pubtype = 19 -- 19 : SystemSearchGroup
				AND gsrchg.pubgroupid = @systemid
		UNION ALL
			 SELECT gsrchi.searchid--, 'g-srch-item', srchi.name
			   FROM [dbo].[hlsyssearch] AS gsrchi
			   WHERE gsrchi.searchid = @systemid
		)
		INSERT INTO @Tbl_systemids (globalid, systemid)
		  SELECT @globalid, stat.systemid--, stat.what, stat.name 
			FROM query_global_system_search AS stat

		IF (NOT EXISTS(SELECT 1 FROM @Tbl_systemids WHERE systemid = @systemid)) 
		BEGIN
			--RAISERROR (N'Item could not be found. GlobalSearch.Id:%d', 16, 17, @systemid)
			INSERT INTO @rtnTable(accessmask, errorcode, errortext) VALUES(0, 160017, N'Global system search not found.')
			RETURN
		END
	END -- @GlobalDefids_GLOBAL_MANAGE_SYSTEMSEARCHES
	ELSE
	IF (@globalid = @GlobalDefids_GLOBAL_SLAS)
	BEGIN
		; WITH query_global_search(systemid) AS
		(
		SELECT gslag.pubgroupid--, 'g-srch-group', srchg.name 
		   FROM [dbo].[hlsyspubgroup] AS gslag
		  WHERE gslag.pubtype = 6 AND ISNULL(gslag.agentid,0) = 0 -- 6 : GlobalSla
		    AND gslag.pubgroupid = @systemid
		UNION ALL
		 SELECT gslai.slaid--, 'g-srch-item', srchi.name
		   FROM [dbo].[hlsyssla] AS gslai
		)
		INSERT INTO @Tbl_systemids (globalid, systemid)
		  SELECT @globalid, stat.systemid--, stat.what, stat.name 
			FROM query_global_search AS stat

		IF (NOT EXISTS(SELECT 1 FROM @Tbl_systemids WHERE systemid = @systemid)) 
		BEGIN
			--RAISERROR (N'Item could not be found. GlobalSearch.Id:%d', 16, 17, @systemid)
			INSERT INTO @rtnTable(accessmask, errorcode, errortext) VALUES(0, 160017, N'Global sla not found.')
			RETURN
		END
	END -- @GlobalDefids_GLOBAL_SLAS
	ELSE
	BEGIN
		--RAISERROR ( N'Wrong globalId:%d', 16, 21, @globalid)
		INSERT INTO @rtnTable(accessmask, errorcode, errortext) VALUES(0, 160021, N'Wrong globalId.')
		RETURN
	END

	--SELECT * FROM @Tbl_systemids

	--IF (NOT EXISTS(SELECT 1 FROM @Tbl_systemids WHERE systemid = @systemid)) 
	--BEGIN
	--	--RAISERROR (N'Item not be found. globalId:%d, systemId:%d', 16, 22, @globalid, @systemid)
	--	INSERT INTO @rtnTable(accessmask, errorcode, errortext) VALUES(0, 160022, 'Item not be found.')
	--	RETURN
	--END

	---- try apply system acl
	--; WITH query_agent_group_sacl(systemid, groupid, accessmask) as
	--(
	--	SELECT gsacl.id, gsacl.groupid, gsacl.accessmask 
	--	  FROM [dbo].[hlsyssystemacl] as gsacl
	--	inner join hlsysagenttogroup as ag 
	--		ON ag.groupid = gsacl.groupid
 --        WHERE ag.agentid = @agentid
	--)
	--, query_global_system_group_mask(globalid, systemid, groupid, accessmask) as 
	--(
	--	SELECT stat.globalid, stat.systemid, sacl.groupid, sacl.accessmask 
	--	  FROM (SELECT @systemid AS systemid, @globalid AS globalid) as stat
	--	left outer join query_agent_group_sacl as sacl
	--		ON stat.systemid = sacl.systemid
	--	 WHERE stat.systemid = @systemid
	--)
	--, query_global_system_mask(globalid, systemid, accessmask) as 
	--(
	--	SELECT gsg.globalid, gsg.systemid
	--		,CAST( SUM(DISTINCT(CAST(bits.binval as smallint))) as smallint) as accessmask
	--	  FROM query_global_system_group_mask as gsg
	--	 INNER JOIN [dbo].[hlsysbits] as bits 
	--	  ON gsg.accessmask & binval = binval
	--	  GROUP BY gsg.globalid, gsg.systemid
	--)
	--SELECT @result_accessmask = gs.accessmask 
	--	FROM query_global_system_mask as gs
	
	---- apply global acl if on system level acl has't been defined
	--IF (@result_accessmask IS NULL) 
	--BEGIN
	--	SELECT @result_accessmask = agm.accessmask 
	--	  FROM [dbo].[hlsysagentaclglobalvw] as agm
	--	 WHERE agm.agentid = @agentid and agm.globalid = @globalid

	--	IF (@result_accessmask IS NULL) 
	--	BEGIN
	--		--RAISERROR (N'Access mask could not be calculated. globalId:%d, systemId:%d, agentid:', 16, 23, @globalid, @systemid, @agentid)
	--		--INSERT INTO @rtnTable(accessmask, errorcode, errortext) VALUES(0, 160023, 'Access mask could not be calculated.')
	--		-- NOACCESS !!! no agent-group mapping or no group rights at all or group access is NO
	--		INSERT INTO @rtnTable(accessmask, errorcode, errortext) VALUES(0, 0, '')
	--		RETURN
	--	END
	--END

	; WITH query_am AS
	(
		SELECT COALESCE(sacl.accessmask, gacl.accessmask, 0) AS am
		  FROM [dbo].[hlsysagenttogroup] AS a2g
		  LEFT OUTER JOIN [dbo].[hlsyssystemacl] AS sacl ON sacl.id = @systemid AND sacl.groupid = a2g.groupid
		  LEFT OUTER JOIN [dbo].[hlsysglobalacl] AS gacl ON gacl.id = @globalid AND gacl.groupid = a2g.groupid --AND sacl.groupid IS NULL
		  WHERE a2g.agentid = @agentid
	)
	, query_finalmask AS
	(
	    SELECT CONVERT(SMALLINT, 0
			 + 0x0001*MAX(IIF(aoa_g.am & 0x0001 = 0x0001, 1, 0))
			 + 0x0002*MAX(IIF(aoa_g.am & 0x0002 = 0x0002, 1, 0))
			 + 0x0004*MAX(IIF(aoa_g.am & 0x0004 = 0x0004, 1, 0))
			 + 0x0008*MAX(IIF(aoa_g.am & 0x0008 = 0x0008, 1, 0))
			 + 0x0010*MAX(IIF(aoa_g.am & 0x0010 = 0x0010, 1, 0))
			 + 0x0020*MAX(IIF(aoa_g.am & 0x0020 = 0x0020, 1, 0))
			 + 0x0040*MAX(IIF(aoa_g.am & 0x0040 = 0x0040, 1, 0))
			 + 0x0080*MAX(IIF(aoa_g.am & 0x0080 = 0x0080, 1, 0))
			 + 0x0100*MAX(IIF(aoa_g.am & 0x0100 = 0x0100, 1, 0))
			 + 0x0200*MAX(IIF(aoa_g.am & 0x0200 = 0x0200, 1, 0))
			 + 0x0400*MAX(IIF(aoa_g.am & 0x0400 = 0x0400, 1, 0))
			 + 0x0800*MAX(IIF(aoa_g.am & 0x0800 = 0x0800, 1, 0))
			 ) AS finalmask
		FROM query_am AS aoa_g
	)
	SELECT @result_accessmask = IIF( (@agentid BETWEEN 1500 AND 1560), 2047, CONVERT(SMALLINT, ISNULL(fm.finalmask, anog.accessmask)))-- AS accessmask
	   --, CONVERT(INT, CASE 
				--    WHEN @agentid  = 0 THEN 160001
				--    WHEN @globalid = 0 THEN 160002
				--    WHEN @systemid = 0 THEN 160003
				--    ELSE 0 END) AS errorcode
	   --, CONVERT(nvarchar(255), N'') AS errortext
	FROM (VALUES(0)) AS anog (accessmask) -- agent-no-group
	OUTER APPLY query_finalmask AS fm	   

	--SELECT * FROM [dbo].[hlsyssystemacl] WHERE id = @systemid
	INSERT INTO @rtnTable(accessmask, errorcode, errortext) VALUES(@result_accessmask, 0, N'')
	RETURN -- @result_accessmask
END