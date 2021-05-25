CREATE TABLE [dbo].[hlsysorgunithistory](
	[historyid] INT NOT NULL,
	[orgunitid] INT NOT NULL,
	[orgunitdefid] INT NOT NULL,
	[creationtime] DATETIME NOT NULL,
	[agentid] INT NOT NULL,
	[type] SMALLINT NOT NULL,
	[historyitem] NVARCHAR(MAX) NOT NULL,
	[historyitemsize] INT NOT NULL,
	[actioncontextid] BIGINT NOT NULL
	, CONSTRAINT [PK_hlsysorgunithistory] PRIMARY KEY CLUSTERED ([historyid] ASC,[orgunitid] ASC)
	, CONSTRAINT [FK_hlsysorgunithistory_orgunitdef] FOREIGN KEY ([orgunitdefid]) REFERENCES [dbo].[hlsysorgunitdef]([orgunitdefid])
	, CONSTRAINT [FK_hlsysorgunithistory_action] FOREIGN KEY ([actioncontextid],[agentid]) REFERENCES [dbo].[hlsysactioncontext]([actioncontextid],[actionagentid])
	, CONSTRAINT [CK_hlsysorgunithistory_orgunitid] CHECK ([orgunitid]>0)
	, CONSTRAINT [CK_hlsysorgunithistory_type] CHECK ([type]=1 OR [type]=2 OR [type]=3) -- enum ObjectHistoryItemState : {1:Inserted; 2:Updated; 3:Deleted}
)
GO

CREATE NONCLUSTERED INDEX [IX_hlsysorgunithistory_orgunitiddefid] ON [dbo].[hlsysorgunithistory]([orgunitid],[orgunitdefid])
GO

CREATE NONCLUSTERED INDEX [IX_hlsysorgunithistory_orgunitdefid] ON [dbo].[hlsysorgunithistory]([orgunitdefid])
GO
