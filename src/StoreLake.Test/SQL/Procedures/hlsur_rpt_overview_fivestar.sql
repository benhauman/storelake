CREATE PROCEDURE [dbo].[hlsur_rpt_overview_fivestar]
    @customerid INT,
    @surveydefid INT,
    @startdate DATETIME,
    @enddate DATETIME
AS
BEGIN
    SET NOCOUNT ON

    SELECT [d].[name]
         , AVG(CAST([r].[result] AS DECIMAL(5, 2))) AS [avg]
         , SUM(CASE WHEN [r].[result] = 1 THEN 1 ELSE 0 END) AS [one]
         , SUM(CASE WHEN [r].[result] = 2 THEN 1 ELSE 0 END) AS [two]
         , SUM(CASE WHEN [r].[result] = 3 THEN 1 ELSE 0 END) AS [three]
         , SUM(CASE WHEN [r].[result] = 4 THEN 1 ELSE 0 END) AS [four]
         , SUM(CASE WHEN [r].[result] = 5 THEN 1 ELSE 0 END) AS [five]
    FROM [dbo].[hlsur_query_surveyresults] (@customerid, 2, @surveydefid, @startdate, @enddate) AS [s]
    INNER JOIN [dbo].[hlsurdefinition] AS [d] ON [d].[surveydefid] = [s].[surveydefid]
    INNER JOIN [dbo].[hlsurresponse] AS [r] ON [r].[surveyid] = [s].[surveyid]
    GROUP BY [d].[name]

END
