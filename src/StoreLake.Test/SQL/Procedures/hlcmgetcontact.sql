CREATE PROCEDURE [dbo].[hlcmgetcontact]
@PersonId INT,
@PersonDefId INT
AS BEGIN
SET NOCOUNT ON;
SELECT 
 personid, persondefid, surname, name, language, title,
 street, city, region, zipcode,country, email, phonenumber
FROM dbo.hlcmcontactvw
WHERE personid=@PersonId AND persondefid=@PersonDefId
END