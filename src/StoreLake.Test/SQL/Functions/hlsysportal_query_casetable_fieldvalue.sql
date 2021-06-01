CREATE FUNCTION [dbo].[hlsysportal_query_casetable_fieldvalue](@sectionid TINYINT, @caseid INT, @casedefid INT, @lcid INT)
RETURNS TABLE AS 
RETURN
    SELECT CAST([x].[a] AS TINYINT)       AS [field_position]
		 , CAST([x].[a] AS INT)           AS [field_attrpathid]
         , CAST([x].[a] AS TINYINT)       AS [field_value_type]   -- 1:text, 2:int,3:listimageid,4:displaynameid(hlsysdisplayname), 5:agentid(hlsysagent.name)
		 , CAST([x].[a] AS TINYINT)       AS [field_display_kind] -- 0:Default | 1:ListItem | 2:TreeItem | 3:Agent | 4:ObjectDefinition
		 , CAST([x].[a] AS TINYINT)       AS [field_display_type] -- // see 'hlsysportalcfgcasetablefield(displaytype)' -- 1: SmallText | 2: LargeText | 3: Icon

		 , CAST([x].[a] AS NVARCHAR(400)) AS [value_nvarchar]
		 , CAST([x].[a] AS INT)           AS [value_int]
		 , CAST([x].[a] AS BIT)           AS [value_bit]
		 , CAST([x].[a] AS DATETIME)      AS [value_datetime]
		 , CAST([x].[a] AS DECIMAL(11,2)) AS [value_decimal]
	FROM (VALUES(@sectionid, @caseid, @casedefid, @lcid, CAST(N'### DB ARTIFACTS WERE NOT REBUILT AFTER DEPLOY! ###' AS BIT))) AS [x]([sectionid], [caseid], [casedefid], [lcid], [a]) -- use Server.RebuildModelApp.exe
