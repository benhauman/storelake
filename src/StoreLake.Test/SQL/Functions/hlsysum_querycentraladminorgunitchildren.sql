CREATE FUNCTION [dbo].[hlsysum_querycentraladminorgunitchildren]
(
	@agentid INT,
	@orgunitid INT,
	@orgunitdefid INT
)
RETURNS TABLE AS RETURN
(
SELECT ou.orgunitid, ou.orgunitdefid, CONVERT(NVARCHAR(255), ou.name) AS orgunitname, CONVERT(INT, ISNULL(ouchild.childcnt, 0)) AS childcnt, CONVERT(INT, ISNULL(ouchild.admincnt, 0)) AS admincnt
FROM 
(
    SELECT DISTINCT ai.objectidb AS orgunitid, ai.objectdefidb AS orgunitdefid, qhc.childcnt, qac.admincnt
	 FROM [dbo].[hlsysadmingraphnodea] AS agna
	 INNER JOIN [dbo].[hlsysadmingraphedge] AS ae ON ae.objectdefida = @orgunitdefid AND ae.objectdefidb = agna.objectdefid -- only child organizations
	 INNER JOIN [dbo].[hlsysassociation] AS ai ON ai.associationdefid =ae.associationdefid AND ai.objectdefida = ae.objectdefida AND ai.objectdefidb = ae.objectdefidb
	   AND ai.objectdefida = @orgunitdefid AND ai.objectida = @orgunitid
	  -- security
	 CROSS APPLY [dbo].[hlsyssec_query_agentobjectprmread](@agentid, ai.objectidb, ai.objectdefidb, 787) AS secrlst
	 -- admincnt
 	 CROSS APPLY (SELECT COUNT(*) AS admincnt FROM [dbo].[hlsysadminorgunitagent] AS ouac WHERE ouac.orgunitid = ai.objectidb AND ouac.orgunitdefid = ai.objectdefidb ) AS qac -- admincount
	 -- haschildren
	 CROSS APPLY (
		--SELECT IIF(EXISTS( 
			SELECT COUNT(*) AS childcnt
		      FROM [dbo].[hlsysadmingraphnodea] AS agnahc
			 INNER JOIN [dbo].[hlsysadmingraphedge] AS aehc ON aehc.objectdefida = ai.objectdefidb AND aehc.objectdefidb = agnahc.objectdefid -- only child of child organizations (B of B)
			 INNER JOIN [dbo].[hlsysassociation] AS aihc ON aihc.associationdefid =aehc.associationdefid AND aihc.objectdefida = aehc.objectdefida AND aihc.objectdefidb = aehc.objectdefidb
			 WHERE aehc.objectdefida = ai.objectdefidb  -- OU (child) is parent def in edge
			  AND aihc.objectdefida = ai.objectdefidb AND aihc.objectida = ai.objectidb -- OU is A in assocs
			--),1, 0) AS haschildren
	 ) AS qhc
) AS ouchild
INNER JOIN [dbo].[hlsysorgunitnamevw] AS ou ON ou.orgunitdefid = ouchild.orgunitdefid AND ou.orgunitid = ouchild.orgunitid AND ou.isdefault = 1
)
