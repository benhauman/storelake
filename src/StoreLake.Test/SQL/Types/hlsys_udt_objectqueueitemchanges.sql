CREATE TYPE [dbo].[hlsys_udt_objectqueueitemchanges] AS TABLE
(
    [objectid]            INT NOT NULL
  , [objectdefid]         INT NOT NULL
  , [agentid]             INT NOT NULL
  , [agentdesk]           BIT NOT NULL
  , [waitingqueue]        BIT NOT NULL
  , [infodesk]            BIT NOT NULL
  , [agentdeskchanged]    BIT NOT NULL
  , [waitingqueuechanged] BIT NOT NULL
  , [infodeskchanged]     BIT NOT NULL
  , PRIMARY KEY CLUSTERED ([objectid] ASC, [objectdefid] ASC, [agentid] ASC)
)