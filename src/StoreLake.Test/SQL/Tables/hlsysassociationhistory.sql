CREATE TABLE [dbo].[hlsysassociationhistory]
(
	[associationid] INT NOT NULL
  , [associationdefid] INT NOT NULL
  , [objectida] INT NOT NULL
  , [objectdefida] INT NOT NULL
  , [objectidb] INT NOT NULL
  , [objectdefidb] INT NOT NULL
  , [actiontype] TINYINT NOT NULL
  , [actionagentid] INT NOT NULL -- @lubo: different agents in one actioncontext (impersonation!)
  , [actiontimestamp] DATETIME NOT NULL
  , [actioncontextid] BIGINT NOT NULL
  , CONSTRAINT [PK_hlsysassociationhistory] PRIMARY KEY ([associationid], [actiontype])
  , CONSTRAINT [FK_hlsysassociationhistory_associationdefid] FOREIGN KEY ([associationdefid]) REFERENCES [dbo].[hlsysassociationdef]([associationdefid])
  , CONSTRAINT [FK_hlsysassociationhistory_objectdefida] FOREIGN KEY ([objectdefida]) REFERENCES [dbo].[hlsysobjectdef]([objectdefid])
  , CONSTRAINT [FK_hlsysassociationhistory_objectdefidb] FOREIGN KEY ([objectdefidb]) REFERENCES [dbo].[hlsysobjectdef]([objectdefid])
  , CONSTRAINT [FK_hlsysassociationhistory_actioncontextid] FOREIGN KEY ([actioncontextid]) REFERENCES [dbo].[hlsysactioncontext] ([actioncontextid])
  , CONSTRAINT [FK_hlsysassociationhistory_actionagentid] FOREIGN KEY ([actionagentid]) REFERENCES [dbo].[hlsysagent] ([agentid]) -- @lubo: different agents in one actioncontext (impersonation!)
  , CONSTRAINT [CK_hlsysassociationhistory_actiontype] CHECK ([actiontype]=(3) OR [actiontype]=(1))
)
GO
CREATE NONCLUSTERED INDEX [IX_hlsysassociationhistory_objectiddefida]
ON [dbo].[hlsysassociationhistory] ([objectida],[objectdefida])
--INCLUDE ([associationdefid],[objectidb],[objectdefidb], [actionagentid],[actiontimestamp], [actioncontextid])
GO

CREATE NONCLUSTERED INDEX [IX_hlsysassociationhistory_objectiddefidb]
ON [dbo].[hlsysassociationhistory] ([objectidb],[objectdefidb])
--INCLUDE ([associationdefid],[objectida],[objectdefida],[actionagentid],[actiontimestamp], [actioncontextid])
GO