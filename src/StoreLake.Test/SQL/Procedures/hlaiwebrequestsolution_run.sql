-- @Name GetSolutions
-- @Return ClrTypes:SolutionResponse;Solution Mode:Single SplitOn:position
CREATE
--OR ALTER
PROCEDURE [dbo].[hlaiwebrequestsolution_run] 
    @agentid         INT
  , @machinename     NVARCHAR(513)
  , @pid             INT
  , @actioncontextid BIGINT
  , @casedefid       INT
  , @caseid          INT
  , @text            NVARCHAR(MAX)
AS
BEGIN
    SET XACT_ABORT ON

	DECLARE @cfgid TINYINT
    DECLARE @defaultconfidencethreshold DECIMAL(3,2)

    SELECT @cfgid = [cfgid], @defaultconfidencethreshold = [confidencethreshold]
    FROM [dbo].[hlaiconfiguration]
    WHERE [isactive] = 1

    IF @cfgid IS NULL
    BEGIN
        ;THROW 404001, N'No active service configuration', 1
    END

    IF NOT EXISTS(SELECT 1 FROM [dbo].[hlaiconfigurationdataset] WHERE [casedefid] = @casedefid)
    BEGIN
        ;THROW 404002, N'No data set configured for given case definition', 2
    END

    DECLARE @requestid INT
    DECLARE @initiatorconversationhandle UNIQUEIDENTIFIER
    
    -- Queue asynchronous request
    EXEC [dbo].[hlaiwebrequestsolution_publish] @machinename                 = @machinename
                                              , @pid                         = @pid
                                              , @actioncontextid             = @actioncontextid
                                              , @cfgid                       = @cfgid
                                              , @casedefid                   = @casedefid
                                              , @caseid                      = @caseid
                                              , @text                        = @text
                                              , @requestid                   = @requestid OUTPUT
                                              , @initiatorconversationhandle = @initiatorconversationhandle OUTPUT
    
    -- Synchronize frontend and backend
    EXEC [dbo].[hlaiwebrequest_sync] @requestid = @requestid, @initiatorconversationhandle = @initiatorconversationhandle

    SELECT [requestid]   = [r].[requestid]
         , [isconfident] = IIF([r].[confidence] >= @defaultconfidencethreshold, 1, 0)
         , [position]    = [rs].[position]
         , [subject]     = [rs].[subject]
         , [excerpt]     = IIF(LEN([rs].[description]) > 100, CONCAT(LEFT([rs].[description], 100), N'...'), [rs].[description])
    FROM [dbo].[hlaiwebrequestsolutionresponse] AS [r]
    OUTER APPLY (
        SELECT [rs].[position], [c].[subject], [c].[description], [c].[solution], [rs].[feedbackvalue]
        FROM [dbo].[hlaiwebrequestsolutionresponsesuggestion] AS [rs] 
        INNER JOIN [dbo].[hlsyscasedata] AS [c] ON [rs].[casedefid] = [c].[casedefid] AND [rs].[caseid] = [c].[caseid]
        CROSS APPLY [dbo].[hlsyssec_query_agentcaseprmread](@agentid, [rs].[caseid], [rs].[casedefid]) AS [s]
        WHERE [rs].[requestid] = [r].requestid AND [s].[canread] = 1
    ) AS [rs]
    WHERE [r].[requestid] = @requestid
    ORDER BY [rs].[position]

    -- Complete request
    EXEC [dbo].[hlaiwebrequest_complete] @requestid = @requestid
END