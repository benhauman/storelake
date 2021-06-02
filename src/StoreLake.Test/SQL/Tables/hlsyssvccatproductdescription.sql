CREATE TABLE [dbo].[hlsyssvccatproductdescription]
(
    [productid] UNIQUEIDENTIFIER NOT NULL
  , [lcid]      INT              NOT NULL
  , [value]     NVARCHAR(MAX)    NOT NULL
  , CONSTRAINT [PK_hlsyssvccatproductdescription] PRIMARY KEY ([productid], [lcid])
  , CONSTRAINT [FK_hlsyssvccatproductdescription_product] FOREIGN KEY ([productid]) REFERENCES [dbo].[hlsyssvccatproduct] ([id]) ON DELETE CASCADE
  , CONSTRAINT [FK_hlsyssvccatproductdescription_lcid] FOREIGN KEY ([lcid]) REFERENCES [dbo].[hlsysculturesupported] ([lcid]) ON DELETE CASCADE
)