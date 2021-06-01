CREATE TABLE [dbo].[hlsysdetailcfgfieldassoc]
(
    [detailcfgid]      INT     NOT NULL
  , [displayregion]    TINYINT NOT NULL
  , [sortorder]        TINYINT NOT NULL
  , [fieldtype]        TINYINT NOT NULL
  , [associationdefid] INT     NOT NULL
  , [objecttypeb]      INT     NOT NULL
  , CONSTRAINT [PK_hlsysdetailcfgfieldassoc] PRIMARY KEY ([detailcfgid] ASC, [displayregion] ASC, [sortorder] ASC)
  , CONSTRAINT [FK_hlsysdetailcfgfieldassoc_field] FOREIGN KEY ([detailcfgid], [displayregion], [sortorder], [fieldtype]) REFERENCES [dbo].[hlsysdetailcfgfield] ([detailcfgid], [displayregion], [sortorder], [fieldtype]) ON DELETE CASCADE
  , CONSTRAINT [FK_hlsysdetailcfgfieldassoc_assoc] FOREIGN KEY ([associationdefid]) REFERENCES [dbo].[hlsysassociationdef] ([associationdefid])
  , CONSTRAINT [FK_hlsysdetailcfgfieldassoc_basetype] FOREIGN KEY ([objecttypeb]) REFERENCES [dbo].[hlsysobjectbasetype] ([basetypeid])
  , CONSTRAINT [CK_hlsysdetailcfgfieldassoc_type] CHECK ([fieldtype] = 2)
  , CONSTRAINT [CK_hlsysdetailcfgfieldassoc_assoc] CHECK ([associationdefid] = 130 AND [objecttypeb] IN (3, 4) -- Customer2Case => Person, Organisation
                                                       OR [associationdefid] = 131 AND [objecttypeb] =      5) -- Product2Case  => Product
  , CONSTRAINT [UQ_hlsysdetailcfgfieldassoc_assoc] UNIQUE ([detailcfgid], [displayregion], [associationdefid], [objecttypeb])
)