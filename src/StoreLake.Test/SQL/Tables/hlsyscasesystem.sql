CREATE TABLE [dbo].[hlsyscasesystem]
(
	 [caseid] INT NOT NULL
	,[casedefid] INT NOT NULL
	,[objecttype] INT NOT NULL CONSTRAINT [DF_hlsyscasesystem_objecttype] DEFAULT(2)
	,[controller] NVARCHAR(50) NOT NULL
	,[workflowinstance] UNIQUEIDENTIFIER NULL
	,[workflowdefid] INT NOT NULL
	,[workflowversion] INT NOT NULL
	,[owner] INT NOT NULL
	,[channel] SMALLINT NOT NULL
	--,[channelrefvalue] AS (CAST([channel] AS INT)) PERSISTED NOT NULL
	,[channelrefvalue] INT NOT NULL
	,[referencenumber] NVARCHAR(50) NOT NULL
	,[creationtime] DATETIME NOT NULL
	,[registrationtime] DATETIME NOT NULL
---(+) changeable/denormalized----------------------------------------
	, [version] INT NOT NULL -- initial:1 & incrementented on every update.
	, [lastmodified] DATETIME NOT NULL
	, [internalstate] INT NOT NULL
	, [calendarid] INT NOT NULL
---(-) changeable/denormalized----------------------------------------
	, CONSTRAINT [CK_hlsyscasesystem_objecttype] CHECK([objecttype] = (2))
	, CONSTRAINT [PK_hlsyscasesystem] PRIMARY KEY ([caseid] ASC)
	, CONSTRAINT [CK_hlsyscasesystem_caseid] CHECK ([caseid] > (0))
	, CONSTRAINT [FK_hlsyscasesystem_casedefid] FOREIGN KEY ([casedefid]) REFERENCES [dbo].[hlsyscasedef]([casedefid])
	, CONSTRAINT [FK_hlsyscasesystem_casedefidtype] FOREIGN KEY ([casedefid],[objecttype]) REFERENCES [dbo].[hlsysobjectdef]([objectdefid],[objecttype])
	, CONSTRAINT [UQ_hlsyscasesystem_caseidcasedefid] UNIQUE(caseid, casedefid) -- needed for FK(s) to ensure definition consistensy on magic-system table(s)

	, CONSTRAINT [CK_hlsyscasesystem_workflowinstance] CHECK (([controller] = N'79F2D5E4-0307-44D3-AF55-51D16604C97B' AND [workflowinstance] IS NULL) OR ([controller] <> N'79F2D5E4-0307-44D3-AF55-51D16604C97B' AND [workflowinstance] IS NOT NULL))
	, CONSTRAINT [CK_hlsyscasesystem_workflowdefid]    CHECK (([controller] = N'79F2D5E4-0307-44D3-AF55-51D16604C97B' AND [workflowdefid] = (1001)) OR ([controller] <> N'79F2D5E4-0307-44D3-AF55-51D16604C97B' AND [workflowdefid] > (0)))
	, CONSTRAINT [CK_hlsyscasesystem_workflowversion]  CHECK (([controller] = N'79F2D5E4-0307-44D3-AF55-51D16604C97B' AND [workflowversion] = (1)) OR ([controller] <> N'79F2D5E4-0307-44D3-AF55-51D16604C97B' AND [workflowversion] > (0)))
	, CONSTRAINT [FK_hlsyscasesystem_workflowinstance] FOREIGN KEY ([workflowinstance]) REFERENCES [dbo].[hlwfinstance]([instanceid])
	, CONSTRAINT [FK_hlsyscasesystem_workflowinstance_dv] FOREIGN KEY ([workflowinstance],[workflowdefid],[workflowversion]) REFERENCES [dbo].[hlwfinstance]([instanceid], [rootworkflowid], [version])
	, CONSTRAINT [FK_hlsyscasesystem_internalstate] FOREIGN KEY ([internalstate]) REFERENCES [dbo].[hlsysinternalstate]([listitemid])
	, CONSTRAINT [FK_hlsyscasesystem_calendarid] FOREIGN KEY ([calendarid]) REFERENCES [dbo].[hlsyscalendar]([calendarid]) -- calendar-type:2:servicetime
	, CONSTRAINT [CK_hlsyscasesystem_owner] CHECK ([owner]>=(710))
	, CONSTRAINT [FK_hlsyscasesystem_owner] FOREIGN KEY ([owner]) REFERENCES [dbo].[hlsysagent]([agentid])
	, CONSTRAINT [FK_hlsyscasesystem_channel] FOREIGN KEY ([channel]) REFERENCES [dbo].[hlsyschannel]([channelid])
	, CONSTRAINT [FK_hlsyscasesystem_channelrefvalue] FOREIGN KEY ([channelrefvalue]) REFERENCES [dbo].[hlsyschannel]([channelrefvalue])
	, CONSTRAINT [CK_hlsyscasesystem_referencenumber] CHECK (LEN([referencenumber]) > (8))
	, CONSTRAINT [UQ_hlsyscasesystem_referencenumber] UNIQUE ([referencenumber])
	, CONSTRAINT [UQ_hlsyscasesystem_values_nonchg] UNIQUE ([caseid],[casedefid],[owner],[channelrefvalue],[referencenumber],[controller],[workflowdefid],[workflowversion]) -- #1. <non changeable values here>! #2. No UQ_ index updates are expected! #3. for consistency check (FKs) of all <casesystem> tables only!
	, CONSTRAINT [UQ_hlsyscasesystem_caseidcasedefidwfi] UNIQUE ([caseid],[casedefid],[workflowinstance])
	, CONSTRAINT [UQ_hlsyscasesystem_controller] UNIQUE ([caseid],[controller])
	, CONSTRAINT [UQ_hlsyscasesystem_owner] UNIQUE ([caseid],[owner])
	, CONSTRAINT [UQ_hlsyscasesystem_values_chg] UNIQUE ([caseid],[version],[lastmodified],[internalstate],[calendarid]) -- needed for magic-casesystem-table's FK to ensure data-integrity and syncronization state for changeable-not-nullable columns

	, CONSTRAINT [CK_hlsyscasesystem_creationtime] CHECK(DATEPART(YEAR,[creationtime]) > (1990))
	, CONSTRAINT [CK_hlsyscasesystem_registrationtime] CHECK(DATEPART(YEAR,[registrationtime]) > (1990))
	, CONSTRAINT [CK_hlsyscasesystem_creationregistrationtime] CHECK(DATEPART(YEAR, [creationtime])<(2020) OR (DATEPART(YEAR, [creationtime])>=(2020) AND DATEDIFF(SECOND, [registrationtime], [creationtime])>=(0))) -- registration cannot be after creation : "I want to have an incident tomorrow!". So what is the use case and why there are entries in 2015? .Net DateTime.Utc poroblem?
	, CONSTRAINT [CK_hlsyscasesystem_channelrefvalue] CHECK([channelrefvalue]=[channel])
	, CONSTRAINT [CK_hlsyscasesystem_version] CHECK ([version]>(0))
	, CONSTRAINT [CK_hlsyscasesystem_lastmodified] CHECK([lastmodified] >= [creationtime])
)