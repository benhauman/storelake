CREATE FUNCTION [dbo].[hlsur_query_surveyresults]
(
    @customerid INT,
    @typeid INT,
    @surveydefid INT,
    @startdate DATETIME,
    @enddate DATETIME
)
RETURNS TABLE
RETURN (
    WITH [CTE] ([currentlevel], [surveyid], [surveydefid], [recipientid], [recipientdefid], [customerid], [customerdefid], [isresult])
    AS
    (
	    SELECT 1 AS [currentlevel]
		     , [s].[surveyid]
		     , [s].[surveydefid]
	         , [s].[recipientid]
		     , [s].[recipientdefid]
		     , [s].[recipientid] AS [customerid]
		     , [s].[recipientdefid] AS [customerdefid]
		     , CASE WHEN @customerid IS NULL THEN 1 ELSE 0 END AS [isresult]
	    FROM [dbo].[hlsursurvey] AS [s]
	    INNER JOIN [dbo].[hlsurresponse] AS [r] ON [s].[surveyid] = [r].[surveyid]
	    INNER JOIN [dbo].[hlsurdefinition] AS [d] ON [s].[surveydefid] = [d].[surveydefid] AND (@typeid IS NULL OR [d].[typeid] = @typeid)
	    WHERE (@startdate IS NULL OR [r].[received] >= @startdate)
          AND (@enddate IS NULL OR [r].[received] <= @enddate)
          AND (@surveydefid IS NULL OR [s].[surveydefid] = @surveydefid)
	    UNION ALL
	    SELECT [c].[currentlevel] + 1
	         , [c].[surveyid]
		     , [c].[surveydefid]
		     , [c].[recipientid]
		     , [c].[recipientdefid]
		     , [a].[objectida] AS [customerid]
		     , [a].[objectdefida] AS [customerdefid]
		     , CASE WHEN @customerid = [a].[objectida] THEN 1 ELSE 0 END AS [isresult]
	    FROM [CTE] AS [c]
	    INNER JOIN [dbo].[hlsysassociation] AS [a] ON [a].[objectidb] = [c].[customerid] AND [a].[objectdefidb] = [c].[customerdefid]
	    INNER JOIN [dbo].[hlsysassociationdef] AS [ad] ON [a].[associationdefid] = [ad].[associationdefid]
	    INNER JOIN [dbo].[hlsurstrucref] AS [ref] ON [ad].[name] = [ref].[name]
	    WHERE [c].[isresult] = 0
	      AND [c].[currentlevel] < 10
    )
    SELECT [c].[surveyid]
         , [c].[surveydefid]
    FROM [CTE] AS [c]
    WHERE [c].[isresult] = 1
    GROUP BY [c].[surveyid], [c].[surveydefid]
)