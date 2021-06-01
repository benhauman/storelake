-- DROP PROCEDURE [dbo].[hlsysportal_query_casetable_data_user];
CREATE PROCEDURE [dbo].[hlsysportal_query_casetable_data_user]
(
	@portalid TINYINT
  , @agentid INT
  , @personid INT
  , @persondefid INT
  , @lcid INT
  , @countertype TINYINT
  , @takeall BIT = NULL
  , @filter_casedefid INT = NULL
  , @filter_value NVARCHAR(1000) = NULL		 -- Required, no Null or empty full-text predicate. Empty = ""
)
-- @countertype={1:InProgress, 2:Closed, 3:All}
-- RESULT(caseid, casedefid)
AS
BEGIN
    SET NOCOUNT ON;
    -- SET DEADLOCK_PRIORITY LOW;

	DECLARE @take INT = IIF(ISNULL(@takeall, 0)=0, 20, 10000);

	DECLARE @filter_text NVARCHAR(1000) = IIF(@filter_value IS NULL OR LEN(@filter_value)=0, N'""', @filter_value);

    DECLARE @sectionid_casegen TINYINT;
	DECLARE @sectionid_casehid TINYINT;
	DECLARE @sectionid_casetable TINYINT;
    DECLARE @sectionid_personcase TINYINT;

	SELECT @sectionid_casegen = IIF(sct.sectionname = N'MyCasesGeneral', sct.sectionid, @sectionid_casegen)
		 , @sectionid_casehid = IIF(sct.sectionname = N'MyCasesHidden', sct.sectionid, @sectionid_casehid)
		 , @sectionid_casetable = IIF(sct.sectionname = N'MyCasesTable', sct.sectionid, @sectionid_casetable)
		 , @sectionid_personcase = IIF(sct.sectionname = N'SearchPersonCase', sct.sectionid, @sectionid_personcase)
		  FROM [dbo].[hlsysportalcfgmap] AS cfgmap WITH (NOLOCK) 
		  INNER JOIN [dbo].[hlsysportalcfgsection] AS sct WITH (NOLOCK) ON sct.sectionid = cfgmap.sectionid AND 
		  (
			    sct.sectionname = N'MyCasesGeneral' 
			 OR sct.sectionname = N'MyCasesHidden'
			 OR sct.sectionname = N'MyCasesTable'
			 OR sct.sectionname = N'SearchPersonCase'
		  )
		  WHERE cfgmap.portalid = @portalid
		  ;

    PRINT CONCAT(N'@sectionid_casetable:', @sectionid_casetable, N', @sectionid_personcase:', @sectionid_personcase);

	DECLARE @assocdefid_person2case INT = ISNULL((SELECT TOP 1 associationdefid FROM [dbo].[hlsysportalcfgsearchpersoncase] WITH (NOLOCK) WHERE sectionid = @sectionid_personcase), 130);

	DECLARE @groupsolvedwithactive BIT = ISNULL((SELECT [sec_case].[groupsolvedwithactive] FROM [dbo].[hlsysportalcfgcase] AS sec_case WITH (NOLOCK) WHERE sec_case.sectionid = @sectionid_casegen), 0);

    
    SELECT cases.caseid, cases.casedefid, cases.internalstate, caseimage.imageindex AS caseimageid
			, cfld.field_position, cfld.field_attrpathid --, apath.attrpath_text AS field_attrpath_text
			, cfld.field_display_kind, cfld.field_display_type, cfld.field_value_type
			, cfld.value_nvarchar
			, cfld.value_int
			, cfld.value_bit
			, cfld.value_datetime 
			, cfld.value_decimal

			-- [field_display_type] >> 1:SmallText | 2:LargeText | 3:Icon
			-- [field_display_kind] >> 0:Default | 1:ListItem | 2:TreeItem | 3:Agent | 4:ObjectDefinition
			, CAST(IIF(cfld.field_display_kind = 0, 0, 1) AS BIT) AS field_has_displayvalue
			, CAST((
				CASE WHEN cfld.field_display_kind = 1 THEN dn_li.displayname -- 1:ListItem
				     WHEN cfld.field_display_kind = 2 THEN dn_ti.displayname -- 2:TreeItem
					WHEN cfld.field_display_kind = 3 THEN dn_ag.fullname    -- 3:Agent
					WHEN cfld.field_display_kind = 4 THEN dn_od.displayname    -- 3:ObjectDefinition
				    ELSE NULL END
				  ) AS NVARCHAR(400)) AS displayvalue

			 , CAST(
					(CASE WHEN cfld.field_display_type = 3  AND cfld.field_display_kind = 1 THEN IIF(listimage.imageindex IS NULL, -1,listimage.imageindex) 				
					ELSE NULL END) AS INT) AS listitemimageid
	FROM [dbo].[hlsysportal_query_casetable_ids_user](@agentid,@personid,@persondefid,@groupsolvedwithactive,@countertype,@take,@assocdefid_person2case,@sectionid_casehid, @filter_casedefid, @filter_text) AS cases
	OUTER APPLY (
		SELECT [i].[imageindex]
		FROM [dbo].[hlsysobjectdefimage] AS [odi] WITH (NOLOCK)
		INNER JOIN [dbo].[hlsysimage] AS [i] WITH (NOLOCK) ON [odi].[imageid] = [i].[imageid] AND [odi].[objectdefid] = [cases].[casedefid]
	) AS [caseimage]
    OUTER APPLY [dbo].[hlsysportal_query_casetable_fieldvalue](@sectionid_casetable, cases.caseid, cases.casedefid, @lcid) AS cfld
    --LEFT OUTER JOIN [dbo].[hlsysattrpathodedef] AS apath WITH (NOLOCK) ON apath.attrpathid = cfld.field_attrpathid
    LEFT OUTER JOIN [dbo].[hlsysdisplayname] AS [dn_li] WITH (NOLOCK) ON [cfld].[field_display_kind] = 1 AND cfld.value_int > 0 AND cfld.value_int = dn_li.reposid AND [dn_li].[languageid] = @lcid
    LEFT OUTER JOIN [dbo].[hlsysdisplayname] AS [dn_ti] WITH (NOLOCK) ON [cfld].[field_display_kind] = 2 AND cfld.value_int > 0 AND cfld.value_int = dn_ti.reposid AND [dn_ti].[languageid] = @lcid
	LEFT OUTER JOIN [dbo].[hlsysdisplayname] AS [dn_od] WITH (NOLOCK) ON [cfld].[field_display_kind] = 4 AND cfld.value_int > 0 AND cfld.value_int = dn_od.reposid AND [dn_od].[languageid] = @lcid
    LEFT OUTER JOIN [dbo].[hlsysagent] AS [dn_ag] WITH (NOLOCK) ON [cfld].[field_display_kind] = 3 AND cfld.value_int > 0 AND cfld.value_int = dn_ag.agentid
	OUTER APPLY (
		-- Unfortunately portal requires the list item id as image index for CASEINFO.INTERNALSTATE,
		-- even though it is not contained in the image list.
		SELECT IIF([cfld].[field_attrpathid] <> 68, [i].[imageindex], [lii].[listitemid]) AS [imageindex]
		FROM [dbo].[hlsyslistitemimage] AS [lii] WITH (NOLOCK)
		INNER JOIN [dbo].[hlsysimage] AS [i] WITH (NOLOCK) ON [lii].[imageid] = [i].[imageid]
									                      AND [lii].[listitemid] = [cfld].[value_int]
									                      AND [cfld].[field_value_type] = 56
									                      AND [cfld].[field_display_type] = 3 -- -- icon:1 AND int:56 AND ???
	) AS [listimage]
	ORDER BY cases.caseid DESC
END

/*
EXEC [dbo].[hlsysportal_query_casetable_data_user]
@portalid = 101
  , @agentid = 710
  , @personid = 667709-- (SELECT objectid FROM dbo.hlsysagenttoobject WHERE agentid = 710)
  , @persondefid = 100307 -- (SELECT objectdefid FROM dbo.hlsysagenttoobject WHERE agentid = 710)
  , @lcid = 1031
  , @countertype = 2
  --, @filter_value = NULL
  , @filter_value = '"Promotion *"' -- Handheld Liquidation - Online Handheld Promotion 

*/