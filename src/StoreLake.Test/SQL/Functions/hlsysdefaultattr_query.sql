--DROP FUNCTION [dbo].[hlsysdefaultattr_query]
CREATE FUNCTION [dbo].[hlsysdefaultattr_query](@objectdefid INT, @objectid INT, @lcid INT)
RETURNS TABLE AS RETURN
			SELECT dv.[defaultattrpathid]
					, dv.[casedefid]
					, dv.[caseid]
					, dv.[defaultvalue]
			FROM [dbo].[hlsysobjectdef] AS od
			CROSS APPLY [dbo].[hlsysdefaultattr_query_case] (@objectdefid, @objectid, @lcid) AS dv
			WHERE od.[objectdefid] = @objectdefid AND od.objecttype=2 -- 2:case

	UNION ALL

			SELECT [defaultattrpathid]
				 , [persondefid]
				 , [personid]
				 , [defaultvalue]
			FROM [dbo].[hlsysobjectdef] AS od
			CROSS APPLY [dbo].[hlsysdefaultattr_query_person] (@objectdefid, @objectid, @lcid) AS dv
			WHERE od.[objectdefid] = @objectdefid AND od.objecttype=3 -- 3:person

	UNION ALL

			SELECT [defaultattrpathid]
				 , [orgunitdefid]
				 , [orgunitid]
				 , [defaultvalue]
			FROM [dbo].[hlsysobjectdef] AS od
			CROSS APPLY [dbo].[hlsysdefaultattr_query_orgunit] (@objectdefid, @objectid, @lcid) AS dv
			WHERE od.[objectdefid] = @objectdefid AND od.objecttype=4 -- 4:orgunit

	UNION ALL

			SELECT [defaultattrpathid]
				 , [productdefid]
				 , [productid]
				 , [defaultvalue]
			FROM [dbo].[hlsysobjectdef] AS od
			CROSS APPLY [dbo].[hlsysdefaultattr_query_product] (@objectdefid, @objectid, @lcid) AS dv
			WHERE od.[objectdefid] = @objectdefid AND od.objecttype=5 -- 5:product

	UNION ALL

			SELECT [defaultattrpathid]
				 , [contractdefid]
				 , [contractid]
				 , [defaultvalue]
			FROM [dbo].[hlsysobjectdef] AS od
			CROSS APPLY [dbo].[hlsysdefaultattr_query_contract] (@objectdefid, @objectid, @lcid)  AS dv
			WHERE od.[objectdefid] = @objectdefid AND od.objecttype=7 -- 7:contract
	