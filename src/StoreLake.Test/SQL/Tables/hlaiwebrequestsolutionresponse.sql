CREATE TABLE [dbo].[hlaiwebrequestsolutionresponse]
(
    [requestid]         INT            NOT NULL
  , [recommendationsid] NCHAR(36)      NOT NULL
  , [confidence]        DECIMAL(19,18) NOT NULL
  , CONSTRAINT [PK_hlaiwebrequestsolutionresponse] PRIMARY KEY ([requestid])
  , CONSTRAINT [FK_hlaiwebrequestsolutionresponse_request] FOREIGN KEY ([requestid]) REFERENCES [dbo].[hlaiwebrequestsolution] ([requestid]) ON DELETE CASCADE
  , CONSTRAINT [UQ_hlaiwebrequestsolutionresponse_recommendationsid] UNIQUE ([recommendationsid])
  , CONSTRAINT [CK_hlaiwebrequestsolutionresponse_recommendationsid] CHECK (LEN([recommendationsid]) = 36)
  , CONSTRAINT [CK_hlaiwebrequestsolutionresponse_confidence] CHECK ([confidence] >= 0 AND [confidence] <= 1)
)