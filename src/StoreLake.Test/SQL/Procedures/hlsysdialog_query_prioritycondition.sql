-- @Name QueryDialogCheckConditionPriority
-- @Return ClrTypes:boolean Mode:Single
CREATE PROCEDURE [dbo].[hlsysdialog_query_prioritycondition]
AS
BEGIN	
	SELECT CheckConditionPriority = CAST(IIF(dm.dialogmode = 28672, 1, 0) AS BIT)
	FROM 
	(
		SELECT configstream, CAST(configstream AS XML) AS configxml 
		FROM [dbo].[hlsysconfiguration] WHERE type = 32768
	) AS cs
	OUTER APPLY
	(
	    SELECT VIRT.node.value(N'.', N'INT') AS [dialogmode]
	    FROM cs.configxml.nodes(N'declare default element namespace "http://www.helpline.de/xsd/configs.xsd";commonconfig/dialogmode') AS VIRT(node)
	) AS dm
END