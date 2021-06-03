CREATE TABLE [dbo].[hlspdefinition]
(
	[spdefinitionid] INT NOT NULL IDENTITY(1,1),
	[definitionname] NVARCHAR(250) NOT NULL, 
	[processversion] NVARCHAR(50) NOT NULL,
	CONSTRAINT [PK_hlspdefinition] PRIMARY KEY (spdefinitionid ASC),
	CONSTRAINT [UQ_hlspdefinition_definition] UNIQUE NONCLUSTERED ([definitionname], [processversion])
)
