CREATE TABLE [dbo].[hlsysassociation]
(
	[associationid] INT NOT NULL CONSTRAINT [CK_hlsysassociation_idc_zr] CHECK ([associationid] > 0), -- see Bug_106197
	[associationdefid] INT NOT NULL,
	[objectida] INT NOT NULL CONSTRAINT [CK_hlsysassociation_ida_zr] CHECK ([objectida] > 0),
	[objectdefida] INT NOT NULL,
	[objectidb] INT NOT NULL CONSTRAINT [CK_hlsysassociation_idb_zr] CHECK ([objectidb] > 0),
	[objectdefidb] INT NOT NULL,
	[creationtime] DATETIME NOT NULL,
	[lastmodified] DATETIME NOT NULL,
	[owner] INT NOT NULL
	, CONSTRAINT [PK_hlsysassociation] PRIMARY KEY CLUSTERED ([associationid] ASC)
	, CONSTRAINT [FK_hlsysassociation_asdefid] FOREIGN KEY ([associationdefid]) REFERENCES [dbo].[hlsysassociationdef]([associationdefid])
	, CONSTRAINT [FK_hlsysassociation_oadefid] FOREIGN KEY ([objectdefida]) REFERENCES [dbo].[hlsysobjectdef]([objectdefid])
	, CONSTRAINT [FK_hlsysassociation_obdefid] FOREIGN KEY ([objectdefidb]) REFERENCES [dbo].[hlsysobjectdef]([objectdefid])
	, CONSTRAINT [CK_hlsysassociation_aid_bid] CHECK ([objectida]<>[objectidb])
	-- Needed for performance 
	, CONSTRAINT [UQ_hlsysassociation_cabidaidb] UNIQUE ([associationdefid],[objectdefida],[objectida],[objectdefidb],[objectidb]) -- @lubo(v62):Nice if this works for old customers!!! If not use the script below.
	, CONSTRAINT [FK_hlsysassociation_owner] FOREIGN KEY ([owner]) REFERENCES [dbo].[hlsysagent]([agentid]) -- required by the trigger
)
GO
CREATE NONCLUSTERED INDEX [IX_hlsysassociation_cabida]
    ON [dbo].[hlsysassociation]([associationdefid] ASC, [objectdefida] ASC, [objectdefidb] ASC, [objectida] ASC)
    INCLUDE([associationid], [objectidb]);
GO
CREATE NONCLUSTERED INDEX [IX_hlsysassociation_cabidb]
    ON [dbo].[hlsysassociation]([associationdefid] ASC, [objectdefida] ASC, [objectdefidb] ASC, [objectidb] ASC)
    INCLUDE([associationid], [objectida]);
GO

CREATE NONCLUSTERED INDEX [IX_hlsysassociation_rolea] -- @lubo: v63up3 UseCase delete object
    ON [dbo].[hlsysassociation]([objectida] ASC, [objectdefida] ASC)
    INCLUDE([associationid], [associationdefid], [objectidb], [objectdefidb]);
GO

CREATE NONCLUSTERED INDEX [IX_hlsysassociation_roleb] -- @lubo: v63up3 UseCase delete object
    ON [dbo].[hlsysassociation]([objectidb] ASC, [objectdefidb] ASC)
    INCLUDE([associationid], [associationdefid], [objectida], [objectdefida]);
GO
/* HOW TO FIND/SHOW/ELIMINATE duplicates:

SELECT ai.associationdefid, ai.objectida, ai.objectidb, COUNT(ai.associationid)
  FROM dbo.hlsysassociation AS ai 
  GROUP BY ai.associationdefid, ai.objectida, ai.objectidb
  HAVING COUNT(ai.associationid) > 1;

--SELECT * FROM dbo.hlsysassociation AS ai  WHERE ai.associationdefid = ... AND ai.objectida = ... AND ai.objectidb = ...;
--DELETE ai FROM dbo.hlsysassociation AS ai WHERE ai.associationdefid = ... AND ai.objectida = ... AND ai.objectidb = ...;
*/