CREATE TABLE [dbo].[hlsysobjectdata]
(
	[objectid] INT NOT NULL,
	[objectdefid] INT NOT NULL,
	[objecttype] INT NOT NULL,
	[objectdata] XML NOT NULL,
	[creationtime] DATETIME NOT NULL,
	[agentid] INT NOT NULL,
	[currenthistorystep] INT NOT NULL,
	[sourceobjectversion] INT NOT NULL  -- for fast decision without using the systemtables twice (). #PBI: 148015#
		CONSTRAINT DF_hlsysobjectdata_sourceobjectversion DEFAULT(0) 
		CONSTRAINT CK_hlsysobjectdata_sourceobjectversion CHECK([sourceobjectversion] >= 0),
	[attachmentcount] INT NOT NULL
		CONSTRAINT DF_hlsysobjectdata_attachmentcount DEFAULT(0) 
		CONSTRAINT CK_hlsysobjectdata_attachmentcount CHECK([attachmentcount] >= 0)
	, CONSTRAINT [PK_hlsysobjectdata] PRIMARY KEY CLUSTERED ([objectid] ASC)
	, CONSTRAINT [FK_hlsysobjectdata_def] FOREIGN KEY ([objectdefid],[objecttype]) REFERENCES [dbo].[hlsysobjectdef]([objectdefid],[objecttype])
	, CONSTRAINT [FK_hlsysobjectdata_agentid] FOREIGN KEY([agentid]) REFERENCES [dbo].[hlsysagent]([agentid])
)
GO

CREATE NONCLUSTERED INDEX IX_hlsysobjectdata_defid ON [dbo].[hlsysobjectdata]([objectdefid]) INCLUDE([objectid],[currenthistorystep],[sourceobjectversion],[objecttype]) -- UseCase DeleteSearchItems/defid
GO
CREATE NONCLUSTERED INDEX IX_hlsysobjectdata_objectid_objectdefid_currenthistorystep ON [dbo].[hlsysobjectdata]([objectid], [objectdefid], [currenthistorystep]) INCLUDE([sourceobjectversion]) -- UseCase : syncsearch
GO
CREATE NONCLUSTERED INDEX IX_hlsysobjectdata_objectid_currenthistorystep ON [dbo].[hlsysobjectdata]([objectid], [currenthistorystep]) INCLUDE([objectdefid], [sourceobjectversion]) -- UseCase : syncsearch
GO
CREATE NONCLUSTERED INDEX IX_hlsysobjectdata_objectdefid_currenthistorystep ON [dbo].[hlsysobjectdata]([objectdefid],[currenthistorystep]) INCLUDE([objectid],[sourceobjectversion]) -- UseCase DeleteSearchItems/defid
GO
--?? CREATE NONCLUSTERED INDEX IX_hlsysobjectdata_objecttype ON [dbo].[hlsysobjectdata]([objecttype]) INCLUDE([objectid],[objectdefid],[currenthistorystep],[sourceobjectversion]) -- UseCase SeacrhItems/defid

