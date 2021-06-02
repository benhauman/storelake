CREATE TABLE [dbo].[hlsyssvccatcategory] 
(
    [id]                     UNIQUEIDENTIFIER NOT NULL
  , [name]                   NVARCHAR (255)   NOT NULL
  , [displaynameid]          INT              NOT NULL
  , [sortorder]              INT              NOT NULL
  , [parentcategoryid]       UNIQUEIDENTIFIER NULL
  , [validfrom]              DATETIME         NULL
  , [validto]                DATETIME         NULL
  , [image]                  VARBINARY(MAX)   NULL
  , CONSTRAINT [PK_hlsyssvccatcategory] PRIMARY KEY CLUSTERED ([id] ASC)
  , CONSTRAINT [FK_hlsyssvccatcategory_parent] FOREIGN KEY ([parentcategoryid]) REFERENCES [dbo].[hlsyssvccatcategory]([id])
  , CONSTRAINT [CK_hlsyssvccatcategory_validrange] CHECK ([validfrom] IS NULL OR [validto] IS NULL OR [validfrom] < [validto])
  , CONSTRAINT [UQ_hlsyssvccatcategory_name] UNIQUE ([name])
  , CONSTRAINT [CK_hlsyssvccatcategory_name] CHECK ([name] LIKE N'[A-Z_]' + REPLICATE(N'[A-Z0-9_]', LEN([name]) - 1)
                                                 OR [name] = CAST([id] AS NVARCHAR(255))) -- Since the name will probably be deprecated, we use the id for now
)
GO
CREATE NONCLUSTERED INDEX [IX_hlsyssvccatcategory_parentcategoryid] ON [dbo].[hlsyssvccatcategory] ([parentcategoryid])