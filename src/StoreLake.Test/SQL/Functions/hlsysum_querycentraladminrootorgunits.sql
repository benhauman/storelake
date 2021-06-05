CREATE FUNCTION [dbo].[hlsysum_querycentraladminrootorgunits]
(
	@agentid INT
)
RETURNS TABLE AS RETURN
(
SELECT ou.orgunitid, ou.orgunitdefid, CONVERT(NVARCHAR(255), ou.name) AS orgunitname, CONVERT(INT, 1) AS childcnt, qac.admincnt
  FROM [dbo].[hlsysadmingraphnodea] AS agna
  INNER JOIN [dbo].[hlsysorgunitnamevw] AS ou ON ou.orgunitdefid = agna.objectdefid AND ou.isdefault = 1
  CROSS APPLY (SELECT 1 AS cr FROM [dbo].[hlsyssec_query_agentobjectprmread](@agentid, ou.orgunitid, ou.orgunitdefid, 787) AS secrlst WHERE secrlst.canread = 1) AS sec
  OUTER APPLY (SELECT COUNT(*) AS admincnt FROM [dbo].[hlsysadminorgunitagent] AS oua WHERE oua.orgunitid = ou.orgunitid AND oua.orgunitdefid = ou.orgunitdefid ) AS qac -- admincount
  WHERE NOT EXISTS(
    SELECT *
      FROM [dbo].[hlsysadmingraphedge] AS ae 
	 INNER JOIN [dbo].[hlsysassociation] AS ai ON ai.associationdefid =ae.associationdefid AND ai.objectdefida = ae.objectdefida AND ai.objectdefidb = ae.objectdefidb
	 --????  AND ae.objectdefida <> ae.objectdefidb -- no recursive edges ????
     WHERE ae.objectdefidb = ou.orgunitdefid -- OU is child
	  AND ai.objectdefidb = ou.orgunitdefid AND ai.objectidb = ou.orgunitid
  )
)
