CREATE TABLE [dbo].[hlsysapprovementfulfillment]
(
    [approvementid] UNIQUEIDENTIFIER NOT NULL
  , [reason]        NVARCHAR(1024)   NULL
  , [approved]      BIT              NOT NULL
  , [approvedat]    DATETIME         NOT NULL
  , [state]         TINYINT          NOT NULL -- 1: Pending | 2: Processed
  , [closereason]   TINYINT          NOT NULL -- 1: User | 2: NeededApprovementsReached | 3: NeededRejectionsReached | 4: NeededApprovementsCanNotBeReached
  , CONSTRAINT [PK_hlsysapprovementfulfillment] PRIMARY KEY CLUSTERED ([approvementid] ASC)
  , CONSTRAINT [FK_hlsysapprovementfulfillment_apprvmnts] FOREIGN KEY([approvementid]) REFERENCES [dbo].[hlsysapprovements] ([approvementid])
  , CONSTRAINT [CK_hlsysapprovementfulfillment_state] CHECK ([state] = 1 OR [state] = 2)
  , CONSTRAINT [CK_hlsysapprovementfulfillment_closedby] CHECK ([closereason] = 1 OR [closereason] = 2 OR [closereason] = 3 OR [closereason] = 4)
) 