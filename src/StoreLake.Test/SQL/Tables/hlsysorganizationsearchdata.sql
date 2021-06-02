CREATE TABLE [dbo].[hlsysorganizationsearchdata] (
	[orgunitid] INT NOT NULL,
	[orgunitdefid] INT NOT NULL,
	[version] INT NOT NULL,
	[creationtime] DATETIME NOT NULL,
	[lastmodified] DATETIME NOT NULL,
	[owner] INT NOT NULL,
	[name] NVARCHAR(255) NULL,

	CONSTRAINT [PK_hlsysorganizationsearchdata] PRIMARY KEY CLUSTERED ([orgunitid]),
	CONSTRAINT [FK_hlsysorganizationsearchdata_orgunitdefid] FOREIGN KEY([orgunitdefid]) REFERENCES [dbo].[hlsysobjectdef] ([objectdefid]),
    CONSTRAINT [UQ_hlsysorganizationsearchdata_orgunitid] UNIQUE NONCLUSTERED ([orgunitid], [orgunitdefid])
)

GO

CREATE NONCLUSTERED INDEX [IX_hlsysorganizationsearchdata_orgunitdefid] ON [dbo].[hlsysorganizationsearchdata](orgunitdefid)