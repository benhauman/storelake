CREATE TABLE [dbo].[hlaiwebrequestsolutionresponsesuggestion]
(
    [requestid]     INT            NOT NULL
  , [position]      TINYINT        NOT NULL
  , [casedefid]     INT            NOT NULL
  , [caseid]        INT            NOT NULL
  , [lastmodified]  DATETIME       NOT NULL
  , [similarity]    DECIMAL(19,18) NOT NULL
  , [feedbackvalue] TINYINT        NULL
  , CONSTRAINT [PK_hlaiwebrequestsolutionresponsesuggestion] PRIMARY KEY ([requestid], [position])
  , CONSTRAINT [FK_hlaiwebrequestsolutionresponsesuggestion_request] FOREIGN KEY ([requestid]) REFERENCES [dbo].[hlaiwebrequestsolutionresponse] ([requestid]) ON DELETE CASCADE
  , CONSTRAINT [FK_hlaiwebrequestsolutionresponsesuggestion_casedef] FOREIGN KEY ([requestid], [casedefid]) REFERENCES [dbo].[hlaiwebrequestsolution] ([requestid], [requestcasedefid])
  , CONSTRAINT [UQ_hlaiwebrequestsolutionresponsesuggestion_case] UNIQUE ([requestid], [caseid])
  , CONSTRAINT [CK_hlaiwebrequestsolutionresponsesuggestion_position] CHECK ([position] > 0)
  , CONSTRAINT [CK_hlaiwebrequestsolutionresponsesuggestion_caseid] CHECK ([caseid] > 0)
  , CONSTRAINT [CK_hlaiwebrequestsolutionresponsesuggestion_feedbackvalue] CHECK ([feedbackvalue] IS NULL
                                                                               OR [feedbackvalue] = 1  -- Neutral
                                                                               OR [feedbackvalue] = 2  -- Positive
                                                                               OR [feedbackvalue] = 3) -- Negative
)