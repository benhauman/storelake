-- @Name GetHistoryDetails
-- @Return History.HistoryDetailEntity
CREATE PROCEDURE [dbo].[hlsys_query_historydetails]
	@objectid INT,
	@objectdefid INT, 
	@lcid INT, 
	@agentid INT, 
	@assochistory BIT, 
	@objecthistory BIT
AS
BEGIN
    SET NOCOUNT ON
	--DECLARE @objectid INT = 100032
    --DECLARE @objectdefid INT = 100824
    --DECLARE @agentid INT = 710
    --DECLARE @assochistory BIT = 1
    --DECLARE @objecthistory BIT = 1
    --DECLARE @lcid INT = 1033

	DECLARE @basetype INT
    
	SELECT @basetype= objecttype
	FROM [dbo].[hlsysobjectdef]
	WHERE [objectdefid] = @objectdefid


    ;WITH [objecthistorytables] AS
    (
    	SELECT [agentid]
    		 , [type]
    		 , [historyitem]
			 , [actioncontextid]
    	FROM dbo.hlsyscasehistory WITH (NOLOCK)
    	WHERE [casedefid] = @objectdefid AND [caseid] = @objectid AND @basetype = 2 AND @objecthistory =1
    	UNION ALL
    	SELECT [agentid]
    		 , [type]
    		 , [historyitem]
			 , [actioncontextid]
    	FROM dbo.hlsyspersonhistory WITH (NOLOCK)
    	WHERE [persondefid] = @objectdefid AND [personid] = @objectid AND @basetype = 3 AND @objecthistory =1
    	UNION ALL
    	SELECT [agentid]
    		 , [type]
    		 , [historyitem]
			 , [actioncontextid]
    	FROM dbo.hlsysorgunithistory WITH (NOLOCK)
    	WHERE [orgunitdefid] = @objectdefid AND [orgunitid] = @objectid AND @basetype = 4 AND @objecthistory =1
    	UNION ALL
    	SELECT [agentid]
    		 , [type]
    		 , [historyitem]
			 , [actioncontextid]
    	FROM dbo.hlsysproducthistory WITH (NOLOCK)
    	WHERE [productdefid] = @objectdefid AND [productid] = @objectid AND @basetype = 5 AND @objecthistory =1
    	UNION ALL
    	SELECT [agentid]
    		 , [type]
    		 , [historyitem]
			 , [actioncontextid]
    	FROM dbo.hlsyscontracthistory WITH (NOLOCK)
    	WHERE [contractdefid] = @objectdefid AND [contractid] = @objectid AND @basetype = 7 AND @objecthistory =1
    ),
    [finalresult] AS
    (
    	SELECT [agentid]
    		 , [type]
    		 , [historyitem]
    		 , NULL AS [associationdefid]
    		 , NULL AS [otherobjectid]
    		 , NULL AS [otherobjectdefid]
    		 , NULL AS [isobjecta]
			 , [actioncontextid]
    	FROM [objecthistorytables]
    	UNION ALL
    	SELECT [ah].[actionagentid]
    		, [ah].[actiontype]
    		, NULL AS [historyitem]
    		, [ah].[associationdefid]
    		, CASE WHEN [ah].[objectida] = @objectid AND [ah].[objectdefida] = @objectdefid THEN [ah].[objectidb] ELSE [ah].[objectida] END AS [otherobjectid]
    		, od.objectdefid AS [otherobjectdefid]
    		, CAST(CASE WHEN [ah].[objectida] = @objectid AND [ah].[objectdefida] = @objectdefid THEN 1 ELSE 0 END AS BIT) AS [isobjecta]
			, [actioncontextid]
    	FROM [dbo].[hlsysassociationhistory] AS ah WITH (NOLOCK)
    	JOIN [dbo].[hlsysobjectdef] AS od WITH (NOLOCK) ON od.objectdefid = CASE WHEN [ah].[objectida] = @objectid AND [ah].[objectdefida] = @objectdefid THEN [ah].[objectdefidb] ELSE [ah].[objectdefida] END 
    	CROSS APPLY dbo.hlsyssec_query_agentobjectprmread(
    		@agentid
    	  , CASE WHEN [ah].[objectida] = @objectid AND [ah].[objectdefida] = @objectdefid THEN [ah].[objectidb] ELSE [ah].[objectida] END
    	  , od.objectdefid
    	  , CASE od.objecttype
    			WHEN 2 THEN 785 -- CASE
    			WHEN 3 THEN 786 -- PERSON
    			WHEN 4 THEN 787 -- ORGANIZATION
    			WHEN 5 THEN 788 -- PRODUCT
    			WHEN 7 THEN 789 -- CONTRACT
    			ELSE 0 END) AS sec
    	WHERE @assochistory = 1 AND (
    		  ([objectida] = @objectid AND [objectdefida] = @objectdefid OR 
    		   [objectidb] = @objectid AND [objectdefidb] = @objectdefid) AND sec.canread = 1)
    )
    SELECT [ac].[creationtime] AS [timestamp]
    	 , [fr].[agentid]
    	 , [fr].[type]
    	 , [fr].[historyitem]
    	 , [fr].[associationdefid]
    	 , [fr].[otherobjectid]
    	 , [fr].[otherobjectdefid]
    	 , [fr].[isobjecta]
    	 , ISNULL(NULLIF([a].[fullname], N''), [a].[name]) AS [agentname]
    	 , [adef].[name] AS [associationdefname]
    	 , [ad].[displayname] AS [associationdisplayname]
    	 , [da].[defaultvalue] AS [otherobjectname]
		 
    FROM [finalresult] AS fr
    INNER JOIN [dbo].[hlsysactioncontext] AS ac ON fr.actioncontextid = ac.actioncontextid
	LEFT JOIN [dbo].[hlsysagent] AS a WITH (NOLOCK) ON [fr].[agentid] = [a].[agentid]
    LEFT JOIN [dbo].[hlsysassociationdef] AS adef WITH (NOLOCK) ON [fr].[associationdefid] = [adef].[associationdefid]
    LEFT JOIN [dbo].[hlsysdisplayname] AS ad WITH (NOLOCK) ON [fr].[associationdefid] = [ad].[reposid] AND [ad].[languageid] = @lcid
    OUTER APPLY [dbo].[hlsysdefaultattr_query] ([fr].[otherobjectdefid], [fr].[otherobjectid], @lcid) AS da

	ORDER BY [ac].[creationtime] ASC
    	   , CASE WHEN [fr].[historyitem] IS NULL THEN 0 ELSE 1 END DESC
END
RETURN 0
