CREATE VIEW dbo.hlcmcontactvw AS
SELECT a.objectid AS personid, a.objectdefid AS persondefid,psys.version, a.agentid, n.name AS surname, g.givenname AS name,ploc.localeid AS language,
t.title, s.street, ci.city, r.region, z.zipcode, c.country, e.emailaddress AS email,p.phonenumber FROM dbo.hlsysagenttoobject AS a
FULL OUTER JOIN dbo.hlsysagentroutingblacklist AS b ON b.agentid = a.agentid
INNER JOIN dbo.hlsysagent AS asys ON asys.agentid = a.agentid AND asys.active = 1
INNER JOIN dbo.hlsyspersonnamevw AS n ON n.personid = a.objectid AND n.persondefid = a.objectdefid
INNER JOIN dbo.hlsyspersongivennamevw AS g ON g.personid = a.objectid AND g.persondefid = a.objectdefid
INNER JOIN dbo.hlsyspersontitlevw AS t ON t.personid = a.objectid AND t.persondefid = a.objectdefid
INNER JOIN dbo.hlsyspersoncountryvw AS c ON c.personid = a.objectid AND c.persondefid = a.objectdefid
INNER JOIN dbo.hlsyspersonstreetvw AS s ON s.personid = a.objectid AND s.persondefid = a.objectdefid
INNER JOIN dbo.hlsyspersoncityvw AS ci ON ci.personid = a.objectid AND ci.persondefid = a.objectdefid
INNER JOIN dbo.hlsyspersonzipcodevw AS z ON z.personid = a.objectid AND z.persondefid = a.objectdefid
INNER JOIN dbo.hlsyspersonvw AS psys ON psys.personid = a.objectid AND psys.persondefid = a.objectdefid
INNER JOIN dbo.hlsyspersonlocalevw AS ploc ON psys.personid = ploc.personid AND psys.persondefid = ploc.persondefid
LEFT JOIN dbo.hlsyspersonregionvw AS r ON r.personid = a.objectid AND r.persondefid = a.objectdefid
LEFT JOIN dbo.hlsyspersonphonenumbervw AS p ON p.personid = a.objectid AND p.persondefid = a.objectdefid AND p.isdefault = 1
LEFT JOIN dbo.hlsyspersonemailaddressvw AS e ON e.personid = a.objectid AND e.persondefid = a.objectdefid AND e.isdefault = 1
