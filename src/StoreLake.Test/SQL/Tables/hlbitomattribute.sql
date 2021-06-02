CREATE TABLE [dbo].[hlbitomattribute](
	[attributeconfigid] INT IDENTITY(1,1) NOT NULL,
	[hlbiattributeconfigid] INT NOT NULL,
	[objectdefinition] NVARCHAR(255) NOT NULL,
	[attributepath] NVARCHAR(255) NOT NULL,
	[description] NVARCHAR(255) NOT NULL,
	[tablename] NVARCHAR(255) NOT NULL,
	[columnname] NVARCHAR(255) NOT NULL,
	[datatype] NVARCHAR(50) NULL,
	[hashvalue] VARBINARY(8000) NOT NULL,
	[createdon] DATETIME NOT NULL CONSTRAINT [DF_hlbitomattribute_createdon] DEFAULT GETUTCDATE(),
	[modifiedon] DATETIME NOT NULL CONSTRAINT [DF_hlbitomattribute_modifiedon] DEFAULT GETUTCDATE(),
	[deletedon] DATETIME NULL,
	[deleted] BIT NOT NULL CONSTRAINT [DF_hlbitomattribute_deleted] DEFAULT 0,
 CONSTRAINT [PK_hlbitomattribute] PRIMARY KEY CLUSTERED 
(
	[attributeconfigid] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]