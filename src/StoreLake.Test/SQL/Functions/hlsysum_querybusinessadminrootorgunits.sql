CREATE FUNCTION [dbo].[hlsysum_querybusinessadminrootorgunits]
(
	@agentid INT
)
RETURNS TABLE AS RETURN
(
    SELECT ou.orgunitid, ou.orgunitdefid, CONVERT(NVARCHAR(255), ou.name) AS orgunitname, CONVERT(INT, ISNULL(qhc.childcnt, 0)) AS childcnt, CONVERT(INT, ISNULL(qac.admincnt, 0)) AS admincnt
      FROM [dbo].[hlsysadminorgunitagent] AS oua
     CROSS APPLY ( SELECT 1 AS canread FROM [dbo].[hlsyssec_query_agentobjectprmread](@agentid, oua.orgunitid, oua.orgunitdefid, 787) AS secrlst WHERE secrlst.canread = 1 ) AS sec
     INNER JOIN [dbo].[hlsysorgunitnamevw] AS ou ON oua.orgunitid = ou.orgunitid AND oua.orgunitdefid = ou.orgunitdefid AND ou.isdefault = 1
	 CROSS APPLY (SELECT COUNT(*) AS admincnt FROM [dbo].[hlsysadminorgunitagent] AS ouac WHERE ouac.orgunitid = oua.orgunitid AND ouac.orgunitdefid = oua.orgunitdefid ) AS qac -- admincount
	 -- query haschildren
	 CROSS APPLY ( --SELECT IIF(EXISTS( 
		  SELECT COUNT(*) AS childcnt
			FROM [dbo].[hlsysassociation] AS aihc
			INNER JOIN [dbo].[hlsysadmingraphedge] AS dghc ON aihc.associationdefid = dghc.associationdefid AND aihc.objectdefida = dghc.objectdefida AND aihc.objectdefidb = dghc.objectdefidb
			INNER JOIN [dbo].[hlsysadmingraphnodea] AS agnahc ON aihc.objectdefidb = agnahc.objectdefid -- filter organization parents only(for person remove it)
			WHERE aihc.objectida = oua.orgunitid AND aihc.objectdefida = oua.orgunitdefid
		--),1, 0) AS haschildren
	 ) qhc
     WHERE oua.agentid = @agentid
)