CREATE TABLE dbo.hlsysattrpathodedef
(
      attrpathid INT IDENTITY(1,1) NOT NULL 
    , attrpath_text NVARCHAR(400) NOT NULL
    , odedefid INT NOT NULL
    --, isfixed BIT NOT NULL -- used for sorting on pathid generation
    , attrpath_lvl TINYINT NOT NULL
    , attrpath_multiple BIT NOT NULL
    , attrpath_required BIT NOT NULL
    , attrpath_readonly BIT NOT NULL CONSTRAINT DF_hlsysattrpathodedef_attrpath_readonly DEFAULT 0
    , attrpath_hidden BIT NOT NULL CONSTRAINT DF_hlsysattrpathodedef_attrpath_hidden DEFAULT 0

    , attr_defid INT NOT NULL, attr_type SMALLINT NOT NULL

    , parent_defid INT NULL, parent_type SMALLINT NULL
    , gparent_defid INT NULL, gparent_type SMALLINT NULL
    , ggparent_defid INT NULL, ggparent_type SMALLINT NULL

	, CONSTRAINT PK_hlsysattrpathodedef PRIMARY KEY ([attrpathid] ASC)

    , CONSTRAINT CK_hlsysattrpathodedef_parent_type CHECK ([parent_defid] IS NULL      AND [parent_type] IS NULL 
	                                                    OR [parent_defid] IS NOT NULL AND ([parent_type] = 7
																						OR [parent_type] = 13
																						OR [parent_type] = 17
																						OR [parent_type] = 18 
														                                OR [parent_type] = 24
																						OR [parent_type] = 25))
    , CONSTRAINT CK_hlsysattrpathodedef_gparent_type CHECK ([gparent_defid] IS NULL     AND [gparent_type] IS NULL
	                                                     OR [gparent_defid] IS NOT NULL AND [gparent_type] = 24)
    , CONSTRAINT CK_hlsysattrpathodedef_ggparent_type CHECK ([ggparent_defid] IS NULL     AND [ggparent_type] IS NULL
	                                                      OR [ggparent_defid] IS NOT NULL AND [ggparent_type] = 24)
    , CONSTRAINT CK_hlsysattrpathodedef_attrpathid CHECK (attrpathid>0)

	, CONSTRAINT FK_hlsysattrpathodedef_odedefid FOREIGN KEY ( [odedefid] ) REFERENCES [dbo].[hlsysodedef]([odedefid])
    , CONSTRAINT FK_hlsysattrpathodedef_attr_defid  FOREIGN KEY ( attr_defid ) REFERENCES dbo.hlsysattributedef(attrdefid)
    , CONSTRAINT FK_hlsysattrpathodedef_attr_idtype FOREIGN KEY ( attr_defid, attr_type ) REFERENCES dbo.hlsysattributedef(attrdefid,attrtype)

    , CONSTRAINT FK_hlsysattrpathodedef_parent_defid  FOREIGN KEY ( parent_defid ) REFERENCES dbo.hlsysattributedef(attrdefid)
    , CONSTRAINT FK_hlsysattrpathodedef_parent_idtype FOREIGN KEY ( parent_defid, parent_type ) REFERENCES dbo.hlsysattributedef(attrdefid,attrtype)

    , CONSTRAINT FK_hlsysattrpathodedef_gparent_defid  FOREIGN KEY ( gparent_defid ) REFERENCES dbo.hlsysattributedef(attrdefid)
    , CONSTRAINT FK_hlsysattrpathodedef_gparent_idtype FOREIGN KEY ( gparent_defid, gparent_type ) REFERENCES dbo.hlsysattributedef(attrdefid,attrtype)

    , CONSTRAINT FK_hlsysattrpathodedef_ggparent_defid  FOREIGN KEY ( ggparent_defid ) REFERENCES dbo.hlsysattributedef(attrdefid)
    , CONSTRAINT FK_hlsysattrpathodedef_ggparent_idtype FOREIGN KEY ( ggparent_defid, ggparent_type ) REFERENCES dbo.hlsysattributedef(attrdefid,attrtype)
    
	, CONSTRAINT UQ_hlsysattrpathodedef_attrtype UNIQUE ( attrpathid, attr_type )

	, CONSTRAINT UQ_hlsysattrpathodedef_path_ode_multiple_attrtype UNIQUE ( attrpathid, odedefid, attrpath_multiple, attr_type )
    , CONSTRAINT UQ_hlsysattrpathodedef_path_ode_multiple_attrtype_defid UNIQUE ( attrpathid, odedefid, attrpath_multiple, attr_type, attr_defid )
)
GO

CREATE UNIQUE NONCLUSTERED INDEX UQ_hlsysattrpathodedef_token_ids ON dbo.hlsysattrpathodedef(odedefid, attr_defid, parent_defid, gparent_defid, ggparent_defid)
    INCLUDE (attrpathid, attrpath_lvl, attrpath_multiple, attrpath_required
		  , attr_type, parent_type, gparent_type, ggparent_type)
GO

CREATE UNIQUE NONCLUSTERED INDEX UQ_hlsysattrpathodedef_attrpathode ON dbo.hlsysattrpathodedef(attrpathid, odedefid)
    INCLUDE (attrpath_lvl, attrpath_multiple, attrpath_required
		  ,attr_defid,attr_type, parent_type, gparent_type, ggparent_type)
GO

CREATE UNIQUE NONCLUSTERED INDEX UQ_hlsysattrpathodedef_ode_attrdef ON dbo.hlsysattrpathodedef(attrpathid, odedefid, attr_defid)
    INCLUDE (attrpath_lvl, attrpath_multiple, attrpath_required
		  ,attr_type, parent_type, gparent_type, ggparent_type)
GO

CREATE UNIQUE NONCLUSTERED INDEX UQ_hlsysattrpathodedef_ode_attrtype ON dbo.hlsysattrpathodedef(attrpathid, odedefid, attr_defid, attr_type)
    INCLUDE (attrpath_lvl, attrpath_multiple, attrpath_required
		  ,parent_type, gparent_type, ggparent_type)
GO

CREATE UNIQUE NONCLUSTERED INDEX UQ_hlsysattrpathodedef_attrpathtext ON dbo.hlsysattrpathodedef(attrpath_text)
    INCLUDE (attrpathid, odedefid, attr_defid, attrpath_lvl, attrpath_multiple, attrpath_required,attr_type, parent_type, gparent_type, ggparent_type)
GO