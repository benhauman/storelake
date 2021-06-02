CREATE TABLE [dbo].[hlsyssvccatproduct] 
(
    [id]                      UNIQUEIDENTIFIER NOT NULL
  , [type]                    TINYINT          NOT NULL -- 1: Asset | 2: Bundle | 3: Service | 100: SLM (65 evaluation for deprecation)
  , [name]                    NVARCHAR(255)    NOT NULL
  , [displaynameid]           INT              NOT NULL
  , [price]                   DECIMAL(11,2)    NULL
  , [currency]                NVARCHAR(255)    NULL
  , [brand]                   NVARCHAR(255)    NULL
  , [thumbnail]               VARBINARY(MAX)   NULL
  , [productcode]             NVARCHAR(255)    NULL
  , [serviceid]               INT              NULL
  , [slaid]                   INT              NULL
  , [validfrom]               DATETIME         NULL
  , [validto]                 DATETIME         NULL
  , [objectdefid]             INT              NULL
  , [tags]                    NVARCHAR(255)    NULL
  , [listattributepath]       NVARCHAR(400)    NULL
  , [listattributevalue]      INT              NULL
  , [hideprice]               BIT              NULL
  , [singleorderitem]         BIT              NOT NULL
  , [allowaddtocart]          BIT              NOT NULL
  , [customfeatures]          BIT              NULL
  , [creationtime]            DATETIME         NOT NULL
  , [tasktemplatekey]         NVARCHAR(255)    NULL
  , [subprocessdefinitionkey] NVARCHAR(255)    NULL
  , CONSTRAINT [PK_hlsyssvccatproduct] PRIMARY KEY CLUSTERED ([id] ASC)
  , CONSTRAINT [FK_hlsyssvccatproduct_objectdefid] FOREIGN KEY ([objectdefid]) REFERENCES [dbo].[hlsysproductdef]([productdefid])
  , CONSTRAINT [FK_hlsyssvccatproduct_listattributepath] FOREIGN KEY ([listattributepath]) REFERENCES [dbo].[hlsysattrpathodedef]([attrpath_text])
  , CONSTRAINT [FK_hlsyssvccatproduct_listattributevalue] FOREIGN KEY ([listattributevalue]) REFERENCES [dbo].[hlsyslistitem]([listitemid])
  , CONSTRAINT [FK_hlsyssvccatproduct_serviceid] FOREIGN KEY ([serviceid]) REFERENCES [dbo].[hlsysslmservice]([id])
  , CONSTRAINT [FK_hlsyssvccatproduct_slaid] FOREIGN KEY ([slaid]) REFERENCES [dbo].[hlsysslmagreement]([id])
  , CONSTRAINT [UQ_hlsyssvccatproduct_name] UNIQUE ([name])
  , CONSTRAINT [UQ_hlsyssvccatproduct_type] UNIQUE ([id], [type]) -- hlsyssvccatdocument, hlsyssvccatfeature, hlsyssvccatproductparent
  , CONSTRAINT [UQ_hlsyssvccatproduct_service] UNIQUE ([id], [type], [allowaddtocart]) -- hlsyssvccatproductprocessdefinition
  , CONSTRAINT [UQ_hlsyssvccatproduct_cart] UNIQUE ([id], [singleorderitem], [allowaddtocart]) -- hlsyssvccatcartitem
  , CONSTRAINT [UQ_hlsyssvccatproduct_order] UNIQUE ([id], [type], [singleorderitem], [allowaddtocart]) -- hlsyssvccatorderitem
  , CONSTRAINT [CK_hlsyssvccatproduct_name] CHECK ([name] LIKE N'[A-Z_]' + REPLICATE(N'[A-Z0-9_]', LEN([name]) - 1)
                                                OR [name] = CAST([id] AS NVARCHAR(255))) -- Since the name will probably be deprecated, we use the id for now
  , CONSTRAINT [CK_hlsyssvccatproduct_type] CHECK ([type] = 1 OR [type] = 2 OR [type] = 3 OR [type] = 100)
  , CONSTRAINT [CK_hlsyssvccatproduct_price] CHECK ([type]  = 2 AND [price] IS NULL     AND [currency] IS     NULL
                                                 OR [type] <> 2 AND [price] IS NOT NULL AND [currency] IS NOT NULL) -- Bundles don't have a price/currency
  , CONSTRAINT [CK_hlsyssvccatproduct_validrange] CHECK ([validfrom] IS NULL OR [validto] IS NULL OR [validfrom] < [validto])
  , CONSTRAINT [CK_hlsyssvccatproduct_slm] CHECK ([type] <> 100 AND [serviceid] IS NULL AND [slaid] IS NULL
                                               OR [type]  = 100 AND [brand] IS NULL
											                    AND [serviceid] IS NOT NULL
											                    AND [slaid] IS NOT NULL
											                    AND [objectdefid] IS NULL
											                    AND [listattributepath] IS NULL
											                    AND [listattributevalue] IS NULL)
  , CONSTRAINT [CK_hlsyssvccatproduct_hideprice] CHECK ([type] IN (2, 100)     AND [hideprice] IS     NULL -- Setting is irrelevant for bundles/SLM
                                                     OR [type] NOT IN (2, 100) AND [hideprice] IS NOT NULL)
  , CONSTRAINT [CK_hlsyssvccatproduct_singleorderitem] CHECK ([type]     IN (3, 100) AND [singleorderitem] = 1 -- Quantity cannot be changed for service/SLM
                                                           OR [type] NOT IN (3, 100))
  , CONSTRAINT [CK_hlsyssvccatproduct_allowaddtocart] CHECK ([type]  = 3                           -- AllowAddToCart is only configurable for Service
                                                          OR [type] <> 3 AND [allowaddtocart] = 1)
  , CONSTRAINT [CK_hlsyssvccatproduct_customfeatures] CHECK ([type]     IN (3, 100) AND [customfeatures] IS     NULL -- Setting is irrelevant for service/SLM
                                                          OR [type] NOT IN (3, 100) AND [customfeatures] IS NOT NULL)
)