CREATE TABLE [dbo].[hlsysdetailcfgfieldattr]
(
    [detailcfgid]   INT     NOT NULL
  , [displayregion] TINYINT NOT NULL
  , [sortorder]     TINYINT NOT NULL
  , [fieldtype]     TINYINT NOT NULL
  , [objectdefid]   INT		NOT NULL
  , [odedefid]      INT     NOT NULL
  , [ispartofsu]    BIT     NOT NULL
  , [attrpathid]    INT     NOT NULL
  , CONSTRAINT [PK_hlsysdetailcfgfieldattr] PRIMARY KEY ([detailcfgid] ASC, [displayregion] ASC, [sortorder] ASC)
  , CONSTRAINT [FK_hlsysdetailcfgfieldattr_field] FOREIGN KEY ([detailcfgid], [displayregion], [sortorder], [fieldtype]) REFERENCES [dbo].[hlsysdetailcfgfield] ([detailcfgid], [displayregion], [sortorder], [fieldtype]) ON DELETE CASCADE
  , CONSTRAINT [FK_hlsysdetailcfgfieldattr_objectdef] FOREIGN KEY ([detailcfgid], [objectdefid]) REFERENCES [dbo].[hlsysdetailcfg] ([detailcfgid], [objectdefid])
  , CONSTRAINT [FK_hlsysdetailcfgfieldattr_path] FOREIGN KEY ([attrpathid], [odedefid]) REFERENCES [dbo].[hlsysattrpathodedef] ([attrpathid], [odedefid])
  , CONSTRAINT [FK_hlsysdetailcfgfieldattr_ode] FOREIGN KEY ([objectdefid], [odedefid], [ispartofsu]) REFERENCES [dbo].[hlsysodedeftoobjectdef] ([objectdefid], [odedefid], [ispartofsu])
  , CONSTRAINT [CK_hlsysdetailcfgfieldattr_type] CHECK ([fieldtype] = 1)
  , CONSTRAINT [CK_hlsysdetailcfgfieldattr_region] CHECK ([ispartofsu] = 0 AND ([displayregion] = 1 OR [displayregion] = 2 OR [displayregion] = 3)
                                                       OR [ispartofsu] = 1 AND ([displayregion] = 4 OR [displayregion] = 5))
  , CONSTRAINT [UQ_hlsysdetailcfgfieldattr_attr] UNIQUE ([detailcfgid], [displayregion], [attrpathid])
)