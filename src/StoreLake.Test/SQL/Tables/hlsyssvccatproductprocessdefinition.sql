CREATE TABLE [dbo].[hlsyssvccatproductprocessdefinition] 
(
    [productid]            UNIQUEIDENTIFIER NOT NULL
  , [producttype]          TINYINT          NOT NULL
  , [allowaddtocart]       BIT              NOT NULL
  , [processdefid]         INT              NOT NULL 
  , [showactivationdialog] BIT              NOT NULL 
  , CONSTRAINT [PK_hlsyssvccatproductprocessdefinition] PRIMARY KEY CLUSTERED ([productid])
  , CONSTRAINT [FK_hlsyssvccatproductprocessdefinition_product] FOREIGN KEY ([productid], [producttype], [allowaddtocart]) REFERENCES [dbo].[hlsyssvccatproduct] ([id], [type], [allowaddtocart]) ON DELETE CASCADE
  , CONSTRAINT [CK_hlsyssvccatproductprocessdefinition_type] CHECK ([producttype] = 3) -- Service
  , CONSTRAINT [CK_hlsyssvccatproductprocessdefinition_dialog] CHECK ([allowaddtocart] = 0 OR [showactivationdialog] = 1) -- An activation dialog is required when processes are added to cart
--, CONSTRAINT [FK_hlsyssvccatproductprocessdefinition_wf] FOREIGN KEY ([processdefid]) REFERENCES [dbo].[hlwfworkflowdefinition] ([rootworkflowid])
)