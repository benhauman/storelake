	CREATE TABLE [dbo].[hlsystemplategroup](
		[id] INT NOT NULL,
		[name] NVARCHAR(255) NOT NULL,
		[sortorder] INT NOT NULL,
		[parentid] INT NOT NULL,
		[agentid] INT NOT NULL,
		[objectdefname] NVARCHAR(255) NOT NULL,
	 CONSTRAINT [PK_hlsystemplategroup] PRIMARY KEY CLUSTERED 
	(
		[id] ASC
	)
	)