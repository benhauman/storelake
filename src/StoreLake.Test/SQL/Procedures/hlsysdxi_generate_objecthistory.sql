CREATE PROCEDURE [dbo].[hlsysdxi_generate_objecthistory]
    -- INPUT context ---
    @contextid BIGINT , --NOT NULL,
    @starttime DATETIME , --NOT NULL,
    @lasttime DATETIME ,-- NOT NULL, -- initial is equal to StartTime. After that should be updated for long running imports / activities
    @agentid INT , -- NOT NULL,
    @channelid INT,  --NOT NULL
	@actioncontextid BIGINT,
    @dodelete BIT = 0,
    @doinsert BIT = 1,
    -- INPUT changes
    @object_changes [dbo].[hlsysdxi_udt_objectchange] READONLY
AS
BEGIN
    SET NOCOUNT ON;

--DECLARE @dowrite BIT = 0;

DECLARE @input_changes TABLE
--CREATE TABLE #input_changes
(
    changecontextid BIGINT NOT NULL, -- known
	changeentrytime DATETIME NOT NULL DEFAULT(GETUTCDATE()),

    actiontypeid TINYINT NOT NULL, -- 1:INSERT -> New, 2:UPDATE -> Mod, 3:DELETE -> Del

	objecttype INT NOT NULL,
    instanceid INT NOT NULL, -- PK/FK
	contentid INT NOT NULL, -- PK or NULL

	objectdefid INT NOT NULL, -- static
	issu BIT NOT NULL DEFAULT(0),

     --tableid INT NOT NULL, -- not required
	--columnid INT NOT NULL, -- not required
     --tablename sysname NOT NULL,
     --columnname sysname NOT NULL,

--	attrbasetype tinyint NOT NULL,
    valuetypeid TINYINT NOT NULL, -- sys.types: 

	v_int INT NULL,
	v_nvarchar NVARCHAR(MAX) NULL,
	v_decimal DECIMAL(38,16) NULL,
	v_datetime DATETIME NULL,
	v_bit BIT NULL,

	-------------------------
	---- (+)CACHE(+)  -------
	-------------------------
	attrpathid INT NOT NULL,
	odedefid INT NOT NULL,
	attrpath_multiple BIT NOT NULL,

	attr_defid INT NOT NULL,
	parent_defid INT NULL,
	gparent_defid INT NULL,
	ggparent_defid INT NULL,

	ode_name NVARCHAR(200) NOT NULL,
	attr_name NVARCHAR(200) NULL,
	parent_name  NVARCHAR(200) NULL,
	gparent_name  NVARCHAR(200) NULL,
	ggparent_name  NVARCHAR(200) NULL,

	--attrpath_rn INT NOT NULL, -- HLOBJECTINFO.ID is in every table but it is needed only once
	attrvalue_text NVARCHAR(MAX) NULL,
	actiontype_text NVARCHAR(3) NOT NULL

	--attrvalue_text AS (
	--   CASE valuetypeid  -- convert to xml text
	--		 WHEN  56 THEN ISNULL(CAST(v_int AS NVARCHAR(MAX)), N'') -- [null listitem:{TC_47547}]
	--		 WHEN 106 THEN ISNULL(CAST(v_decimal AS NVARCHAR(MAX)), N'')
	--		 WHEN  61 THEN ISNULL(CONVERT(NVARCHAR(MAX), v_datetime, 126), N'')
	--		 WHEN 104 THEN ISNULL(CAST(v_bit AS NVARCHAR(MAX)), N'')
	--		 ELSE ISNULL(v_nvarchar,'') END
	--)
	-------------------------
	---- (-)CACHE(-)  -------
	-------------------------
	
	--, PRIMARY KEY(changecontextid, attrpathid, instanceid, contentid)
);

INSERT INTO @input_changes(changecontextid, actiontypeid, objecttype, instanceid, contentid, objectdefid
    , valuetypeid, v_int, v_nvarchar, v_decimal, v_datetime, v_bit
    , attrpathid, odedefid, attrpath_multiple, attr_defid, parent_defid, gparent_defid, ggparent_defid
    , ode_name
    , attr_name, parent_name, gparent_name, ggparent_name
    , actiontype_text
    , attrvalue_text
    )
    SELECT x.changecontextid, x.actiontypeid, x.objecttype, x.instanceid, x.contentid, x.objectdefid
    , x.valuetypeid, x.v_int, x.v_nvarchar, x.v_decimal, x.v_datetime, x.v_bit
    , apath.attrpathid, apath.odedefid, apath.attrpath_multiple, apath.attr_defid, apath.parent_defid, apath.gparent_defid, apath.ggparent_defid
    , odedef.name
    --, attr.name, parent.name, gparent.name, ggparent.name
    , att_names.attr_name, att_names.parent_name, att_names.gparent_name, att_names.ggparent_name

    , CASE x.actiontypeid WHEN 1 THEN N'New' WHEN 2 THEN N'Upd' WHEN 3 THEN N'Del' WHEN 0 THEN N'Non' ELSE NULL END AS actiontype_text

	-- (+) Compensate Removed trailing zeros : 1.0000 => 1. => 1.0
    , CASE WHEN x.valuetypeid = 106 AND SUBSTRING(txt_value.attrvalue_text, LEN(txt_value.attrvalue_text),1) = N'.' THEN CONCAT(txt_value.attrvalue_text, N'0') ELSE attrvalue_text END AS attrvalue_text_x
	-- (-) Compensate Removed trailing zeros
    
    FROM @object_changes AS x
    CROSS APPLY
    (
	   SELECT CASE x.valuetypeid  -- convert to xml text
			 WHEN  56 THEN ISNULL(CAST(x.v_int AS NVARCHAR(MAX)), N'') -- [null listitem:{TC_47547}]

			 WHEN 106 THEN ISNULL(
				REPLACE(RTRIM(REPLACE( -- (+) Remove trailing zeros: 1.0000 => 1. => Compensation needed!!!
				CAST(x.v_decimal AS NVARCHAR(MAX))
				, N'0',N' ')),N' ',N'0') -- (-) Remove trailing zeros
				, N'')
			 WHEN  61 THEN IIF(x.v_datetime IS NOT NULL, CONVERT(NVARCHAR(MAX), x.v_datetime, 126) + N'Z', N'')
			 WHEN 104 THEN ISNULL(CAST(x.v_bit AS NVARCHAR(MAX)), N'')
			 ELSE ISNULL(x.v_nvarchar,N'') END AS attrvalue_text
    ) AS txt_value
    CROSS APPLY
    (
	   /*SELECT attrpathid FROM dbo.hlsysobjectdefstgcolumn AS col WITH (NOLOCK) WHERE col.objectdefid = x.objectdefid 
		 AND col.tableid = OBJECT_ID(x.tablename) 
		 AND col.columnid = COLUMNPROPERTY(OBJECT_ID(x.tablename), x.columnname, 'columnid')
		 AND ISNULL(x.attrpathid,0)=0
	   UNION */
	   SELECT x.attrpathid WHERE NOT ISNULL(x.attrpathid,0)=0
    ) AS col
    INNER JOIN dbo.hlsysattrpathodedef AS apath WITH (NOLOCK) ON col.attrpathid = apath.attrpathid
    INNER JOIN dbo.hlsysodedef AS odedef WITH (NOLOCK) ON odedef.odedefid = apath.odedefid

    CROSS APPLY (
	   SELECT MAX(CASE WHEN ix = 1 THEN ad.name END) AS attr_name
		    ,MAX(CASE WHEN ix = 2 THEN ad.name END) AS parent_name
		    ,MAX(CASE WHEN ix = 3 THEN ad.name END) AS gparent_name
		    ,MAX(CASE WHEN ix = 4 THEN ad.name END) AS ggparent_name
		FROM (VALUES(1, apath.attr_defid), (2, apath.parent_defid), (3, apath.gparent_defid), (4, apath.ggparent_defid)) AS v(ix, attrdefid)
		LEFT OUTER JOIN dbo.hlsysattributedef AS ad ON ad.attrdefid = v.attrdefid
    ) AS att_names
--    INNER JOIN dbo.hlsysattributedef AS attr WITH (NOLOCK) ON attr.attrdefid = apath.attr_defid
--    LEFT OUTER JOIN dbo.hlsysattributedef AS parent WITH (NOLOCK) ON parent.attrdefid = apath.parent_defid
--    LEFT OUTER JOIN dbo.hlsysattributedef AS gparent WITH (NOLOCK) ON gparent.attrdefid = apath.gparent_defid
--    LEFT OUTER JOIN dbo.hlsysattributedef AS ggparent WITH (NOLOCK) ON ggparent.attrdefid = apath.ggparent_defid

--SELECT  * FROM @input_changes;

-- just minimize the record size
 --   WHERE  (apath.odedefid = 1 AND apath.attr_defid NOT IN (
	--15 -- 'HLOBJECTINFO.LASTMODIFIED'
	--)) OR (apath.odedefid > 1)
;
/*
DECLARE @tbl_attrname TABLE(attrdefid INT NOT NULL PRIMARY KEY, name NVARCHAR(200) NOT NULL);

--INSERT INTO @tbl_attrname(attrdefid, name)
SELECT ad.attrdefid, ad.name FROM dbo.hlsysattributedef ad WHERE ad.attrdefid IN (
SELECT v.attrdefid FROM @input_changes AS x CROSS APPLY (VALUES(x.attr_defid), (x.parent_defid), (x.gparent_defid), (x.ggparent_defid))v(attrdefid))
*/

-- DB column types : int:56, datetime:61, nvarchar:231, bit:61
-- Usecases: (note: in result.xml the cxa is threated as a compound attriute
-- * -- 1: ode.sca.cxa.pa (~~)
-- * -- 2: ode.sca.nca.cxa.pa (~~~)
-- * -- 3: ode.sca.nca.pa (done)
-- * -- 4: ode.sca.pa (done)
-- * -- 7: ode.sxa.pa (done)
-- * -- 5: ode.spa (done)
-- * -- 6: ode.mpa (~~~)
-- * -- 8: ode.mca.pa (done)
-- * -- 9: ode.mca.nca.pa (done)
-- * --10: ode.mca.nca.cxa.pa (~~~)
-- * --11: ode.mca.cxa.pa (~~~)

DECLARE @tbl_objecthistory TABLE(objecttype INT NOT NULL, historyid INT  NOT NULL, objectid INT  NOT NULL, objectdefid INT  NOT NULL, type TINYINT  NOT NULL, historyitem XML NOT NULL);

; WITH query_vis AS
(
   SELECT x.actiontypeid, x.actiontype_text
		, x.instanceid, x.contentid, x.odedefid, x.ode_name, x.issu, x.attrpath_multiple, x.attr_name, x.attrvalue_text
		, x.parent_defid, x.parent_name
		, x.gparent_defid, x.gparent_name
		, x.ggparent_name
   FROM @input_changes AS x
  
   WHERE (x.odedefid = 1 AND NOT (
	     (x.attr_defid = 11) -- 'HLOBJECTINFO.ID'
	  OR (x.attr_defid = 12) -- 'HLOBJECTINFO.DEFID'
	  OR (x.attr_defid = 15) -- 'HLOBJECTINFO.LASTMODIFIED'
	  OR (x.attr_defid = 28) -- 'HLOBJECTINFO.VERSION'
	)) 
	OR (x.odedefid > 1 AND x.attr_defid <> 192) -- 192:CONTENTID
)
, query_object_ode_attributes AS 
(
    SELECT x.actiontypeid, x.actiontype_text
		, x.instanceid, x.contentid
		, x.odedefid, x.ode_name, x.issu, x.attrpath_multiple, x.attr_name, x.attrvalue_text
		, x.parent_defid, x.parent_name
		, x.gparent_defid, x.gparent_name
		, x.ggparent_name
	  FROM query_vis AS x
)
, query_object_instance(actiontypeid, objecttype, objectdefid, objectid, objectversion) AS
(
    SELECT MAX(cat.actiontypeid) AS actiontypeid, MAX(cat.objecttype), MAX(cat.objectdefid), cat.instanceid AS objectid, MAX(cat.v_int) AS objectversion
	   FROM @input_changes AS cat
	   WHERE cat.changecontextid = @contextid AND cat.odedefid = 1 AND cat.attr_defid = 28 -- 'HLOBJECTINFO.VERSION'
	   GROUP BY cat.changecontextid, cat.instanceid
)

INSERT INTO @tbl_objecthistory(objecttype, objectid, objectdefid, historyid, type, historyitem)
SELECT chgobj.objecttype, chgobj.objectid, chgobj.objectdefid, chgobj.objectversion AS historyid, chgobj.actiontypeid AS historytype,
(SELECT chgobj.objectversion AS HistoryId
    , CASE chgobj.objecttype 
	   WHEN 2 THEN N'HLCASE' 
	   WHEN 3 THEN N'HLPERSON'
	   WHEN 4 THEN N'HLORGAUNIT'
	   WHEN 5 THEN N'HLPRODUCT'
	   WHEN 7 THEN N'HLCONTRACT'
	   ELSE CAST(0/CONCAT(N'Unknown base type:', chgobj.objecttype) AS NVARCHAR(10))
	   END AS ObjType
    , @lasttime AS LastModified
    , @agentid AS AgentId
    , chgobj.objectid AS ObjId
    , chgobj.objectdefid AS ObjDefId
    , 50003 AS [Version]
    , CASE chgobj.actiontypeid WHEN 1 THEN N'New' WHEN 2 THEN N'Upd' ELSE N'MxyzS' END AS N'ModifiedState'
    , (
		  SELECT chgode.ode_name AS Name
---(+) ODE content----------------------------------------------------
			 , (SELECT spa.attr_name AS Name
				    , spa.attrvalue_text AS [Value]
				FROM query_object_ode_attributes AS spa
				WHERE spa.instanceid = chgode.instanceid AND spa.odedefid = chgode.odedefid
				  AND spa.attrpath_multiple = 0 AND spa.parent_defid IS NULL
				ORDER BY spa.attr_name -- sort is needed only for simple asserts
				FOR XML PATH(N'Attribute')
				, TYPE) AS Attributes
---(+) SCA / CXA
				, (SELECT MAX(sca.parent_name) AS Name -- sca
					   , CASE MAX(sca.actiontypeid) WHEN 1 THEN N'New' ELSE N'Upd' END AS ModifiedState -- ['Upd':{TC_47325}]
				    , (SELECT pa_att.attr_name AS Name		    -- sca.pa
						  , pa_att.attrvalue_text AS [Value]
						  --, 'Bzzzz' AS 'ModifiedState'
					   FROM query_object_ode_attributes AS pa_att
					   WHERE sca.instanceid = pa_att.instanceid AND sca.odedefid = pa_att.odedefid AND ISNULL(sca.parent_defid,0) = ISNULL(pa_att.parent_defid, 0)
					   FOR XML PATH(N'Attribute')
					   , TYPE) AS N'Attributes'

				    , (SELECT MAX(sca_nca.gparent_name) AS N'Name'	    -- sca.nca
					   , MAX(sca.actiontype_text) AS N'ModifiedState'
					   , (SELECT pa_att.attr_name AS N'Name'		    -- sca.nca.pa
							 , pa_att.attrvalue_text AS N'Value'
							 , N'Wzzzz' AS ModifiedState
						  FROM query_object_ode_attributes AS pa_att
						  WHERE sca_nca.instanceid = pa_att.instanceid AND sca_nca.odedefid = pa_att.odedefid AND ISNULL(sca_nca.gparent_defid,0) = ISNULL(pa_att.gparent_defid,0)
						  FOR XML PATH(N'Attribute')
						  , TYPE) AS Attributes
					   , ( SELECT MAX(sca_nca.gparent_name) AS Name	    -- sca.nca.cxa
							 , MAX(sca.actiontype_text) AS ModifiedState
							 , (SELECT pa_att.attr_name AS Name		    -- sca.nca.cxa.pa
								    , pa_att.attrvalue_text AS [Value]
								    , N'Rzzzz' AS ModifiedState
								FROM query_object_ode_attributes AS pa_att
								WHERE sca_nca_cxa.instanceid = pa_att.instanceid AND sca_nca_cxa.odedefid = pa_att.odedefid AND ISNULL(sca_nca_cxa.gparent_defid,0) = ISNULL(pa_att.gparent_defid,0)
								FOR XML PATH(N'Attribute')
								, TYPE) AS Attributes
				    		  FROM query_object_ode_attributes AS sca_nca_cxa
						  WHERE sca_nca.instanceid = sca_nca_cxa.instanceid AND sca_nca_cxa.odedefid = sca_nca.odedefid
							 AND sca_nca_cxa.attrpath_multiple = 0 AND ISNULL(sca_nca_cxa.gparent_defid,0) = ISNULL(sca_nca.gparent_defid,0) AND sca_nca_cxa.gparent_defid IS NOT NULL
							 GROUP BY instanceid, odedefid, gparent_defid 
						  FOR XML PATH(N'CompoundZ'), TYPE
					   ) AS CompoundZ

				    	   FROM query_object_ode_attributes AS sca_nca
					   WHERE sca.instanceid = sca_nca.instanceid AND sca.odedefid = sca_nca.odedefid
						  AND sca_nca.attrpath_multiple = 0 AND ISNULL(sca_nca.parent_defid,0) = ISNULL(sca.parent_defid,0) AND sca_nca.gparent_defid IS NOT NULL
						  GROUP BY instanceid, odedefid, gparent_defid--, contentid
					   FOR XML PATH(N'CompoundY'), TYPE
				    ) AS CompoundY


				    FROM query_object_ode_attributes AS sca
				    WHERE sca.instanceid = chgode.instanceid AND sca.odedefid = chgode.odedefid
					   AND sca.attrpath_multiple = 0 AND sca.parent_defid IS NOT NULL
				    GROUP BY instanceid, odedefid, parent_defid
				    FOR XML PATH(N'Compound'), TYPE
				) AS Compounds

---(-) SCA / CXA
---(+) MCA ------
			 , (SELECT MAX(mca.parent_name) AS Name
			         , N'Non' AS ModifiedState --ISNULL(MAX(mca.actiontype_text), 'Non') AS 'ModifiedState' -- ['Non':{TC_45772}]
				    --, 'Qzzzz' AS 'ModifiedStateX'
				    , (
					   SELECT mci.contentid AS ContentId
					    , ISNULL(MAX(mci.actiontype_text),N'New') AS ModifiedState

					   , (SELECT pa_att.attr_name AS Name		    -- mca.pa
							 , pa_att.attrvalue_text AS [Value]
							 --, 'Kzzzz' AS 'ModifiedState'
						  FROM query_object_ode_attributes AS pa_att
						  WHERE mci.instanceid = pa_att.instanceid AND mci.odedefid = pa_att.odedefid AND ISNULL(mci.parent_defid,0) = ISNULL(pa_att.parent_defid,0)
						  AND mci.contentid = pa_att.contentid
						  ORDER BY pa_att.attr_name -- sort is needed only for simple asserts
						  FOR XML PATH(N'Attribute')
						  , TYPE) AS Attributes
					    , (SELECT MAX(mci_nca.gparent_name) AS Name
						    , MAX(mci_nca.actiontype_text) AS ModifiedState

							 , (SELECT pa_att.attr_name AS Name		    -- mca.nca.pa
								    , pa_att.attrvalue_text AS [Value]
								    , N'Tzzzz' AS ModifiedState
								FROM query_object_ode_attributes AS pa_att
								WHERE mci_nca.instanceid = pa_att.instanceid AND mci_nca.odedefid = pa_att.odedefid AND ISNULL(mci_nca.gparent_defid,0) = ISNULL(pa_att.gparent_defid,0)
								AND mci_nca.contentid = pa_att.contentid
								FOR XML PATH(N'Attribute')
								, TYPE) AS Attributes

						    , ( SELECT MAX(mci_nca_cxa.ggparent_name) AS Name -- mca.nca.cxa
						             , MAX(mci_nca_cxa.actiontype_text) AS ModifiedState
								, (SELECT pa_att.attr_name AS Name	 -- mca.nca.cxa.pa
									   , pa_att.attrvalue_text AS [Value]
									   , N'Xzzzz' AS ModifiedState
								    FROM query_object_ode_attributes AS pa_att
								    WHERE mci_nca_cxa.instanceid = pa_att.instanceid AND mci_nca_cxa.odedefid = pa_att.odedefid AND ISNULL(mci_nca_cxa.gparent_defid,0) = ISNULL(pa_att.gparent_defid,0)
								    AND mci_nca_cxa.contentid = pa_att.contentid
								    FOR XML PATH(N'Attribute')
								    , TYPE) AS Attributes
						      FROM query_object_ode_attributes AS mci_nca_cxa
							 WHERE mci_nca.instanceid = mci_nca_cxa.instanceid AND mci_nca.odedefid = mci_nca_cxa.odedefid
							  AND mci_nca_cxa.attrpath_multiple = 1 AND ISNULL(mci_nca_cxa.parent_defid,0) = ISNULL(mci_nca.parent_defid,0) AND mci_nca_cxa.gparent_defid IS NOT NULL
							  AND mci_nca_cxa.contentid = mci_nca.contentid
							  GROUP BY instanceid, odedefid, contentid, gparent_defid
							 FOR XML PATH(N'CompoundItem'), TYPE
						  ) AS CompoundItems
						  FROM query_object_ode_attributes AS mci_nca
						  WHERE mci_nca.instanceid = mci.instanceid AND mci_nca.odedefid = mci.odedefid
						   AND mci_nca.attrpath_multiple = 1 AND ISNULL(mci.parent_defid,0) = ISNULL(mci_nca.parent_defid,0) AND mci_nca.gparent_defid IS NOT NULL
						  GROUP BY instanceid, odedefid, parent_defid, contentid, mci_nca.gparent_defid
						  FOR XML PATH(N'CompoundItem'), TYPE
					   ) AS CompoundItems
					   FROM query_object_ode_attributes AS mci
					   WHERE mca.instanceid = mci.instanceid AND mca.odedefid = mci.odedefid
				         AND mci.attrpath_multiple = 1 AND ISNULL(mci.parent_defid,0) = ISNULL(mca.parent_defid,0)
					   GROUP BY instanceid, odedefid, parent_defid, mci.contentid
					   ORDER BY mci.contentid-- sort is needed only for simple asserts
					   FOR XML PATH(N'CompoundItem'), TYPE
				    ) AS CompoundItems

				FROM query_object_ode_attributes AS mca
				WHERE mca.instanceid = chgode.instanceid AND mca.odedefid = chgode.odedefid
				  AND mca.attrpath_multiple = 1 AND mca.parent_defid IS NOT NULL -- ode.mca
				  GROUP BY instanceid, mca.odedefid, mca.parent_defid
				ORDER BY MAX(mca.parent_name)-- sort is needed only for simple asserts
				FOR XML PATH(N'MultipleCompound')
				, TYPE) AS MultipleCompounds
---(-) MCA ------
---(+) MPA ------
	   , (SELECT MAX(mca.attr_name) AS Name -- ode.mpa
	   -- TODO > ValueItems > ValueItem > {Value, ContentId, ModifiedState}
		  FROM query_object_ode_attributes AS mca
		  WHERE mca.instanceid = chgode.instanceid AND mca.odedefid = chgode.odedefid
			 AND mca.attrpath_multiple = 1 AND mca.parent_defid IS NULL -- ode.mpa
			 GROUP BY instanceid, mca.odedefid--, mca.attr_defid
		  FOR XML PATH(N'MultipleAttribute')
		  , TYPE) AS MultipleAttributes

---(-) MPA ------
---(-) ODE content----------------------------------------------------
		  FROM (
			     SELECT odeatt.instanceid, odeatt.odedefid, IIF(odeatt.odedefid < 2000, odeatt.odedefid, 2000) AS ode_sort -- fix ODEs have higher sorted category
				, MAX(odeatt.ode_name) AS ode_name
				FROM query_object_ode_attributes AS odeatt
				WHERE issu = 0
				GROUP BY instanceid, odedefid
			 ) chgode
			 --INNER JOIN @ode_names AS odex ON odex.odedefid = chgode.odedefid
		WHERE chgode.instanceid = chgobj.objectid
		ORDER BY chgode.ode_sort, chgode.ode_name
		FOR XML PATH(N'ODE')
	 , type) AS ODEs
    FOR XML PATH(N'HistoryDataDto'), TYPE) AS historydata
FROM query_object_instance AS chgobj
;

IF (@doinsert = 1)
BEGIN

    IF (@dodelete = 1)
    BEGIN
		DELETE objhist FROM [dbo].[hlsyspersonhistory] AS objhist 
		WHERE EXISTS ( 
			SELECT 1 AS found FROM @input_changes AS cat 
			WHERE cat.changecontextid = @contextid AND cat.instanceid = objhist.personid AND ISNULL(cat.v_int, 0) = objhist.historyid AND cat.objecttype = 3
		);

		DELETE objhist FROM [dbo].[hlsysorgunithistory] AS objhist 
		WHERE EXISTS ( 
			SELECT 1 AS found FROM @input_changes AS cat 
			WHERE cat.changecontextid = @contextid AND cat.instanceid = objhist.orgunitid AND ISNULL(cat.v_int, 0) = objhist.historyid AND cat.objecttype = 4
		);

		DELETE objhist FROM [dbo].[hlsysproducthistory] AS objhist 
		WHERE EXISTS ( 
			SELECT 1 AS found FROM @input_changes AS cat 
			WHERE cat.changecontextid = @contextid AND cat.instanceid = objhist.productid AND ISNULL(cat.v_int, 0) = objhist.historyid AND cat.objecttype = 5
		);
	END

	INSERT INTO [dbo].[hlsyspersonhistory] (historyid, personid, persondefid, creationtime, agentid, type, historyitem, historyitemsize, actioncontextid)
		SELECT historyid, objectid, objectdefid, GETUTCDATE(), @agentid, type, CAST(historyitem AS NVARCHAR(MAX)), LEN(CAST(historyitem AS NVARCHAR(MAX))), @actioncontextid 
			FROM @tbl_objecthistory WHERE objecttype = 3;
	INSERT INTO [dbo].[hlsysorgunithistory] (historyid, orgunitid, orgunitdefid, creationtime, agentid, type, historyitem, historyitemsize, actioncontextid)
		SELECT historyid, objectid, objectdefid, GETUTCDATE(), @agentid, type, CAST(historyitem AS NVARCHAR(MAX)), LEN(CAST(historyitem AS NVARCHAR(MAX))), @actioncontextid 
			FROM @tbl_objecthistory WHERE objecttype = 4;
	INSERT INTO [dbo].[hlsysproducthistory] (historyid, productid, productdefid, creationtime, agentid, type, historyitem, historyitemsize, actioncontextid)
		SELECT historyid, objectid, objectdefid, GETUTCDATE(), @agentid, type, CAST(historyitem AS NVARCHAR(MAX)), LEN(CAST(historyitem AS NVARCHAR(MAX))), @actioncontextid 
			FROM @tbl_objecthistory WHERE objecttype = 5;

END
ELSE
BEGIN
    SELECT objecttype, objectid, objectdefid, historyid, type, historyitem FROM @tbl_objecthistory;
END



END
