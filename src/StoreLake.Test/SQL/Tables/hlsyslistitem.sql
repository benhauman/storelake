CREATE TABLE [dbo].[hlsyslistitem]
(
	[listitemid] INT		   NOT NULL
  , [name]		 NVARCHAR(255) NOT NULL
  , [state]		 SMALLINT	   NOT NULL
  , CONSTRAINT [PK_hlsyslistitem] PRIMARY KEY CLUSTERED ([listitemid] ASC)
  , CONSTRAINT [CK_hlsyslistitem_state] CHECK ([state]=(0) OR [state]=(1)) -- 1:frozen
  , CONSTRAINT [CK_hlsyslistitem_name] CHECK (LEN([name])>(0))
)
GO

CREATE NONCLUSTERED INDEX [IX_hlsyslistitem_name] ON [dbo].[hlsyslistitem]([name])