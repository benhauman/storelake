CREATE TABLE [dbo].[hlsystemplatedefinition](
	[id] INT NOT NULL,
	[name] NVARCHAR(255) NOT NULL,
	[description] NVARCHAR(255) NULL,
	[sortorder] INT NOT NULL,
	[parentid] INT NOT NULL,
	[agentid] INT NOT NULL,
	[objectdefname] NVARCHAR(255) NOT NULL,
	[instructions] NVARCHAR(MAX) NOT NULL,
 CONSTRAINT [PK_hlsystemplatedefinition] PRIMARY KEY CLUSTERED 
(
	[id] ASC
)
)