CREATE TABLE [dbo].[hlsysproducthistory](
	[historyid] INT NOT NULL,
	[productid] INT NOT NULL,
	[productdefid] INT NOT NULL,
	[creationtime] DATETIME NOT NULL,
	[agentid] INT NOT NULL,
	[type] SMALLINT NOT NULL,
	[historyitem] NVARCHAR(MAX) NOT NULL,
	[historyitemsize] INT NOT NULL,
	[actioncontextid] BIGINT NOT NULL
	, CONSTRAINT [PK_hlsysproducthistory] PRIMARY KEY CLUSTERED ([historyid] ASC, [productid] ASC)
	, CONSTRAINT [FK_hlsysproducthistory_productdef] FOREIGN KEY ([productdefid]) REFERENCES [dbo].[hlsysproductdef]([productdefid])
	, CONSTRAINT [FK_hlsysproducthistory_action] FOREIGN KEY ([actioncontextid],[agentid]) REFERENCES [dbo].[hlsysactioncontext]([actioncontextid],[actionagentid])
	, CONSTRAINT [CK_hlsysproducthistory_productid] CHECK ([productid]>0)
	, CONSTRAINT [CK_hlsysproducthistory_type] CHECK ([type]=1 OR [type]=2 OR [type]=3) -- enum ObjectHistoryItemState : {1:Inserted; 2:Updated; 3:Deleted}
)
GO

CREATE NONCLUSTERED INDEX [IX_hlsysproducthistory_productiddefid] ON [dbo].[hlsysproducthistory]([productid],[productdefid])
GO

CREATE NONCLUSTERED INDEX [IX_hlsysproducthistory_productdefid] ON [dbo].[hlsysproducthistory]([productdefid])
GO
