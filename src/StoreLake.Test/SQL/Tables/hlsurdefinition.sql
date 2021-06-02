CREATE TABLE [dbo].[hlsurdefinition]
(
	[surveydefid] INT NOT NULL IDENTITY(1,1), 
    [name] NVARCHAR(128) NOT NULL,
	[processname] NVARCHAR(500) NOT NULL,
	[typeid] INT NOT NULL,
	[validityweeks] INT NULL,
	CONSTRAINT [PK_hlsurdefinition] PRIMARY KEY ([surveydefid]),
	CONSTRAINT [FK_hlsurdefinition_typeid] FOREIGN KEY ([typeid]) REFERENCES [dbo].[hlsurtype]([typeid]),
	CONSTRAINT [UQ_hlsurdefinition_definition] UNIQUE ([name], [processname], [typeid])
)
