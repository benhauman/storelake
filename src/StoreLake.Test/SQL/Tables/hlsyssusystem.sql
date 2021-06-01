CREATE TABLE [dbo].[hlsyssusystem]
(
	[suid] INT NOT NULL,
	[sudefid] INT NOT NULL,
	[suindex] INT NOT NULL,
	[caseid] INT NOT NULL,
	[channel] INT NOT NULL,
	[editor] INT NOT NULL,
	[creationtime] DATETIME NOT NULL,
	[creationendtime] DATETIME NOT NULL,
	[registrationtime] DATETIME NOT NULL,
	[registrationendtime] DATETIME NOT NULL
---(+) changeable/denormalized----------------------------------------
	, [closed] BIT NOT NULL
	, [durationtime] INT NOT NULL
	, [published] BIT NOT NULL
	, [lastmodified] DATETIME NOT NULL
	, [susyncseq] INT NOT NULL -- initial:1 & incrementented on every update(similar to lastmodified and case-version).
---(-) changeable/denormalized----------------------------------------

	, CONSTRAINT [PK_hlsyssusystem] PRIMARY KEY ([suid])
	, CONSTRAINT [UQ_hlsyssusystem_caseid_suindex] UNIQUE ([caseid],[suindex])
	, CONSTRAINT [UQ_hlsyssusystem_suid_caseid_sudefid] UNIQUE ([suid], [caseid], [sudefid])
	, CONSTRAINT [CK_hlsyssusystem_suid] CHECK ([suid]>(0))
	, CONSTRAINT [CK_hlsyssusystem_suindex] CHECK ([suindex]>(0))
	, CONSTRAINT [FK_hlsyssusystem_case] FOREIGN KEY ([caseid],[sudefid]) REFERENCES [dbo].[hlsyscasesystem]([caseid],[casedefid])
	, CONSTRAINT [FK_hlsyssusystem_channel] FOREIGN KEY ([channel]) REFERENCES [dbo].[hlsyschannel]([channelrefvalue])
	, CONSTRAINT [CK_hlsyssusystem_creationtime] CHECK(DATEPART(YEAR,[creationtime]) > (1990))
	, CONSTRAINT [CK_hlsyssusystem_creationendtime] CHECK(DATEPART(YEAR,[creationendtime]) > (1990))
	, CONSTRAINT [CK_hlsyssusystem_registrationtime] CHECK(DATEPART(YEAR,[registrationtime]) > (1990))
	, CONSTRAINT [CK_hlsyssusystem_registrationendtime] CHECK(DATEPART(YEAR,[registrationendtime]) > (1990))
	, CONSTRAINT [FK_hlsyssusystem_editor] FOREIGN KEY ([editor]) REFERENCES [dbo].[hlsysagent]([agentid])
	, CONSTRAINT [UQ_hlsyssusystem_values_nonchg] UNIQUE ([suid],[sudefid],[suindex],[caseid],[channel],[editor],[creationtime],[creationendtime],[registrationtime],[registrationendtime])
	, CONSTRAINT [UQ_hlsyssusystem_values_chg] UNIQUE ([suid],[lastmodified],[closed],[published],[durationtime],[susyncseq]) -- needed for magic-susystem-table's FK to ensure data-integrity and syncronization state
	, CONSTRAINT [CK_hlsyssusystem_durationtime] CHECK([durationtime]>=(0))
	, CONSTRAINT [CK_hlsyssusystem_susyncseq] CHECK([susyncseq]>(0))
	, INDEX [IX_hlsyssusystem_sudefidcaseid]([sudefid], [caseid])-- usecase : DELETE case & [FK_hlsyssusystem_case]
)

