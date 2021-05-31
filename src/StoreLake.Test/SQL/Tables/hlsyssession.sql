CREATE TABLE [dbo].[hlsyssession]
(
    [sessionkey]   INT NOT NULL IDENTITY(1,1)
  , [sessionid]    UNIQUEIDENTIFIER NOT NULL
  , [agentid]      INT              NOT NULL
  , [objectid]     INT              NOT NULL
  , [objectdefid]  INT              NOT NULL
  , [productid]    SMALLINT         NOT NULL
  , [version]      SMALLINT         NOT NULL
  , [logontime]    DATETIME         NOT NULL
  , [lcid]         INT              NOT NULL
  , [timezone]     INT              NOT NULL
  , [seatname]     NVARCHAR(513)    NOT NULL
  , [testmode]     BIT              NOT NULL
  , [lastvisit]	   DATETIME		    NOT NULL
  , [portalid]     TINYINT          NULL
  , CONSTRAINT [PK_hlsyssession] PRIMARY KEY CLUSTERED ([sessionkey])
  , CONSTRAINT [UQ_hlsyssession_sessionid] UNIQUE ([sessionid])
  , CONSTRAINT [CK_hlsyssession_sessionkey] CHECK ([sessionkey] > (0))
  , CONSTRAINT [FK_hlsyssession_lcid] FOREIGN KEY ([lcid]) REFERENCES [dbo].[hlsysculturesupported] ([lcid])
  , CONSTRAINT [FK_hlsyssession_portalid] FOREIGN KEY ([portalid]) REFERENCES [dbo].[hlsysportalapplication] ([portalid])
  , CONSTRAINT [FK_hlsyssession_productid] FOREIGN KEY ([productid]) REFERENCES [dbo].[hlsyslicclient] ([hlclient])
  , CONSTRAINT [CK_hlsyssession_productid] CHECK ([productid]>(0) AND NOT ([productid]=(18) OR [productid]=(6) OR [productid]=(4))) -- 4:DASHBOARD, 6:TELEPHONY, 18:PortalWebShop are addon session only
  , CONSTRAINT [CK_hlsyssession_version] CHECK ([version]=(7000) OR ([productid]=(1) AND [version]=(7000))) -- Multiple ClassicDesk versions are supported
  , CONSTRAINT [CK_hlsyssession_objectid] CHECK (([productid]<>3) OR ([productid] = 3 AND [objectid] > 0)) -- 13:PortalUser - only allowed with persons
  , CONSTRAINT [CK_hlsyssession_objectdefid] CHECK (([productid]<>3) OR ([productid] = 3 AND [objectdefid] > 0)) -- 13:PortalUser - only allowed with persons
  , CONSTRAINT [CK_hlsyssession_portalid] CHECK (([productid] = 3 OR [productid] = 13) AND ISNULL([portalid], 0) > 0 -- 3:Portal or 13:PortalAdmin => portalid required
			                               OR NOT([productid] = 3 OR [productid] = 13) AND [portalid] IS NULL)
  , CONSTRAINT [CK_hlsyssession_lastvisit] CHECK (DATEPART(year, [lastvisit]) >= 2020)
  , CONSTRAINT [FK_hlsyssession_timezone] FOREIGN KEY ([timezone]) REFERENCES [dbo].[hlsystimezone]([hltzid])
  , CONSTRAINT [FK_hlsyssession_agentid] FOREIGN KEY ([agentid]) REFERENCES [dbo].[hlsysagent]([agentid])
)
GO
CREATE NONCLUSTERED INDEX [IX_hlsyssession_agntprd] ON [dbo].[hlsyssession]([agentid],[productid]) INCLUDE([sessionid]) -- used for agent notification
GO
CREATE NONCLUSTERED INDEX [IX_hlsyssession_agentid] ON [dbo].[hlsyssession]([agentid]) INCLUDE([sessionid],[productid]) -- used for agent is online
GO
CREATE NONCLUSTERED INDEX [IX_hlsyssession_productid] ON [dbo].[hlsyssession]([productid]) INCLUDE([sessionid],[agentid]) -- used for agent notification
GO
CREATE NONCLUSTERED INDEX [IX_hlsyssession_productobjectiddefid] ON [dbo].[hlsyssession] (productid, objectid, objectdefid) --used for portal user count