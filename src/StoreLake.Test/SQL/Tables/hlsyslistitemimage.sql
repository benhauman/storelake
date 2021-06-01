CREATE TABLE [dbo].[hlsyslistitemimage]
(
	[listitemid] INT      NOT NULL
  , [imageid]    INT      NOT NULL
  , CONSTRAINT [PK_hlsyslistitemimage] PRIMARY KEY ([listitemid] ASC)
  , CONSTRAINT [FK_hlsyslistitemimage_image] FOREIGN KEY ([imageid]) REFERENCES [dbo].[hlsysimage] ([imageid]) ON DELETE CASCADE
  , CONSTRAINT [FK_hlsyslistitemimage_item] FOREIGN KEY ([listitemid]) REFERENCES [dbo].[hlsyslistitem] ([listitemid]) ON DELETE CASCADE
)
