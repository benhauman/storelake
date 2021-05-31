CREATE TABLE [dbo].[hlwfdefrights](
	[rootworkflowid] INT NOT NULL,
	[version] INT NOT NULL,
	[objectid] INT NOT NULL,
	[accessmask] INT NOT NULL,
 CONSTRAINT [PK_hlwfdefrights] PRIMARY KEY CLUSTERED 
 (
	[rootworkflowid] ASC,
	[version] ASC,
	[objectid] ASC
 ),
  CONSTRAINT [FK_hlwfdefrights_wfdef] FOREIGN KEY([rootworkflowid], [version]) REFERENCES [dbo].[hlwfworkflowdefinition] ([rootworkflowid], [version]) ON DELETE CASCADE
) 