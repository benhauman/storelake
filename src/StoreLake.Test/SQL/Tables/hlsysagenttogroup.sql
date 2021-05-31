CREATE TABLE [dbo].[hlsysagenttogroup]
(
    [agentid] INT NOT NULL,
	[groupid] INT NOT NULL,
	[inherited] BIT NOT NULL CONSTRAINT DF_hlsysagenttogroup_inherited DEFAULT(0)
	,CONSTRAINT [PK_hlsysagenttogroup] PRIMARY KEY CLUSTERED 
	(
		[agentid] ASC,
		[groupid] ASC
	)
    ,CONSTRAINT [FK_hlsysagenttogroup_hlsysagent] FOREIGN KEY ([agentid]) REFERENCES [dbo].[hlsysagent]([agentid])
    ,CONSTRAINT [FK_hlsysagenttogroup_hlsysgroup] FOREIGN KEY ([groupid]) REFERENCES [dbo].[hlsysgroup]([groupid])

	, INDEX [IX_hlsysagenttogroup_inherited] NONCLUSTERED ([inherited] ASC)
)
GO

CREATE NONCLUSTERED INDEX [IX_hlsysagenttogroup_group] ON [dbo].[hlsysagenttogroup]([groupid] ASC) INCLUDE([agentid])
GO

CREATE NONCLUSTERED INDEX [IX_hlsysagenttogroup_agent] ON [dbo].[hlsysagenttogroup]([agentid] ASC) INCLUDE([groupid])
GO

