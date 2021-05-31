CREATE TABLE [dbo].[hlsysimage]
(
	[imageid]      INT     NOT NULL
  , [imagetype]    TINYINT NOT NULL -- 1: Core | 2: Custom
  , [imageindex]   INT     NULL
  , CONSTRAINT [PK_hlsysimage] PRIMARY KEY ([imageid] ASC)
  , CONSTRAINT [CK_hlsysimage_type] CHECK ([imagetype] = 1 OR [imagetype] = 2)
  , CONSTRAINT [CK_hlsysimage_index] CHECK ([imagetype] = 1 AND [imageindex] IS NULL OR [imagetype] = 2 AND [imageindex] IS NOT NULL)
)
GO
-- Only customer images have an image index (helpLine Designer)
CREATE UNIQUE NONCLUSTERED INDEX [UQ_hlsysimage_index] ON [dbo].[hlsysimage] ([imageindex]) WHERE [imagetype] = 2