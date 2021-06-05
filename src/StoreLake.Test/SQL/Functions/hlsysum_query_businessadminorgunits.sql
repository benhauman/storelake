/* -- HOW TO TEST
DECLARE @filter_text NVARCHAR(1000)

DECLARE @volume INT = 100
DECLARE @agentid INT = 710

SET @filter_text = '"Find me"' -- <- your filter goes here

SELECT * FROM hlsysum_query_businessadminorgunits(@volume, @agentid, @filter_text)
*/
CREATE FUNCTION [dbo].[hlsysum_query_businessadminorgunits]
(
	  @volume INT
	, @agentid INT
	, @filter_text NVARCHAR(1000)
)
RETURNS TABLE AS RETURN
(
	WITH query_bottom_up AS 
	(
		-- anchor
		SELECT org.orgunitid, org.orgunitdefid, 1 AS _fnd, 1 AS _cnt, 0 AS _last, 0 AS _lastdef, 0 AS _scnd_last, 0 AS _third_last
		   FROM dbo.hlsysorganizationsearchdata AS org
		   INNER JOIN dbo.hlsysadmingraphnodea AS agna
			  ON org.orgunitdefid = agna.objectdefid AND CONTAINS(org.name, @filter_text)
   
		UNION ALL
    
		-- recursive subquery
		SELECT ai.objectida, ai.objectdefida, 0, bu._cnt + 1, bu.orgunitid, bu.orgunitdefid, bu._last, bu._scnd_last
		   FROM query_bottom_up AS bu 
		   INNER JOIN dbo.hlsysadmingraphedge AS age
			  ON bu.orgunitdefid = age.objectdefidb
		   INNER JOIN dbo.hlsysassociation AS ai
			  ON ai.objectidb = bu.orgunitid AND ai.objectdefidb = bu.orgunitdefid AND ai.objectdefida = age.objectdefida AND ai.associationdefid = age.associationdefid
		   WHERE bu._cnt < 10 AND ai.objectida <> bu._last AND ai.objectida <> bu._scnd_last AND ai.objectida <> bu._third_last
	)
	, query_top_down AS
	(
		-- anchor
		SELECT TOP(@volume) bu.orgunitid, bu.orgunitdefid, bu._fnd, 0 AS __cnt, bu._last, bu._lastdef, 0 AS __last, 0 AS __lastdef, 0 AS __scnd_last, 0 AS __third_last
		   FROM query_bottom_up AS bu
		   INNER JOIN [dbo].[hlsysadminorgunitagent] AS aoua
			  ON bu.orgunitid = aoua.orgunitid AND bu.orgunitdefid = aoua.orgunitdefid AND aoua.agentid = @agentid
			CROSS APPLY ( 
				SELECT 1 AS canread 
					FROM dbo.hlsyssec_query_agentobjectprmread(@agentid, bu.orgunitid, bu.orgunitdefid, 787) AS secrlst 
					WHERE secrlst.canread = 1 
			) AS sec

		UNION ALL

		-- recursive subquery
		SELECT bu.orgunitid, bu.orgunitdefid, bu._fnd, __cnt + 1, bu._last, bu._lastdef, td.orgunitid, td.orgunitdefid, td.__last, td.__scnd_last
			FROM query_bottom_up AS bu
			INNER JOIN query_top_down AS td
				ON bu.orgunitid = td._last AND bu.orgunitdefid = td._lastdef
			CROSS APPLY ( 
				SELECT 1 AS canread 
					FROM dbo.hlsyssec_query_agentobjectprmread(@agentid, bu.orgunitid, bu.orgunitdefid, 787) AS secrlst 
					WHERE secrlst.canread = 1 
			) AS sec
			WHERE td.__cnt < 10 AND bu.orgunitid <> td.__last AND bu.orgunitid <> td.__scnd_last AND bu.orgunitid <> td.__third_last
	)

	SELECT td.orgunitid, td.orgunitdefid, org.name, td.__last AS parentorgunitid, td.__lastdef AS parentorgunitdefid, CAST(MAX(td._fnd) AS BIT) AS found
		FROM query_top_down AS td
		INNER JOIN dbo.hlsysorganizationsearchdata AS org
			ON td.orgunitid = org.orgunitid AND td.orgunitdefid = org.orgunitdefid
		GROUP BY td.orgunitid, td.orgunitdefid, org.name, td.__last, td.__lastdef
)