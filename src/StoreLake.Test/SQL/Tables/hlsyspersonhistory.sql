CREATE TABLE [dbo].[hlsyspersonhistory](
	[historyid] INT NOT NULL,
	[personid] INT NOT NULL,
	[persondefid] INT NOT NULL,
	[creationtime] DATETIME NOT NULL,
	[agentid] INT NOT NULL,
	[type] SMALLINT NOT NULL,
	[historyitem] NVARCHAR(MAX) NOT NULL,
	[historyitemsize] INT NOT NULL,
	[actioncontextid] BIGINT NOT NULL
	, CONSTRAINT [PK_hlsyspersonhistory] PRIMARY KEY CLUSTERED ([historyid] ASC,[personid] ASC)
	, CONSTRAINT [FK_hlsyspersonhistory_persondef] FOREIGN KEY ([persondefid]) REFERENCES [dbo].[hlsyspersondef]([persondefid])
	, CONSTRAINT [FK_hlsyspersonhistory_action] FOREIGN KEY ([actioncontextid],[agentid]) REFERENCES [dbo].[hlsysactioncontext]([actioncontextid],[actionagentid])
	, CONSTRAINT [CK_hlsyspersonhistory_personid] CHECK ([personid]>0)
	, CONSTRAINT [CK_hlsyspersonhistory_type] CHECK ([type]=1 OR [type]=2 OR [type]=3) -- enum ObjectHistoryItemState : {1:Inserted; 2:Updated; 3:Deleted}
)
GO

CREATE NONCLUSTERED INDEX [IX_hlsyspersonhistory_personiddefid] ON [dbo].[hlsyspersonhistory]([personid],[persondefid])
GO

CREATE NONCLUSTERED INDEX [IX_hlsyspersonhistory_persondefid] ON [dbo].[hlsyspersonhistory]([persondefid])
GO
