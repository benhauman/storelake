CREATE TABLE [dbo].[hlsysassociationdef]
(
	[associationdefid] INT NOT NULL,
	[name] NVARCHAR(255) NOT NULL,
	CONSTRAINT [PK_hlsysassociationdef] PRIMARY KEY CLUSTERED 
	(
		[associationdefid] ASC
	)
	,CONSTRAINT [UQ_hlsysassociationdef_name] UNIQUE ([name])
	,CONSTRAINT [CK_hlsysassociationdef_associationdefid] CHECK ([associationdefid] > 0)
	,CONSTRAINT [UQ_hlsysassociationdef_defidname] UNIQUE ([associationdefid],[name]) -- FOR FKs
)