-- @Name WriteObjectHierarchyACL
CREATE -- ALTER
PROCEDURE [dbo].[hlsyssec_write_objecthierarchyacl]
	 @actioncontextid BIGINT
	,@rootobjectid INT
	,@rootobjectdefid INT
	,@objectacl dbo.hlsys_udt_inttwoset READONLY -- groupid:INT,accessmask:SMALLINT
	,@associationdefids dbo.hlsys_udt_idset READONLY
AS
BEGIN
	SET NOCOUNT ON;
	SET XACT_ABORT ON;

	DECLARE @error_text NVARCHAR(MAX);

	DECLARE @agentid INT; -- valid user / 
	SELECT @agentid = actx.actionagentid FROM dbo.hlsysactioncontext AS actx WHERE actx.actioncontextid = @actioncontextid;

	IF NOT EXISTS(SELECT * FROM dbo.hlsysagent AS ag WHERE ag.agentid = @agentid)
	BEGIN
		SET @error_text = CONCAT(N'Invalid agent:', @agentid);
		;THROW 50000, @error_text, 1;
	END
		-- 1. Not system agent
	IF (@agentid BETWEEN 1500 AND 2000)
	BEGIN
		SET @error_text = CONCAT(N'System agent is not expected. @agentid=', @agentid);
		;THROW 50000, @error_text, 1; -- System agent
	END

	IF NOT EXISTS(SELECT * FROM dbo.hlsysobjectdef AS objdef WHERE objdef.objectdefid = @rootobjectdefid)
	BEGIN
		SET @error_text = CONCAT(N'Invalid root object definition. @agent:', @agentid, N',@rootobjectdefid:', @rootobjectdefid, N',@rootobjectid:',@rootobjectid);
		;THROW 50000, @error_text, 1;
	END

	IF (ISNULL(@rootobjectid,0)<=0)
	BEGIN
		SET @error_text = CONCAT(N'Invalid root object identity. @agent:', @agentid, N',@rootobjectdefid:', @rootobjectdefid, N',@rootobjectid:',@rootobjectid);
		;THROW 50000, @error_text, 1;
	END

	IF EXISTS(SELECT * FROM @objectacl AS acl WHERE acl.va = 1400 OR acl.vb>2047) -- 1400:systemgroup, 2047:@AccessMask_HL_ACCESS_ALL
	BEGIN
		SET @error_text = CONCAT(N'Invalid object ACL. @agent:', @agentid, N',@rootobjectdefid:', @rootobjectdefid, N',@rootobjectid:',@rootobjectid);
		;THROW 50000, @error_text, 1;
	END

	IF NOT EXISTS(SELECT * FROM @associationdefids)
	BEGIN
		SET @error_text = CONCAT(N'Empty association definition list specified. @agent:', @agentid, N',@rootobjectdefid:', @rootobjectdefid, N',@rootobjectid:',@rootobjectid);
		;THROW 50000, @error_text, 1;
	END

	DECLARE @assocdefs dbo.hlsys_udt_idset; -- at least one non-fixed association definition for the root object definition should be there.
	INSERT INTO @assocdefs(id)
	SELECT id FROM @associationdefids AS ad
	WHERE ad.id > 1000 -- eliminate fixed association definitions
	AND EXISTS(SELECT * FROM dbo.hlsysassociationdefpoint AS ap WHERE ap.associationdefid = ad.id AND ap.objectdefida = @rootobjectdefid) -- applicable association definition for the rootobject

	IF NOT EXISTS(SELECT * FROM @associationdefids) -- at least one non-fixed association definition for the root object definition should be there.
	BEGIN
		SET @error_text = CONCAT(N'Missing applicable root object association definition. @agent:', @agentid, N',@rootobjectdefid:', @rootobjectdefid, N',@rootobjectid:',@rootobjectid);
		;THROW 50000, @error_text, 1;
	END

	--- ** Cleanup expired (>1h) requests
	BEGIN TRANSACTION hlsecobjectacl
		DELETE oldreq FROM dbo.hlsysobjectaclinheritanceprocessing AS oldreq 
		INNER JOIN dbo.hlsysactioncontext AS actx ON oldreq.actioncontextid = actx.actioncontextid AND DATEDIFF(MINUTE,actx.creationtime, GETUTCDATE())> 60;
	COMMIT;
	CHECKPOINT;

	-- ** Collect the root identity
	INSERT INTO dbo.hlsysobjectaclinheritanceprocessing(actioncontextid, objectid, objectdefid, processstatus) VALUES(@actioncontextid, @rootobjectid, @rootobjectdefid, 0);

	-- ** Collect the root's hierachy
	--DECLARE @cnt BIGINT;
	WHILE(EXISTS(SELECT * FROM dbo.hlsysobjectaclinheritanceprocessing WHERE processstatus = 0))
	BEGIN
		--SELECT @cnt = COUNT(*) FROM hlsysobjectaclinheritanceprocessing WHERE processstatus = 0;
		--SET @logtext = CONCAT(N'query-B(',@cnt,')....');
		--RAISERROR(@logtext, 1, 0) WITH NOWAIT;
		UPDATE dbo.hlsysobjectaclinheritanceprocessing SET processstatus=1 WHERE actioncontextid = @actioncontextid AND processstatus = 0;

		INSERT INTO dbo.hlsysobjectaclinheritanceprocessing(actioncontextid, objectid, objectdefid, processstatus)
		SELECT DISTINCT @actioncontextid, ai.objectidb, ai.objectdefidb, status=0 FROM dbo.hlsysassociation AS ai
		INNER JOIN @assocdefs AS ad ON ai.associationdefid = ad.id
		INNER JOIN dbo.hlsysobjectaclinheritanceprocessing AS obja ON obja.actioncontextid = @actioncontextid AND obja.processstatus = 1 AND ai.objectida = obja.objectid AND ai.objectdefida = obja.objectdefid
		AND NOT EXISTS(SELECT * FROM dbo.hlsysobjectaclinheritanceprocessing AS objp WHERE objp.actioncontextid = @actioncontextid AND ai.objectidb = objp.objectid)

		CROSS APPLY [dbo].[hlsyssec_globalaclidforobjectdef](ai.objectdefidb) AS og
		CROSS APPLY(
			SELECT z=1 FROM dbo.hlsyssec_query_agentcaseprmread(@agentid, ai.objectidb, ai.objectdefidb) AS sec WHERE og.objectglobalid = 785 AND sec.canread = 1
		UNION ALL
			SELECT z=1 FROM dbo.hlsyssec_query_agentpopcprmread(@agentid, ai.objectidb, ai.objectdefidb, og.objectglobalid) AS sec WHERE og.objectglobalid<> 785 AND sec.canread = 1
		) AS sec_flt

		--SELECT st=status, cnt=COUNT(*) FROM @objects AS x GROUP BY status

		--RAISERROR( N'collecting....', 1, 0) WITH NOWAIT;
		UPDATE dbo.hlsysobjectaclinheritanceprocessing SET processstatus=2 WHERE actioncontextid = @actioncontextid AND processstatus = 1;
	END

	-- ** Apply ACL to the hierarchy
	DELETE todel FROM dbo.hlsysobjectace AS todel
	INNER JOIN dbo.hlsysobjectaclinheritanceprocessing AS obj ON todel.objectid = obj.objectid AND obj.actioncontextid = @actioncontextid
	WHERE NOT EXISTS(SELECT * FROM @objectacl AS acl WHERE todel.groupid = acl.va AND todel.objectid = obj.objectid);

	MERGE dbo.hlsysobjectace AS T
	USING (
	SELECT obj.objectid, obj.objectdefid, groupid = acl.va, accessmask=acl.vb , od.objecttype
	  FROM dbo.hlsysobjectaclinheritanceprocessing AS obj
	  INNER JOIN dbo.hlsysobjectdef AS od ON obj.objectdefid = od.objectdefid
	CROSS JOIN @objectacl AS acl
	WHERE obj.actioncontextid = @actioncontextid
	) AS S ON T.objectid = S.objectid AND T.groupid = S.groupid
	WHEN NOT MATCHED BY TARGET THEN
		INSERT(  groupid,  objectid,   objectdefid,   objecttype, accessmask)
		VALUES(S.groupid, S.objectid, S.objectdefid,S.objecttype,S.accessmask)
	WHEN MATCHED AND T.accessmask <> S.accessmask THEN
		UPDATE SET T.accessmask = S.accessmask
	;

	-- ** Cleanup
	DELETE oldreq FROM dbo.hlsysobjectaclinheritanceprocessing AS oldreq WHERE oldreq.actioncontextid = @actioncontextid;
END -- procedure