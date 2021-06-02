CREATE TABLE [dbo].[hlsyssvccatcategorydescription]
(
    [categoryid] UNIQUEIDENTIFIER NOT NULL
  , [lcid]       INT              NOT NULL
  , [value]      NVARCHAR(MAX)    NOT NULL
  , CONSTRAINT [PK_hlsyssvccatcategorydescription] PRIMARY KEY ([categoryid], [lcid])
  , CONSTRAINT [FK_hlsyssvccatcategorydescription_category] FOREIGN KEY ([categoryid]) REFERENCES [dbo].[hlsyssvccatcategory] ([id]) ON DELETE CASCADE
  , CONSTRAINT [FK_hlsyssvccatcategorydescription_lcid] FOREIGN KEY ([lcid]) REFERENCES [dbo].[hlsysculturesupported] ([lcid]) ON DELETE CASCADE
)