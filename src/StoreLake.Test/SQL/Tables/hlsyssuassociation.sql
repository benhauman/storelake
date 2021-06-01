CREATE TABLE [dbo].[hlsyssuassociation]
(
	[suid] INT NOT NULL,
	[sudefid] INT NOT NULL,
	[caseid] INT NOT NULL,

	[associationid] INT NOT NULL,
	[associationdefid] INT NOT NULL,
	[objectidb] INT NOT NULL,
	[objectdefidb] INT NOT NULL,
	[objecttypeb] INT NOT NULL

	, CONSTRAINT [PK_hlsyssuassociation] PRIMARY KEY ([suid],[associationid])
	, CONSTRAINT [FK_hlsyssuassociation_sukey] FOREIGN KEY ([suid],[caseid],[sudefid]) REFERENCES [dbo].[hlsyssusystem]([suid],[caseid],[sudefid]) -- see 'UQ_hlsyssusystem_suid_caseid_sudefid'
	, CONSTRAINT [FK_hlsyssuassociation_associationid] FOREIGN KEY ([associationid]) REFERENCES [dbo].[hlsysassociation]([associationid])
	, CONSTRAINT [FK_hlsyssuassociation_associationdata] FOREIGN KEY ([associationdefid],[sudefid],[caseid],[objectdefidb],[objectidb]) REFERENCES [dbo].[hlsysassociation]([associationdefid],[objectdefida],[objectida],[objectdefidb],[objectidb])
	, CONSTRAINT [FK_hlsyssuassociation_objecttypeb] FOREIGN KEY ([objectdefidb],[objecttypeb]) REFERENCES [dbo].[hlsysobjectdef]([objectdefid],[objecttype])
	, CONSTRAINT [CK_hlsyssuassociation_associationdefid] CHECK ([associationdefid]=(130) OR [associationdefid]=(131))
	, CONSTRAINT [CK_hlsyssuassociation_objecttypeb] CHECK (([associationdefid]=(130) AND ([objecttypeb]=(3) OR [objecttypeb]=(4))) OR([associationdefid]=(131) AND [objecttypeb]=(5)))
	, INDEX [IX_hlsyssuassociation_associationid]([associationid])-- usecase : DELETE FROM [hlsysassociation] + [FK_hlsyssuassociation_associationid](1col)
	, INDEX [IX_hlsyssuassociation_associationdata]([associationdefid],[sudefid],[caseid],[objectdefidb],[objectidb])-- usecase : DELETE FROM [hlsysassociation] + [FK_hlsyssuassociation_associationdata](5col)
	, CONSTRAINT [UQ_hlsyssuassociation_defpoint] UNIQUE ([suid],[associationdefid],[objecttypeb]) 
	, CONSTRAINT [FK_hlsyssuassociation_defpoint] FOREIGN KEY ([associationdefid],[sudefid],[objectdefidb]) REFERENCES [dbo].[hlsysassociationdefpoint]([associationdefid],[objectdefida],[objectdefidb])
)
GO
CREATE NONCLUSTERED INDEX [IX_hlsyssuassociation_caller] ON [dbo].[hlsyssuassociation]([objectidb], [objectdefidb]) WHERE ([associationdefid] = 130 AND [objecttypeb] = 3) -- hlpm_ssp_casetable_query
GO
CREATE NONCLUSTERED INDEX [IX_hlsyssuassociation_caseid] ON [dbo].[hlsyssuassociation]([caseid]) INCLUDE ([associationdefid],[objecttypeb])-- usecase : DELETE case
GO