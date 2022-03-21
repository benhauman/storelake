CREATE PROCEDURE [dbo].[tc_001_isnull_args1] 
    @agentid         INT
AS
    SELECT x=ISNULL(@agentid);