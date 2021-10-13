-- @Name IntTwoSet
CREATE TYPE [dbo].[hlsys_udt_inttwoset] AS TABLE
(
	va INT NOT NULL,
	vb INT NOT NULL,
	PRIMARY KEY ([va],[vb])
)