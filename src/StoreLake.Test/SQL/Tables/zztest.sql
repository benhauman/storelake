CREATE TABLE [dbo].[zztest](
	 [id] INT NOT NULL
	,[thumbnail] NVARCHAR(MAX) NULL
	--,[thumbnailhash]           AS               IIF([thumbnail] IS NOT NULL, LOWER(CONVERT(NCHAR(64), HASHBYTES(N'SHA2_256', CONCAT([id], [thumbnail])), 2)), NULL) PERSISTED
	,[committimestampa] DATETIME         NOT NULL CONSTRAINT [DF_zztes_committimestampa] DEFAULT (DATEADD(SECOND, 0, 0))
	,[committimestampb] DATETIME         NOT NULL CONSTRAINT [DF_zztes_committimestampb] DEFAULT (0)
	,[longidone] BIGINT DEFAULT(1)
	,[longidzr] BIGINT DEFAULT(0)
) 
