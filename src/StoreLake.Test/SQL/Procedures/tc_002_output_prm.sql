CREATE PROCEDURE [dbo].[tc_002_output_prm] 
	  @instanceid           UNIQUEIDENTIFIER
    , @actioncontextid      BIGINT
    , @casedefid            INT              NULL
    , @caseid               INT              NULL
    , @doextend             BIT              --NOT NULL -- [doextend]:1:true => [caseid] is required, [doextend]:0:false:create => a new workflow instance must be created and activated
    , @mailrequestkey       INT              NULL -- see [hlsysinqueuepending]([requestkey]) -- this mailrequest can be retried by different workflows/versions/instances => before insert the previos registration must be taken into account (drop/create).
    , @mailrequestid        NVARCHAR(255)    NULL
	, @actionrequestid       INT OUTPUT
AS
    SET @actionrequestid = NULL