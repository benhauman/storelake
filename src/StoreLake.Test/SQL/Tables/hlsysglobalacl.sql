CREATE TABLE [dbo].[hlsysglobalacl]
(
	[id] INT NOT NULL,
	[groupid] INT NOT NULL,
	[accessmask] SMALLINT NOT NULL
, CONSTRAINT [PK_hlsysglobalacl] PRIMARY KEY CLUSTERED ([id],[groupid])
, CONSTRAINT [FK_hlsysglobalacl_hlsysgroup] FOREIGN KEY ([groupid]) REFERENCES [dbo].[hlsysgroup]([groupid])
, CONSTRAINT [FK_hlsysglobalacl_id] FOREIGN KEY ([id]) REFERENCES [dbo].[hlsysglobalpolicy]([globalid])
)
