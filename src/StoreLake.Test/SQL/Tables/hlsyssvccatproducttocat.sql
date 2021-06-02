CREATE TABLE [dbo].[hlsyssvccatproducttocat]
(
    [products_id]   UNIQUEIDENTIFIER NOT NULL
  , [categories_id] UNIQUEIDENTIFIER NOT NULL
  , CONSTRAINT [PK_hlsyssvccatproducttocat] PRIMARY KEY NONCLUSTERED ([products_id] ASC, [categories_id] ASC)
  , CONSTRAINT [FK_hlsyssvccatproducttocat_product] FOREIGN KEY ([products_id]) REFERENCES [dbo].[hlsyssvccatproduct] ([id]) ON DELETE CASCADE
  , CONSTRAINT [FK_hlsyssvccatproducttocat_category] FOREIGN KEY ([categories_id]) REFERENCES [dbo].[hlsyssvccatcategory] ([id]) ON DELETE CASCADE
)
GO
CREATE NONCLUSTERED INDEX [IX_hlsyssvccatproducttocat_category] ON [dbo].[hlsyssvccatproducttocat]([categories_id]) INCLUDE ([products_id])