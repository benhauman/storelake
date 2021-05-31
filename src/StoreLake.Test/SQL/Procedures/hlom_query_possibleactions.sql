-- @Namespace ObjectManagement
-- @Name GetPossibleActions
-- @Return short
CREATE PROCEDURE [dbo].[hlom_query_possibleactions] @agentid INT, @otheragentid INT NULL, @channelid SMALLINT, @objectdefid INT, @objectid INT
AS
BEGIN
    SET NOCOUNT ON
    
    DECLARE @actions TABLE ([actionid] SMALLINT NOT NULL, PRIMARY KEY ([actionid]))
    DECLARE @objecttype INT = (SELECT [objecttype] FROM [dbo].[hlsysobjectdef] WHERE [objectdefid] = @objectdefid)
    DECLARE @accessmask INT = (SELECT [accessmask] FROM [dbo].[hlsyssec_query_agentobjectmsk](@agentid, @objectid, @objectdefid, IIF(@objecttype = 7, 789, @objecttype + 783)))
    DECLARE @controller NVARCHAR(100)
    DECLARE @reservedby INT = 0
    DECLARE @shape INT = 0

    DECLARE @canupdate BIT
    DECLARE @candelete BIT

    IF @objecttype = 2
    BEGIN
        SELECT @controller = [controller], @reservedby = [reservedby], @shape = [shape]
        FROM [dbo].[hlsyscasevw] 
        WHERE [casedefid] = @objectdefid AND [caseid] = @objectid
    END

    -- Case
    IF @objecttype = 2
    BEGIN
        DECLARE @allowmodifycase BIT = IIF((@accessmask & 133) = 133 /* Read/Modify/Take ownership */, 1, 0)
        DECLARE @unreserved BIT = IIF(@reservedby = 0, 1, 0)
        DECLARE @reservedbyme BIT = IIF(@reservedby = @agentid, 1, 0)
        DECLARE @isonline BIT = IIF(EXISTS(SELECT 1 FROM [dbo].[hlsyssession] WITH (NOLOCK) WHERE [agentid] = @reservedby), 1, 0)
        DECLARE @allowfreebehalfofonline BIT = (SELECT [canexec] FROM [dbo].[hlsyssec_query_agentglobalprmexec] (@agentid, 794))
        DECLARE @allowfreebehalfofoffline BIT = (SELECT [canexec] FROM [dbo].[hlsyssec_query_agentglobalprmexec] (@agentid, 795))
        DECLARE @isportalsupporter BIT = IIF(EXISTS(SELECT 1 
                                                    FROM [dbo].[hlsysglobalsetting] AS [s] 
                                                    INNER JOIN [dbo].[hlsysagenttogroup] AS [a2g] ON [s].[settingid] = 7
                                                                                                 AND CAST([s].[settingvalue] AS INT) = [a2g].[groupid]
                                                                                                 AND [a2g].[agentid] = @agentid
                                                                                                 AND @channelid = 213), 1, 0)
		DECLARE @isonwatchlist BIT = IIF(EXISTS(SELECT 1 FROM [dbo].[hlsyswatchlist] WHERE [defid] = @objectdefid AND [objid] = @objectid AND [agentid] = @agentid), 1, 0)
		DECLARE @allowwatchlist BIT = (SELECT [canexec] FROM [dbo].[hlsyssec_query_agentglobalprmexec] (@agentid, 834))

        -- Adhoc
        IF @controller = N'79F2D5E4-0307-44D3-AF55-51D16604C97B'
        BEGIN
            DECLARE @ispartofcasefolder BIT = IIF(@shape & 2 = 2, 1, 0)
            DECLARE @canreserve BIT = IIF(@ispartofcasefolder = 0 AND @allowmodifycase = 1 AND @unreserved = 1, 1, 0)
        
            -- OA_UPDATE
            SET @canupdate = IIF(@ispartofcasefolder = 0 AND @allowmodifycase = 1 AND (@isportalsupporter = 1 OR @reservedbyme = 1), 1, 0)
            INSERT INTO @actions ([actionid])
            SELECT 1 
            WHERE @canupdate = 1

            -- OA_DELETE
            DECLARE @otherreserver BIT = IIF(@unreserved = 0 AND @reservedbyme = 0, 1, 0)
            DECLARE @isdelegatecase BIT = IIF(@shape & 4 = 4, 1, 0)
            DECLARE @allowdeletecase BIT = IIF((@accessmask & 141) = 141 /* Read/Modify/Delete/Take ownership */, 1, 0)
            SET @candelete = IIF(@otherreserver = 0 AND @ispartofcasefolder = 0 AND @isdelegatecase = 0 AND @allowdeletecase = 1, 1, 0)
            INSERT INTO @actions ([actionid])
            SELECT 2 
            WHERE @candelete = 1

            -- OA_RESERVE
            INSERT INTO @actions ([actionid])
            SELECT 3
            WHERE @canreserve = 1

            -- OA_RESERVEBEHALFOF
            DECLARE @allowreservebehalfof BIT = (SELECT [canexec] FROM [dbo].[hlsyssec_query_agentglobalprmexec] (@agentid, 793))
            DECLARE @checksameagent BIT = IIF(ISNULL(@otheragentid, 0) <> @agentid OR @canreserve = 1, 1, 0) -- If other agent == current agent => Check CanReserve instead
            DECLARE @otheragentspecified BIT = IIF(ISNULL(@otheragentid, 0) = 0, 0, 1)
            DECLARE @allowotheragentmodify BIT = (SELECT IIF([accessmask] & 133 = 133, 1, 0) FROM [dbo].[hlsyssec_query_agentobjectmsk](@otheragentid, @objectid, @objectdefid, 785))
            DECLARE @canreservebehalfof BIT = IIF(@allowreservebehalfof = 1 AND @ispartofcasefolder = 0 AND @unreserved = 1 AND @checksameagent = 1 AND @otheragentspecified = 1 AND @allowotheragentmodify = 1, 1, 0)
            INSERT INTO @actions ([actionid])
            SELECT 4
            WHERE @canreservebehalfof = 1

            -- OA_RELEASE
            DECLARE @canrelease BIT = IIF(@ispartofcasefolder = 0 AND @allowmodifycase = 1 AND @unreserved = 0 AND (@reservedbyme = 1 OR (@isonline = 0 AND @allowfreebehalfofoffline = 1 OR @isonline = 1 AND @allowfreebehalfofonline = 1)), 1, 0)
            INSERT INTO @actions ([actionid])
            SELECT 5
            WHERE @canrelease = 1

            -- OA_DELEGATE
            DECLARE @candelegate BIT = IIF(@ispartofcasefolder = 0 AND @allowmodifycase = 1 AND @reservedbyme = 1, 1, 0)
            INSERT INTO @actions ([actionid])
            SELECT 6
            WHERE @candelegate = 1

            -- OA_CREATEFOLDER
            DECLARE @cancreatecasefolder BIT = IIF(@shape = 0 AND @allowmodifycase = 1 AND @reservedbyme = 1, 1, 0)
            INSERT INTO @actions ([actionid])
            SELECT 7
            WHERE @cancreatecasefolder = 1

            -- OA_FOLDERADDITEM
            DECLARE @iscasefolder BIT = IIF(@shape & 1 = 1, 1, 0)
            DECLARE @canfolderadditem BIT = IIF(@iscasefolder = 1 AND @allowmodifycase = 1 AND @reservedbyme = 1, 1, 0)
            INSERT INTO @actions ([actionid])
            SELECT 8
            WHERE @canfolderadditem = 1

            -- OA_REMOVEFROMFOLDER
            DECLARE @folderdefid INT = 0
            DECLARE @folderid INT = 0
            DECLARE @folderreservedby INT = 0

            SELECT TOP 1 @folderdefid = [a].[objectdefida], @folderid = [a].[objectida], @folderreservedby = [c].[reservedby]
            FROM [dbo].[hlsysassociation] AS [a] WITH (NOLOCK)
            INNER JOIN [dbo].[hlsyscasevw] AS [c] ON [a].[objectdefida] = [c].[casedefid] AND [a].[objectida] = [c].[caseid]
            WHERE [a].[associationdefid] = 133 AND [a].[objectdefidb] = @objectdefid AND [a].[objectidb] = @objectid

            DECLARE @isfolderreservedbyme BIT = IIF(@folderreservedby = @agentid, 1, 0)
            DECLARE @allowmodifycasefolder BIT = (SELECT IIF([accessmask] & 133 = 133, 1, 0) FROM [dbo].[hlsyssec_query_agentobjectmsk](@agentid, @folderid, @folderdefid, 785))
            DECLARE @canremovefromfolder BIT = IIF(@ispartofcasefolder = 1 AND @allowmodifycase = 1 AND @isfolderreservedbyme = 1 AND @allowmodifycasefolder = 1, 1, 0)
            INSERT INTO @actions ([actionid])
            SELECT 9
            WHERE @canremovefromfolder = 1

            -- OA_EXTENDCASE
            DECLARE @canextendcase BIT = IIF(@ispartofcasefolder = 0 AND @allowmodifycase = 1 AND @isportalsupporter = 1, 1, 0)
            INSERT INTO @actions ([actionid])
            SELECT 10
            WHERE @canextendcase = 1
        END
        -- Workflow
        ELSE
        BEGIN
            IF @allowmodifycase = 1
            BEGIN
                INSERT INTO @actions ([actionid])
                SELECT CASE [actionid] WHEN 5 /* UpdateHLObject */ THEN 1   /* OA_UPDATE */
                                       WHEN 6 /* DeleteHLObject */ THEN 2   /* OA_DELETE */
                                       WHEN 8 /* ReserveCase */    THEN 3   /* OA_RESERVE */
                                       WHEN 7 /* ReleaseCase */    THEN 5   /* OA_RELEASE */
                                       WHEN 9 /* ExtendCase */     THEN 10  /* OA_EXTENDCASE */
                                       WHEN 1 /* OnMail */         THEN 11  /* OA_EXTENDFROMMAIL */
                                       WHEN 2 /* OnAlert */        THEN 12  /* OA_EXTENDFROMEAS */
                       END 
                FROM [dbo].[hlwf_query_possibleactions](@controller, @agentid, @objectid, @objectdefid)
                WHERE [actionid] <> 7 
                   -- Additional release case check
                   OR (@unreserved = 0 AND (@reservedbyme = 1 OR (@isonline = 0 AND @allowfreebehalfofoffline = 1 OR @isonline = 1 AND @allowfreebehalfofonline = 1)))
            END                          
        END

        -- OA_RESERVEOREDIT
        INSERT INTO @actions ([actionid])
        SELECT TOP 1 16
        FROM @actions
        WHERE [actionid] = 1/* OA_UPDATE */ OR [actionid] = 3 /* OA_RESERVE */

        -- OA_ADDTOWATCHLIST
        INSERT INTO @actions ([actionid])
        SELECT 17
        WHERE @isportalsupporter = 0 AND @allowwatchlist = 1 AND @isonwatchlist = 0

        -- OA_REMOVEFROMWATCHLIST
        INSERT INTO @actions ([actionid])
        SELECT 18
        WHERE @isportalsupporter = 0 AND @allowwatchlist = 1 AND @isonwatchlist = 1
    END
    -- Masterdata
    ELSE
    BEGIN
        -- Update
        DECLARE @allowmodify BIT = IIF(@accessmask & 5 = 5 /* Read/Modify */, 1, 0)
        SET @canupdate = IIF(@allowmodify = 1, 1, 0)
        INSERT INTO @actions ([actionid])
        SELECT 1 
        WHERE @canupdate = 1

        -- Delete
        DECLARE @allowdelete BIT = IIF((@accessmask & 9) = 9 /* Read/Delete */, 1, 0)
        SET @candelete = IIF(@allowdelete = 1, 1, 0)
        INSERT INTO @actions ([actionid])
        SELECT 2 
        WHERE @candelete = 1
    END

    SELECT [actionid] 
    FROM @actions
END