CREATE FUNCTION [dbo].[hlsysum_querybusinessadminorgunitchildren]
(
	@agentid INT,
	@orgunitid INT,
	@orgunitdefid INT
)
RETURNS TABLE AS RETURN
(
	SELECT ou.orgunitid, ou.orgunitdefid, ou.name AS orgunitname, CONVERT(INT, ISNULL(ouchild.childcnt,0)) AS childcnt, CONVERT(INT,ISNULL(qac.admincnt,0)) AS admincnt
	FROM
	(
		SELECT ai.objectdefidb AS orgunitdefid, ai.objectidb AS orgunitid, qhc.childcnt
			FROM [dbo].[hlsysassociation] AS ai
			INNER JOIN [dbo].[hlsysadmingraphedge] AS dg ON ai.associationdefid = dg.associationdefid AND ai.objectdefida = dg.objectdefida AND ai.objectdefidb = dg.objectdefidb
			INNER JOIN [dbo].[hlsysadmingraphnodea] AS agna ON ai.objectdefidb = agna.objectdefid -- filter organization parents only(for person remove it)
			CROSS APPLY (SELECT 1 AS canread FROM [dbo].[hlsyssec_query_agentobjectprmread](@agentid, ai.objectidb, ai.objectdefidb, 787) AS secrlst WHERE secrlst.canread = 1) AS sec
			-- query haschildren
		    CROSS APPLY ( --SELECT CONVERT(BIT, IIF(EXISTS( 
				SELECT COUNT(*) AS childcnt
				   FROM [dbo].[hlsysassociation] AS aihc
				   INNER JOIN [dbo].[hlsysadmingraphedge] AS dghc ON aihc.associationdefid = dghc.associationdefid AND aihc.objectdefida = dghc.objectdefida AND aihc.objectdefidb = dghc.objectdefidb
				   INNER JOIN [dbo].[hlsysadmingraphnodea] AS agnahc ON aihc.objectdefidb = agnahc.objectdefid -- filter organization parents only(for person remove it)
				   WHERE aihc.objectida = ai.objectidb AND aihc.objectdefida = ai.objectdefidb
			   --),1, 0)) AS haschildren
		    ) qhc
			WHERE ai.objectida = @orgunitid AND ai.objectdefida = @orgunitdefid
	) AS ouchild
	INNER JOIN [dbo].[hlsysorgunitnamevw] AS ou ON ou.orgunitdefid = ouchild.orgunitdefid AND ou.orgunitid = ouchild.orgunitid AND ou.isdefault = 1
	OUTER APPLY (SELECT COUNT(*) AS admincnt FROM [dbo].[hlsysadminorgunitagent] AS ouac WHERE ouac.orgunitid = ouchild.orgunitid AND ouac.orgunitdefid = ouchild.orgunitdefid ) AS qac -- admincount
	
)
