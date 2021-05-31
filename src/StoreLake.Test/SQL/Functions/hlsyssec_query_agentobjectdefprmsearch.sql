---CREATE       [dbo].[hlsyssec_query_agentobjectmsk]
CREATE FUNCTION [dbo].[hlsyssec_query_agentobjectdefprmsearch]
(
    @agentid INT,
	@objectdefid INT,
	@objectglobalid INT
)
RETURNS TABLE AS RETURN
(
WITH aoa_g_bits AS
(
    SELECT a2g.agentid, a2g.groupid
	   , aoa_s.accessmask AS accessmask_s, aoa_g.accessmask AS accessmask_g
	   , COALESCE(aoa_s.accessmask, aoa_g.accessmask, 0) AS am
    FROM [dbo].[hlsysagenttogroup] AS a2g WITH (NOLOCK)
    LEFT OUTER JOIN [dbo].[hlsysobjectdefacl] AS aoa_s WITH (NOLOCK) ON a2g.groupid = aoa_s.groupid AND aoa_s.objectdefid = @objectdefid AND ( @agentid < 1500 OR @agentid > 2000)
    LEFT OUTER JOIN [dbo].[hlsysglobalacl]    AS aoa_g WITH (NOLOCK) ON a2g.groupid = aoa_g.groupid AND aoa_g.id = @objectglobalid AND ( @agentid < 1500 OR @agentid > 2000)
    WHERE  ( @agentid < 1500 OR @agentid > 2000) AND a2g.agentid=@agentid AND ( aoa_s.accessmask IS NOT NULL OR aoa_g.accessmask IS NOT NULL)
)
, query_regular_agent_mask AS
(
	SELECT CONVERT(BIT, 0
		  --+ 0x0001*MAX(IIF(aoa_g.am & 0x0001 = 0x0001, 1, 0))
		  --+ 0x0002*MAX(IIF(aoa_g.am & 0x0002 = 0x0002, 1, 0))
		  --+ 0x0004*MAX(IIF(aoa_g.am & 0x0004 = 0x0004, 1, 0))
		  --+ 0x0008*MAX(IIF(aoa_g.am & 0x0008 = 0x0008, 1, 0))
		  --+ 0x0010*MAX(IIF(aoa_g.am & 0x0010 = 0x0010, 1, 0))
		  --+ 0x0020*MAX(IIF(aoa_g.am & 0x0020 = 0x0020, 1, 0))
		  --+ 0x0040*MAX(IIF(aoa_g.am & 0x0040 = 0x0040, 1, 0))
		  --+ 0x0080*MAX(IIF(aoa_g.am & 0x0080 = 0x0080, 1, 0))
		  + 0x0100*MAX(IIF(aoa_g.am & 0x0100 = 0x0100, 1, 0))
		  --+ 0x0200*MAX(IIF(aoa_g.am & 0x0200 = 0x0200, 1, 0))
		  --+ 0x0400*MAX(IIF(aoa_g.am & 0x0400 = 0x0400, 1, 0))
		  --+ 0x0800*MAX(IIF(aoa_g.am & 0x0800 = 0x0800, 1, 0))
		  ) AS prmfinal
	 FROM aoa_g_bits AS aoa_g
	 GROUP BY aoa_g.agentid
)
SELECT ram.prmfinal AS [cansearch] FROM query_regular_agent_mask AS ram WHERE ram.prmfinal = 1
)