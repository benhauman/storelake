-- returns 8 columns (0:int,              1:int,              2:string?,                 3:int?,                  4:int?,               5:bool,           6:bool,       7:bool)
--      ==> 0:orgunitid(int),1:orgunitdefid(int), 2:orgunitname(string?),3:parentorgunitid(int?)/4:parentorgunitdefid(int?),5:haschildren(bool),6:hasadmins(bool),7:found(bool)
--   when @filter_specified = true  (not empty) then then haschildren is 0 and hasadmins = 0
--   when @filter_specified = false (null or empty then parentorgunitid = null, parentorgunitdefid = null, found = false
CREATE PROCEDURE [dbo].[hlsysum_query_adminorgunits]
	@loadroots BIT,
	@adminid INT,
	@orgunitid INT,
	@orgunitdefid INT,
	@filter_text NVARCHAR(1000)
AS
	DECLARE @filter_specified BIT = IIF(LEN(ISNULL(@filter_text, N'')) > 0, 1, 0)
	DECLARE @iscentraladmin BIT = IIF(EXISTS(SELECT * FROM [dbo].[hlsyssec_query_agentglobalprmexec](@adminid, 780) WHERE canexec = 1), 1, 0) -- @AccessMask_HL_ACCESS_EXECUTE:780

	IF (@filter_specified = 1)
		IF (@iscentraladmin = 1)
			SELECT qo.orgunitid, qo.orgunitdefid, qo.name, qo.parentorgunitid, qo.parentorgunitdefid, CONVERT(BIT,0) AS haschildren, CONVERT(BIT,0) AS hasadmins, qo.found
				FROM [dbo].[hlsysum_query_centraladminorgunits](100, @adminid, @filter_text) AS qo
		ELSE		
			SELECT qo.orgunitid, qo.orgunitdefid, qo.name, qo.parentorgunitid, qo.parentorgunitdefid, CONVERT(BIT,0) AS haschildren, CONVERT(BIT,0) AS hasadmins, qo.found
				FROM [dbo].[hlsysum_query_businessadminorgunits](100, @adminid, @filter_text) AS qo
	ELSE
		IF (@iscentraladmin = 1)
			IF (@loadroots = 1)
				SELECT orgunitid, orgunitdefid, orgunitname, CONVERT(INT, NULL) AS parentorgunitid, CONVERT(INT, NULL) AS parentorgunitdefid, CONVERT(BIT,IIF(childcnt>0,1,0)) AS haschildren, CONVERT(BIT,IIF(admincnt>0,1,0)) AS hasadmins, CONVERT(BIT, 0) AS found 
				  FROM [dbo].[hlsysum_querycentraladminrootorgunits](@adminid)
			ELSE
				SELECT orgunitid, orgunitdefid, orgunitname, CONVERT(INT, NULL) AS parentorgunitid, CONVERT(INT, NULL) AS parentorgunitdefid, CONVERT(BIT,IIF(childcnt>0,1,0)) AS haschildren, CONVERT(BIT,IIF(admincnt>0,1,0)) AS hasadmins, CONVERT(BIT, 0) AS found 
				  FROM [dbo].[hlsysum_querycentraladminorgunitchildren](@adminid, @orgunitid, @orgunitdefid)
		ELSE
			IF (@loadroots = 1)
				SELECT orgunitid, orgunitdefid, orgunitname, CONVERT(INT, NULL) AS parentorgunitid, CONVERT(INT, NULL) AS parentorgunitdefid, CONVERT(BIT,IIF(childcnt>0,1,0)) AS haschildren, CONVERT(BIT,IIF(admincnt>0,1,0)) AS hasadmins, CONVERT(BIT, 0) AS found
				  FROM [dbo].[hlsysum_querybusinessadminrootorgunits](@adminid)
			ELSE
				SELECT orgunitid, orgunitdefid, orgunitname, CONVERT(INT, NULL) AS parentorgunitid, CONVERT(INT, NULL) AS parentorgunitdefid, CONVERT(BIT,IIF(childcnt>0,1,0)) AS haschildren, CONVERT(BIT,IIF(admincnt>0,1,0)) AS hasadmins, CONVERT(BIT, 0) AS found
				  FROM [dbo].[hlsysum_querybusinessadminorgunitchildren](@adminid, @orgunitid, @orgunitdefid)
RETURN 0
