CREATE TABLE [dbo].[hlsyswatchlist]
(			      
    [objid]       INT	   NOT NULL
  , [defid]       INT	   NOT NULL
  , [agentid]	  INT	   NOT NULL
  , [createdtime] DATETIME NOT NULL
  , CONSTRAINT [PK_hlsyswatchlist] PRIMARY KEY ([objid] ASC, [agentid] ASC)
  , CONSTRAINT [FK_hlsyswatchlist_def] FOREIGN KEY ([defid]) REFERENCES [dbo].[hlsyscasedef] ([casedefid])
  , CONSTRAINT [FK_hlsyswatchlist_agent] FOREIGN KEY ([agentid]) REFERENCES [dbo].[hlsysagent] ([agentid])
  , CONSTRAINT [CK_hlsyswatchlist_objid] CHECK ([objid] > 0)
)
GO

CREATE NONCLUSTERED INDEX [IX_hlsyswatchlist_agentid] ON [dbo].[hlsyswatchlist] ([agentid] ASC) INCLUDE ([objid], [defid])