-- @Namespace Administration
-- @Name UpdateEditorial
CREATE
--OR ALTER
PROCEDURE [dbo].[hlnewseditorial_merge] 
    @editorialid    INT
  , @editorialdefid INT
  , @editorids      [dbo].[hlsys_udt_inttwoset] READONLY
AS                                                                       
    -- Ensure editorial definition exists for this org unit
    IF NOT EXISTS (SELECT 1 FROM [dbo].[hlnewseditorial] AS [e] WHERE [e].[orgunitid] = @editorialid AND [e].[orgunitdefid] = @editorialdefid)
    BEGIN
        INSERT INTO [dbo].[hlnewseditorial] ([orgunitdefid], [orgunitid], [orgunittype]) VALUES(@editorialdefid, @editorialid, 4)
    END

    -- Merge list of editors for a given editorial
    MERGE [dbo].[hlnewseditor] AS [target]
    USING @editorids AS [source]
    ON ([target].[orgunitid]    = @editorialid
    AND [target].[orgunitdefid] = @editorialdefid
    AND [target].[personid]     = [source].[va]
    AND [target].[persondefid]  = [source].[vb])
    WHEN NOT MATCHED THEN
        INSERT (  [orgunitdefid],  [orgunitid], [persondefid], [personid],    [persontype])
        VALUES (@editorialdefid, @editorialid,  [source].[vb], [source].[va], 3)
    WHEN NOT MATCHED BY SOURCE AND [target].[orgunitid] = @editorialid AND [target].[orgunitdefid] = @editorialdefid
        THEN DELETE
    ;
