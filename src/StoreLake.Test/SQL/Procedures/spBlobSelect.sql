CREATE PROCEDURE [dbo].[spBlobSelect] 
	@id UNIQUEIDENTIFIER 
	AS 
	SET NOCOUNT ON;
	BEGIN
		-- SET NOCOUNT ON added to prevent extra result sets from
		-- interfering with SELECT statements.
		SET NOCOUNT ON;

		SELECT data.PathName() AS Path
			, GET_FILESTREAM_TRANSACTION_CONTEXT() AS TransactionContext 
		FROM [dbo].[Blob] 
		WHERE identifier = @id
	
	END