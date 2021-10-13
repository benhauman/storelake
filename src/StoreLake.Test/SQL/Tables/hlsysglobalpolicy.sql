CREATE TABLE [dbo].[hlsysglobalpolicy]
(
	[globalid] INT NOT NULL CONSTRAINT [PK_hlsysglobalpolicy] PRIMARY KEY CLUSTERED,
	[description] NVARCHAR(50) NOT NULL,
	[accessmask] SMALLINT NOT NULL,

	CONSTRAINT CK_hlsysglobalpolicy_globalid CHECK([globalid] <> 839 /*GLOBAL_PM_PROJECT_EXPLORER*/)
)