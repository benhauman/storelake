CREATE TABLE [dbo].[hlbiattributeconfig](
	[id] INT NOT NULL, -- manual generated id : max(id)+1
	[objectdefinition] NVARCHAR(255) NOT NULL,
	[attributepath] NVARCHAR(255) NOT NULL,
	[description] NVARCHAR(255) NULL,
	[tablename] NVARCHAR(255) NOT NULL,
	[columnname] NVARCHAR(255) NOT NULL
	, CONSTRAINT [PK_hlbiattributeconfig] PRIMARY KEY CLUSTERED ([id] ASC)
	, CONSTRAINT [UQ_hlbiattributeconfig_objectdef_attrpath] UNIQUE([objectdefinition], [attributepath])
)
