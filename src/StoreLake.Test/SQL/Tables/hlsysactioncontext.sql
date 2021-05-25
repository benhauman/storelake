CREATE TABLE [dbo].[hlsysactioncontext]
(
    [actioncontextid]       BIGINT        NOT NULL
  , [parentactioncontextid] BIGINT        NULL
  , [actionid]              BIGINT        NOT NULL
  , [spid]                  INT           NOT NULL
  , [actionagentid]         INT           NOT NULL
  , [actionchannel]         SMALLINT      NOT NULL
  , [actionlcid]            INT           NOT NULL CONSTRAINT [DF_hlsysactioncontext_actionlcid] DEFAULT 127 /* Invariant */
  , [actiontzid]            INT           NOT NULL CONSTRAINT [DF_hlsysactioncontext_actiontzid] DEFAULT 111 /* W. Europe Standard Time */
  , [hostname]              NVARCHAR(100) NOT NULL
  , [creationtime]          DATETIME      NOT NULL
  , CONSTRAINT [PK_hlsysactioncontext] PRIMARY KEY ([actioncontextid] ASC)
  , CONSTRAINT [FK_hlsysactioncontext_parentactioncontext] FOREIGN KEY ([parentactioncontextid]) REFERENCES [dbo].[hlsysactioncontext]([actioncontextid])
  , CONSTRAINT [FK_hlsysactioncontext_actionid] FOREIGN KEY ([actionid]) REFERENCES [dbo].[hlsysaction]([actionid])
  , CONSTRAINT [FK_hlsysactioncontext_actionagentid] FOREIGN KEY ([actionagentid]) REFERENCES [dbo].[hlsysagent]([agentid])
  , CONSTRAINT [FK_hlsysactioncontext_actionchannel] FOREIGN KEY ([actionchannel]) REFERENCES [dbo].[hlsyschannel]([channelid])
  , CONSTRAINT [FK_hlsysactioncontext_actionlcid] FOREIGN KEY ([actionlcid]) REFERENCES [dbo].[hlsyscultureinfo]([lcid])
  , CONSTRAINT [FK_hlsysactioncontext_actiontzid] FOREIGN KEY ([actiontzid]) REFERENCES [dbo].[hlsystimezone]([hltzid])
  , CONSTRAINT [CK_hlsysactioncontext_actionchannel] CHECK ([actionchannel]>=(209) AND [actionchannel]<=(222))
  , CONSTRAINT [UQ_hlsysactioncontext_actionagentid] UNIQUE ([actioncontextid], [actionagentid])
)
