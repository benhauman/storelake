CREATE TABLE [dbo].[hlsyscasehistory]
(
	[historyid] INT NOT NULL,
	[caseid] INT NOT NULL,
	[casedefid] INT NOT NULL,
	[creationtime] DATETIME NOT NULL,
	[agentid] INT NOT NULL,
	[type] SMALLINT NOT NULL, -- enum ObjectHistoryItemState : {1:Inserted; 2:Updated; 3:Deleted}
	[historyitem] NVARCHAR(MAX) NOT NULL,
	[historyitemsize] INT NOT NULL,
	[actioncontextid] BIGINT NOT NULL
	, CONSTRAINT [PK_hlsyscasehistory] PRIMARY KEY CLUSTERED ([historyid] ASC,[caseid] ASC)
	, CONSTRAINT [FK_hlsyscasehistory_casedef] FOREIGN KEY ([casedefid]) REFERENCES [dbo].[hlsyscasedef]([casedefid])
	, CONSTRAINT [FK_hlsyscasehistory_action] FOREIGN KEY ([actioncontextid],[agentid]) REFERENCES [dbo].[hlsysactioncontext]([actioncontextid],[actionagentid])
	, CONSTRAINT [CK_hlsyscasehistory_caseid] CHECK ([caseid]>0)
	, CONSTRAINT [CK_hlsyscasehistory_type] CHECK ([type]=1 OR [type]=2 OR [type]=3) -- enum ObjectHistoryItemState : {1:Inserted; 2:Updated; 3:Deleted}
)
GO

CREATE NONCLUSTERED INDEX [IX_hlsyscasehistory_caseiddefid] ON [dbo].[hlsyscasehistory]([caseid],[casedefid])
GO

CREATE NONCLUSTERED INDEX [IX_hlsyscasehistory_casedefid] ON [dbo].[hlsyscasehistory]([casedefid])
GO
