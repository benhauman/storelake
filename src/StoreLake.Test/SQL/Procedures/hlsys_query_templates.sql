-- @Namespace Repository
-- @Name GetTemplates
-- @Return Repository.Template
CREATE PROCEDURE [dbo].[hlsys_query_templates]
    @agentid INT,
    @lcid INT,
    @objectdefname NVARCHAR(255)
AS
BEGIN
    ;WITH [templatetranslations] AS
    (
        SELECT ttv.[ttkey], ttv.[ttvalue]
        FROM [dbo].[hlsystranslationvalue] AS ttv
        WHERE ttv.[ttcategoryid] = 5 AND ttv.[ttlcid] IN (
            SELECT TOP 1 ttv2.[ttlcid]
            FROM [dbo].[hlsystranslationvalue] AS ttv2
            CROSS APPLY [dbo].[hlsys_query_culturepriority](@lcid, 0) AS c
            WHERE ttv2.[ttkey] = ttv.[ttkey] AND c.[lcid] = ttv2.[ttlcid]
        )
    )
    , [groups] AS 
    (
        SELECT g.[id], ISNULL(tt.[ttvalue], g.[name]) AS [name], g.[agentid], g.[objectdefname], g.[parentid] 
        FROM [dbo].[hlsystemplategroup] AS g
        CROSS APPLY [dbo].[hlsyssec_query_agenttemplategroupprmread](@agentid, g.[id]) AS prmread
        LEFT JOIN [templatetranslations] AS tt ON tt.ttkey = CONCAT(N'G_', g.[name], N'_', g.[id])
        WHERE g.[objectdefname] = @objectdefname AND (g.[agentid] = @agentid OR g.[agentid] = 0)
    )
    , [grouprecursion] AS
    (
        SELECT g.[id], CAST(g.[name] AS NVARCHAR(MAX)) AS [name], g.[agentid], g.[objectdefname], g.[parentid] 
        FROM [groups] AS g
        WHERE g.[parentid] = 0
        UNION ALL
        SELECT g.[id], CAST(CONCAT(p.[name], N'\', g.[name]) AS NVARCHAR(MAX)) AS [name], g.[agentid], g.[objectdefname], g.[parentid] FROM [grouprecursion] AS p 
        JOIN [groups] AS g ON p.[id] = g.[parentid]
    )
    , [templates] AS
    (
        SELECT t.[id], g.[name] AS [path], ISNULL(tt.ttvalue, t.[name]) AS [name], t.[agentid], t.[sortorder], t.[objectdefname], t.[parentid] 
        FROM [dbo].[hlsystemplatedefinition] AS t
        LEFT JOIN [grouprecursion] AS g ON t.[parentid] = g.[id]
        CROSS APPLY [dbo].[hlsyssec_query_agenttemplatedefinitionprmread](@agentid, t.[id]) AS prmread
        LEFT JOIN [templatetranslations] AS tt ON tt.ttkey = CONCAT(N'T_', t.[name], N'_', t.[id])
        WHERE (g.[id] IS NOT NULL OR t.[parentid] = 0) AND t.[objectdefname] = @objectdefname AND (t.[agentid] = @agentid OR t.[agentid] = 0)
    )
    SELECT [id] AS [Id]
            , [path] AS [Path]
            , [name] AS [Name]
            , [agentid] AS [AgentId]
            , [sortorder] AS [Sortorder]
            , [objectdefname] AS [ObjectDefinitionName]
            , [parentid] AS [ParentId]
    FROM templates
    ORDER BY name
END
