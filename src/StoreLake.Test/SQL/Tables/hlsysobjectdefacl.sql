CREATE TABLE [dbo].[hlsysobjectdefacl](
	[objectdefid] INT NOT NULL, -- references objectdefinitions or associationdefinitions
	[objectdefname] NVARCHAR(255) NOT NULL,
	[groupid] INT NOT NULL,
	[accessmask] SMALLINT NOT NULL,
 CONSTRAINT [PK_hlsysobjectdefacl] PRIMARY KEY CLUSTERED 
(
	[objectdefid] ASC,
	[groupid] ASC
) 
,CONSTRAINT [FK_hlsysobjectdefacl_hlsysgroup] FOREIGN KEY([groupid]) REFERENCES [dbo].[hlsysgroup] ([groupid]) ON DELETE CASCADE
,CONSTRAINT [FK_hlsysobjectdefacl_hlsysobjectdefref_id] FOREIGN KEY ([objectdefid]) REFERENCES [dbo].[hlsysobjectdefref]([objectdefid])
,CONSTRAINT [FK_hlsysobjectdefacl_hlsysobjectdefref_name] FOREIGN KEY ([objectdefname]) REFERENCES [dbo].[hlsysobjectdefref]([objectdefname])
,CONSTRAINT [UQ_hlsysobjectdefacl_name_group] UNIQUE ([objectdefname], [groupid])
)
GO

CREATE NONCLUSTERED INDEX IX_hlsysobjectdefacl_defid ON [dbo].[hlsysobjectdefacl]([objectdefid]) INCLUDE([groupid], [accessmask])
GO