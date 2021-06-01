CREATE PROCEDURE [dbo].[hlsysadhocinstanceaction_targetqueue_receive] @timeout INT = 2000 -- miliseconds
AS
BEGIN
	SET NOCOUNT ON

	DECLARE @conversationhandle UNIQUEIDENTIFIER
	DECLARE @messagebody VARBINARY(MAX)
	DECLARE @messagetype SYSNAME

	RAISERROR(N'Waiting for message signal', 0, 1) WITH NOWAIT
	SET @conversationhandle = NULL
	WAITFOR (
		RECEIVE TOP(1) @conversationhandle = [conversation_handle],
					   @messagetype        = [message_type_name],
					   @messagebody        = [message_body]
		FROM [dbo].[hlsysadhocinstanceaction_targetqueue]
	), TIMEOUT @timeout

	IF @conversationhandle IS NOT NULL
	BEGIN
		DECLARE @trace NVARCHAR(256)
		IF @messagetype = N'DEFAULT'
		BEGIN
			-- Do not close the conversation until the invidual request is processed, allowing the response to be synchronized with the client
			--END CONVERSATION @conversationhandle

			SET @trace = CONCAT(N'Application message available (conversationhandle: ', @conversationhandle, N', body: ', CAST(@messagebody AS NVARCHAR(MAX)), N')')
			RAISERROR(@trace, 0, 1) WITH NOWAIT
			SELECT [targetconversationhandle] = @conversationhandle
				 , [requestid]                = CAST(@messagebody AS INT)
		END
		ELSE
		BEGIN
			SET @trace = CONCAT(N'System message available (conversationhandle: ', @conversationhandle, N', type: ', @messagetype, N')')
			RAISERROR(@trace, 0, 1) WITH NOWAIT
		END
	END
	ELSE 
	BEGIN
		RAISERROR(N'No message available', 0, 1) WITH NOWAIT
	END
END