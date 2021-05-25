CREATE TABLE [dbo].[hlsyscontracthistory](
	[historyid] INT NOT NULL,
	[contractid] INT NOT NULL,
	[contractdefid] INT NOT NULL,
	[creationtime] DATETIME NOT NULL,
	[agentid] INT NOT NULL,
	[type] SMALLINT NOT NULL,
	[historyitem] NVARCHAR(MAX) NOT NULL,
	[historyitemsize] INT NOT NULL,
	[actioncontextid] BIGINT NOT NULL
	, CONSTRAINT [PK_hlsyscontracthistory] PRIMARY KEY CLUSTERED ([historyid] ASC,[contractid] ASC)
	, CONSTRAINT [FK_hlsyscontracthistory_contractdef] FOREIGN KEY ([contractdefid]) REFERENCES [dbo].[hlsyscontractdef]([contractdefid])
	, CONSTRAINT [FK_hlsyscontracthistory_action] FOREIGN KEY ([actioncontextid],[agentid]) REFERENCES [dbo].[hlsysactioncontext]([actioncontextid],[actionagentid])
	, CONSTRAINT [CK_hlsyscontracthistory_contractid] CHECK ([contractid]>0)
	, CONSTRAINT [CK_hlsyscontracthistory_type] CHECK ([type]=1 OR [type]=2 OR [type]=3) -- enum ObjectHistoryItemState : {1:Inserted; 2:Updated; 3:Deleted}
)
GO

CREATE NONCLUSTERED INDEX [IX_hlsyscontracthistory_contractiddefid] ON [dbo].[hlsyscontracthistory]([contractid],[contractdefid])
GO

CREATE NONCLUSTERED INDEX [IX_hlsyscontracthistory_contractdefid] ON [dbo].[hlsyscontracthistory]([contractdefid])
GO
