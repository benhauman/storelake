CREATE TABLE [dbo].[hlsysapprovements]
(
    [approvementid]      UNIQUEIDENTIFIER NOT NULL
  , [workflowinstanceid] UNIQUEIDENTIFIER NOT NULL
  , [approvementcontext] UNIQUEIDENTIFIER NOT NULL
  , [approverdefid]      INT              NOT NULL
  , [approverid]         INT              NOT NULL
  , [subject]            NVARCHAR(512)    NULL
  , [body]               NVARCHAR(MAX)    NOT NULL
  , [createdat]          DATETIME         NOT NULL
  , CONSTRAINT [PK_hlsysapprovements] PRIMARY KEY CLUSTERED ([approvementid] ASC)
  , CONSTRAINT [FK_hlsysapprovements_workflowinstanceid] FOREIGN KEY ([workflowinstanceid]) REFERENCES [dbo].[hlwfinstance] ([instanceid])
  , CONSTRAINT [FK_hlsysapprovements_approverdefid] FOREIGN KEY ([approverdefid]) REFERENCES [dbo].[hlsyspersondef] ([persondefid])
  , INDEX [IX_hlsysapprovements_context] NONCLUSTERED ([approvementcontext])
)
GO
CREATE NONCLUSTERED INDEX [IX_hlsysapprovements_approver] ON [dbo].[hlsysapprovements]([approverdefid], [approverid]) INCLUDE([approvementid], [subject], [createdat]) -- GetApprovals API