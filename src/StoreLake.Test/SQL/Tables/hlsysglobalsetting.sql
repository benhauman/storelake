CREATE TABLE [dbo].[hlsysglobalsetting](
	[settingid] SMALLINT NOT NULL,
	[settingvalue] NVARCHAR(255) NULL,
	[description] NVARCHAR(255) NULL,
 CONSTRAINT [PK_hlsysglobalsetting] PRIMARY KEY CLUSTERED 
(
	[settingid] ASC
)
) 