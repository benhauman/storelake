CREATE TABLE [dbo].[hlsysconfiguration]
(
	[configid] INT NOT NULL,
	[name] NVARCHAR(255) NULL,
	[type] INT NOT NULL,
	[agentid] INT NOT NULL,
	[roleid] INT NOT NULL,
	[objdefid] INT NOT NULL,
	[groupid] INT NOT NULL CONSTRAINT DF_hlsysconfiguration_groupid DEFAULT ((0)),
	[configstream] NVARCHAR(MAX) NOT NULL,
	[subtype] NVARCHAR(255) NULL,
	[condition] NVARCHAR(MAX) NULL,
	[description] NVARCHAR(2000) NULL,
	[lastmodified] DATETIME NOT NULL CONSTRAINT DF_hlsysconfiguration_lastmodified DEFAULT (GETUTCDATE()),
	[searchid] INT NOT NULL CONSTRAINT DF_hlsysconfiguration_searchid DEFAULT ((0))
	,CONSTRAINT [PK_hlsysconfiguration] PRIMARY KEY CLUSTERED 
	(
		[configid] ASC
	)
	, CONSTRAINT [CK_hlsysconfiguration_searchid] CHECK ([searchid]=(0) OR ([searchid]<>(0) AND [objdefid]>(0)))
	, CONSTRAINT [CK_hlsysconfiguration_name] CHECK ([name] IS NULL OR [name]=N'' OR [name]=N'userconfig' OR [name]=N'transfer' OR [name]=N'slct' OR [name]=N'rslt' OR [name]=N'preview' OR [name]=N'frame01' OR [name]=N'default' OR [name]=N'view')
	, CONSTRAINT [CK_hlsysconfiguration_agentid] CHECK (ISNULL([agentid],(0))>=(0) AND NOT (ISNULL([agentid],(0))>=(1500) AND ISNULL([agentid],(0))<=(2000)))
	, CONSTRAINT [CK_hlsysconfiguration_condition] CHECK (LEN(ISNULL([condition],N''))=(0))
	, CONSTRAINT [CK_hlsysconfiguration_groupid] CHECK (ISNULL([groupid],(0))=(0)) -- @lubo what is the usecase for this column? if known -> change it to nullable with FK.
	, CONSTRAINT [CK_hlsysconfiguration_configstream_xml] CHECK(CONVERT([xml],[configstream]) IS NOT NULL)
	, CONSTRAINT [UQ_hlsysconfiguration_ntaross] UNIQUE ([name],[type],[agentid],[roleid],[objdefid],[subtype],[searchid])
	, CONSTRAINT [CK_hlsysconfiguration_agentrole] CHECK (([roleid]=0 AND [agentid]=(0)) OR ([roleid]>(0) AND [agentid]=(0)) OR ([roleid]=(0) AND [agentid]>(0)))
	, CONSTRAINT [CK_hlsysconfiguration_cfgtype] CHECK 
	(
-- see: Helpline.Server.Repository.Configuration.ConfigType in $\Source\Server\Server.Repository\Server.Repository.Administration\GenericConfigurationService\ConfigType.cs

---  --
          [type]=0x00000001 --  N'control - Sub_Table', '')
     --OR [type]=0x00000002 --  N'control - Sub_Folder', '')
       OR [type]=0x00000004 --  N'control - Sub_Bar', '')
       OR [type]=0x00000008 --  N'control - Sub_Preview', '')
       OR [type]=0x00000010 --  N'control - Sub_Columns', '')
---  --			 -- 
	   OR [type]=0x00000020 --  N'OutlookView','')
	   OR [type]=0x00000040 --  N'Graph', N'')
	 --OR [type]=0x00000080 --  N'Personal', N'')
---  --			 -- 
-- ERR:OR [type]=0x00000100 --  N'DialogObject', N'')
	   OR [type]=0x00000200 --  N'Script_GUI', N'')
	   OR [type]=0x00000400 --  N'Configs', N'global customization')
	   OR [type]=0x00000800 --  N'Relators', 'global relators')
---  --			 -- 
-- ERR:OR [type]=0x00001000 --  N'Script_Web',N'')     -- 4096
-- ERR:OR [type]=0x00002000 --  N'DialogSearch', N'')  -- 512
-- ERR:OR [type]=0x00003000 --  N'DialogContact', N'') -- 12288
	   OR [type]=0x00004000 --  N'Transfer', 'data transfer configuration')
       OR [type]=0x00008000 --  N'Common', 'general customization')
---  --			 -- 
	   OR [type]=0x00010000 --  N'WebTable', N'')
	   OR [type]=0x00020000 --  N'WebPreview', N'')
	   OR [type]=0x00040000 --  N'CMDB','')
-- ??? OR [type]=0x00080000 --  N'', N'')
---  --			 -- 
	 --OR [type]=0x00100000 --  N'SimpleTableView', '')
	   OR [type]=0x00200000 --  N'RibbonConfig','')
	   OR [type]=0x00400000 --  N'ClassicDeskGlobal', N'')
	   OR [type]=0x00800000 --  N'AdHocReportParams', N'')
---  --			 -- 
	   OR [type]=0x01000000 --  N'BICenter','')
--	   OR [type]=0x02000000 --  N'RecentObjects', N'')
--	   OR [type]=0x04000000 --  N'TimelineChart','')
	   OR [type]=0x08000000 --  N'Startpage', N'')
---  --			 -- 
	   OR [type]=0x10000000 --  N'GlobalClient', '')
---  --
	)
)
GO

CREATE NONCLUSTERED INDEX [IX_hlsysconfiguration_tarod] ON [dbo].[hlsysconfiguration] ([type],[agentid],[roleid],[objdefid]) INCLUDE ([lastmodified])
GO