CREATE TABLE [dbo].[hlspinstance]
(
	[spinstanceid] INT NOT NULL IDENTITY(1,1),
	[processinstanceid] UNIQUEIDENTIFIER NOT NULL,
	[spdefinitionid] INT NOT NULL,
	[creationtime] DATETIME NOT NULL
		CONSTRAINT DF_hlspinstance_creationtime DEFAULT(GETUTCDATE())
		,
	[lastmodified] DATETIME NOT NULL
		CONSTRAINT DF_hlspinstance_lastmodified DEFAULT(GETUTCDATE())
		,
	[version] INT NOT NULL
		CONSTRAINT CK_hlspinstance_version CHECK ([version] > 0)
		CONSTRAINT DF_hlspinstance_version DEFAULT(1)
		,
	CONSTRAINT [PK_hlspinstance] PRIMARY KEY ([spinstanceid] ASC),
	CONSTRAINT [FK_hlspinstance_spdefinitionid] FOREIGN KEY ([spdefinitionid]) REFERENCES [dbo].[hlspdefinition] ([spdefinitionid]),
	CONSTRAINT [UQ_hlspinstance_spiddefid] UNIQUE NONCLUSTERED ([spinstanceid], [spdefinitionid]),
	CONSTRAINT [UQ_hlspinstance_procinstid] UNIQUE NONCLUSTERED ([processinstanceid])
)
