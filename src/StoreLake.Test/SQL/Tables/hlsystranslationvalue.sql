CREATE TABLE [dbo].[hlsystranslationvalue]
(
    [tthc] BIGINT NOT NULL, -- see HASHBYTES('SHA2_512', ttkey) + CAST AS BIGINT
    [ttkey] NVARCHAR(255) NOT NULL,
    [ttlcid] INT NOT NULL,
	[ttcategoryid] TINYINT NOT NULL
			CONSTRAINT DF_hlsystranslationvalue_ttcategoryid DEFAULT (0)
			CONSTRAINT CK_hlsystranslationvalue_categoryid CHECK ( -- see Helpline.Server.Repository.SysClasses.TranslationCategory
				   [ttcategoryid] = 0 -- Objectmodel
				OR [ttcategoryid] = 1 -- ObjectmodelContent
				OR [ttcategoryid] = 2 -- Error
				OR [ttcategoryid] = 3 -- Common
				OR [ttcategoryid] = 4 -- SuggestionItems
				OR [ttcategoryid] = 5 -- Templates
				OR [ttcategoryid] = 6 -- Infoprovider
				OR [ttcategoryid] = 7 -- Reports
				)
		,
    [ttvalue] NVARCHAR(2000) NOT NULL

    , CONSTRAINT PK_hlsystranslationvalue PRIMARY KEY([tthc],[ttlcid])
    , CONSTRAINT UQ_hlsystranslationvalue_key_lcid UNIQUE([ttkey],[ttlcid])
)