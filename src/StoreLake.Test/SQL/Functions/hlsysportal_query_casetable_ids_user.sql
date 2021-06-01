CREATE FUNCTION [dbo].[hlsysportal_query_casetable_ids_user]
(
	@agentid INT
  , @personid INT
  , @persondefid INT
  , @groupsolvedwithactive BIT
  , @countertype TINYINT -- 1:running or 2:completed
  , @take INT
  , @associationdefid INT -- default:130:'Customer2Case'
  ----------------
  , @sectionid_casehid TINYINT
  , @filter_casedefid INT
  , @filter_text NVARCHAR(1000) -- not null or empty!!!
)
RETURNS TABLE AS RETURN
(
	SELECT TOP(@take) ai.objectida AS caseid, ai.objectdefida AS casedefid, casevw.internalstate
	   FROM [dbo].[hlsysassociation] AS ai WITH (NOLOCK)
	   CROSS APPLY (SELECT 1 AS allowedreadandsearch FROM [dbo].[hlsyssec_query_agentcaseprmreadsearch](@agentid, ai.objectida, ai.objectdefida) AS sec WHERE sec.canreadandsearch = 1) AS secflt
	   INNER JOIN [dbo].[hlsyscasevw] AS casevw ON casevw.caseid = ai.objectida AND casevw.casedefid = ai.objectdefida
		  AND (
			(@countertype  = 1 AND (casevw.internalstate=180 OR casevw.internalstate=182 OR casevw.internalstate=184 OR (casevw.internalstate = 183 AND @groupsolvedwithactive=1)))
			OR 
			(@countertype <> 1 AND (casevw.internalstate= 181 OR (casevw.internalstate = 183 AND @groupsolvedwithactive<>1)))
			)
	   LEFT OUTER JOIN [dbo].[hlsysportalcfgcasehiddenstate] AS hiddenstate WITH (NOLOCK) ON hiddenstate.sectionid = @sectionid_casehid AND hiddenstate.casedefid = casevw.casedefid AND hiddenstate.internalstate = casevw.internalstate
	-- FILTER
	   OUTER APPLY 
	   (
		  SELECT ctb.[KEY] AS caseid, ctb.[RANK] AS rnk  
		    FROM CONTAINSTABLE([dbo].[hlsysobjectdata], [objectdata], @filter_text) AS ctb -- If the language is needed then retrieve its [alias] from [sys].[syslanguages] where [msglangid] = @lcid!!
		   WHERE ctb.[KEY] = casevw.caseid AND @filter_text <> N'""'
	   ) AS csd

	   WHERE ai.associationdefid = @associationdefid
	   AND ai.objectdefidb = @persondefid
	   AND ai.objectidb = @personid
	   AND hiddenstate.sectionid IS NULL
	   AND ( csd.caseid IS NOT NULL OR @filter_text = N'""') -- filter
	   AND (@filter_casedefid IS NULL OR @filter_casedefid=ai.objectdefida) -- filter
	   ORDER BY csd.rnk -- NULL is ok!

    
)
