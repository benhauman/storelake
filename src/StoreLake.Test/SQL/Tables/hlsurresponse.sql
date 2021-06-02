CREATE TABLE [dbo].[hlsurresponse]
(
	[surveyid] INT NOT NULL,
	[received] DATETIME NOT NULL,
	[result] SMALLINT NOT NULL,
	CONSTRAINT [PK_hlsurresponse] PRIMARY KEY ([surveyid]),
	CONSTRAINT [FK_hlsurresponse_surveyid] FOREIGN KEY ([surveyid]) REFERENCES [dbo].[hlsursurvey]([surveyid])
)
