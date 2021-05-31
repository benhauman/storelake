CREATE TABLE [dbo].[hlsysobjectqueue]
(
    [objectid]     INT NOT NULL
  , [objectdefid]  INT NOT NULL
  , [agentid]      INT NOT NULL
  , [agentdesk]    BIT NOT NULL
  , [waitingqueue] BIT NOT NULL
  , [infodesk]     BIT NOT NULL
  , CONSTRAINT [PK_hlsysobjectqueue] PRIMARY KEY CLUSTERED ([objectid] ASC, [objectdefid] ASC, [agentid] ASC)
  , CONSTRAINT [FK_hlsysobjectqueue_objectdef] FOREIGN KEY ([objectdefid]) REFERENCES [dbo].[hlsysobjectdef] ([objectdefid])
  , CONSTRAINT [FK_hlsysobjectqueue_agent] FOREIGN KEY ([agentid]) REFERENCES [dbo].[hlsysagent] ([agentid])
  , CONSTRAINT [CK_hlsysobjectqueue_flags] CHECK ([agentdesk] > 0 OR [waitingqueue] > 0 OR [infodesk] > 0)
  , CONSTRAINT [FK_hlsysobjectqueue_objectid] FOREIGN KEY ([objectid]) REFERENCES [dbo].[hlsyscasesystem]([caseid])
  , CONSTRAINT [FK_hlsysobjectqueue_objectidobjectdefid] FOREIGN KEY ([objectid],[objectdefid]) REFERENCES [dbo].[hlsyscasesystem]([caseid],[casedefid])
)
GO

CREATE NONCLUSTERED INDEX [IX_hlsysobjectqueue_agentid] ON [dbo].[hlsysobjectqueue] ([agentid]) INCLUDE ([objectid], [objectdefid],[agentdesk], [waitingqueue], [infodesk])
GO
-- agentdesk:ad, waitingqueue:wq, controltable:ct
CREATE NONCLUSTERED INDEX [IX_hlsysobjectqueue_agnt_ad] ON [dbo].[hlsysobjectqueue] ([agentid], [agentdesk]) INCLUDE ([objectid], [objectdefid], [waitingqueue], [infodesk])
GO

CREATE NONCLUSTERED INDEX [IX_hlsysobjectqueue_object] ON [dbo].[hlsysobjectqueue] ([objectid], [objectdefid]) INCLUDE ([agentid], [agentdesk], [waitingqueue], [infodesk])
GO

-- Get agent waiting queue (called very often in load generator used by performance tests on - to be clarified if this is a heavily used customer scenario aswell)
CREATE NONCLUSTERED INDEX [IX_hlsysobjectqueue_agentwaitingqueue] ON [dbo].[hlsysobjectqueue]([agentid]) WHERE ([waitingqueue] = 1)
GO
