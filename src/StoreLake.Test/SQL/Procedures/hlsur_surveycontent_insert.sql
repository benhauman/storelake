--DECLARE @template NVARCHAR(MAX) = CONVERT(nvarchar(max), (SELECT data FROM hltmnotificationtaskstorage WHERE RowKey = 'FeedbackOne'))
--DECLARE @surveydefid INT = 3
--DECLARE @lcid INT = 1033
--DECLARE @caseidString NVARCHAR(255) = '100100'
--DECLARE @casedefidString NVARCHAR(255) = '100824'
--DECLARE @personid INT = '100300'
--DECLARE @persondefid INT = '100307'

CREATE PROCEDURE [dbo].[hlsur_surveycontent_insert]
	@surveydefid INT,
	@template NVARCHAR(MAX),
    @attachments NVARCHAR(MAX),
	@lcid INT,
	@caseidString NVARCHAR(255),
	@casedefidString NVARCHAR(255),
	@personid INT,
	@persondefid INT
AS
BEGIN
	SET NOCOUNT ON

	DECLARE @surveybusinessviolation INT = 0;

	-- insert surveycontent into template if necessary
	IF(@surveydefid != -1)
	BEGIN
		DECLARE @surveyDefinition TABLE (defid INT, name NVARCHAR(128), typeid INT, validityweeks INT)
		INSERT INTO @surveyDefinition ([defid], [name], [typeid], [validityweeks])
			SELECT [surveydefid],[name],[typeid],[validityweeks]
			FROM [dbo].[hlsurdefinition]
			WHERE [surveydefid] = @surveydefid

		DECLARE @portalLink NVARCHAR(MAX) = (SELECT [settingvalue] 
											FROM [dbo].[hlsysglobalsetting] 
											WHERE [settingid] = 20)

		DECLARE @caseid INT = CONVERT(INT, @caseidString)
		DECLARE @casedefid INT = CONVERT(INT, @casedefidString)

		-- only add surveydata if the definition exists and a portal link is present
		IF(@caseid IS NOT NULL AND @casedefid IS NOT NULL AND @portalLink IS NOT NULL AND (SELECT COUNT(*) FROM @surveyDefinition) > 0)
		BEGIN
			-- get content by agent lcid, else parent lcid, else default helpline lcid 
			DECLARE @surveyContentXML XML = (SELECT [content]
									FROM [dbo].[hlsurtypecontent] AS hst
									JOIN [dbo].[hlsurdefinition] AS hsd ON hsd.[typeid] = hst.[typeid]
									WHERE hsd.[surveydefid] = @surveydefid
									AND (hst.[lcid] = @lcid))
			IF (@surveyContentXML IS NULL)
			BEGIN
				DECLARE @surveytemplcid INT = (SELECT [parentlcid] 
												FROM   [dbo].[hlsyscultureinfo]
												WHERE  lcid = @lcid) 
				SET @surveyContentXML = (SELECT [content]
									FROM [dbo].[hlsurtypecontent] AS hst
									JOIN [dbo].[hlsurdefinition] AS hsd ON hsd.[typeid] = hst.[typeid]
									WHERE hsd.[surveydefid] = @surveydefid
									AND (hst.[lcid] = @surveytemplcid))
				IF(@surveyContentXML IS NULL)
				BEGIN
					SET @surveytemplcid = (SELECT TOP 1 lcid 
											FROM   [dbo].[hlsysdefaultculture])
					SET @surveyContentXML = (SELECT [content]
											FROM [dbo].[hlsurtypecontent] AS hst
											JOIN [dbo].[hlsurdefinition] AS hsd ON hsd.[typeid] = hst.[typeid]
											WHERE hsd.[surveydefid] = @surveydefid
											AND (hst.[lcid] = @surveytemplcid))
				END
			END
			DECLARE @surveyContent NVARCHAR(MAX) = CONVERT(NVARCHAR(MAX), @surveyContentXML)
			IF(@surveyContent IS NOT NULL)
			BEGIN
				DECLARE @surveyid INT = 0
				SELECT TOP 1 @surveyid = [surveyid] FROM [dbo].[hlsursurvey]
				WHERE [surveydefid] = @surveydefid AND [caseid] = @caseid AND [recipientid] = @personid
				ORDER BY [surveyid] DESC

				IF(@surveyid = 0)
				BEGIN
					INSERT INTO [dbo].[hlsursurvey] ([surveydefid], [caseid], [casedefid], [recipientid], [recipientdefid], [transmitted])
					VALUES(@surveydefid, @caseid, @casedefid, @personid, @persondefid, GETUTCDATE())

					SET @surveyid = CONVERT(INT, IDENT_CURRENT(N'hlsursurvey'))
				END
				ELSE
				BEGIN
					-- Handle Unique Key Violation (sys.messages id = 2627) so subprocess does not suspend
					SET @surveybusinessviolation = 2627 
				END

				DECLARE @linkPattern NVARCHAR(100) = N'[[][[]link_replacement_'
				DECLARE @linkEndPattern NVARCHAR(100) = N']]'
				DECLARE @linkLengthPattern BIGINT = LEN(@linkPattern) - 4	-- Remove 4 from length to ignore the regex escaping characters
				DECLARE @surveylinkStartIndex BIGINT = PATINDEX(N'%' + @linkPattern + N'%', @surveyContent)
				WHILE (@surveylinkStartIndex != 0)
				BEGIN
					DECLARE @surveylinkEndIndex BIGINT = 0, @tempSurveyContent NVARCHAR(MAX) = NULL,
					@surveyResultValue NVARCHAR(10) = NULL, @replacementLink NVARCHAR(MAX) = NULL, @idHash NVARCHAR(128) = NULL
					SET @tempSurveyContent = SUBSTRING(@surveyContent, @surveylinkStartIndex + @linkLengthPattern, LEN(@surveyContent))
					SET @surveylinkEndIndex = PATINDEX(N'%' + @linkEndPattern + N'%', @tempSurveyContent)
					SET @surveyResultValue = SUBSTRING(@tempSurveyContent, 0, @surveylinkEndIndex)
					-- Link replacement
					SET @replacementLink = @portalLink
					IF (SUBSTRING(@replacementLink, LEN(@replacementLink), 1) != N'/')
					BEGIN
						SET @replacementLink = CONCAT(@replacementLink, N'/')
					END

					SET @idHash = [dbo].[hlsur_createhashfromid](@surveyid)

					SET @replacementLink = CONCAT(@replacementLink, N'App/Survey/Result?hash=', @idHash, N'&id=', CONVERT(NVARCHAR(100), @surveyid), N'&result=' , @surveyResultValue)
					SET @surveyContent = STUFF(@surveyContent, @surveylinkStartIndex, @linkLengthPattern + LEN(@surveyResultValue) + LEN(@linkEndPattern), @replacementLink)

					SET @surveylinkStartIndex = PATINDEX(N'%' + @linkPattern + N'%', @surveyContent)
				END

				--escape XML characters in HTML for mail
				SET @surveyContent = (SELECT @surveyContent FOR XML PATH(N''))
				DECLARE @surveyPattern NVARCHAR(100) = N'[[][[]#Survey]]' 
				DECLARE @surveyLengthPattern INT = LEN(@surveyPattern) - 4	-- Remove 4 from length to ignore the regex escaping characters
				DECLARE @surveyStartIndex BIGINT = PATINDEX(N'%' + @surveyPattern + N'%', @template)
				IF(@surveyStartIndex != 0)
				BEGIN
					-- Successful insertion of survey content into notification task
					SET @template = STUFF(@template, @surveyStartIndex, @surveyLengthPattern, @surveyContent)
				END

                --add attachments
                DECLARE @attachmentStartindex INT
                DECLARE @surveyAttachments NVARCHAR(MAX)

                SET @attachmentStartindex = PATINDEX(N'%</Attachments>%', @attachments) 

                SELECT @surveyAttachments = CONCAT(
					@surveyAttachments, 
					N'<Attachment><Type>', [a].[contenttype], 
					N'</Type><Filename>0D4543028AF6417fB85C11902032DF59#', [a].[filename], N'#1#', [a].[filename],
					N'</Filename><Data>', [a].[data], 
					N'</Data></Attachment>'
				)
                FROM [dbo].[hlsurdefinition] AS [d]
                INNER JOIN [dbo].[hlsurtypecontentattachment] AS [a] ON [a].[typeid] = [d].[typeid]
                WHERE [d].[surveydefid] = @surveydefid

                IF (@attachmentStartindex > 0)
	                SET @attachments = CONCAT(
		                SUBSTRING(@attachments, 1, @attachmentStartindex - 1),
		                @surveyAttachments,
		                SUBSTRING(@attachments, @attachmentStartindex, LEN(@attachments) - @attachmentStartindex + 1)
	                )
                ELSE
	                SET @attachments = CONCAT(N'<Attachments>', @surveyAttachments, N'</Attachments>')
			END 
		END
	END
	SELECT @template, @attachments, @surveybusinessviolation
END