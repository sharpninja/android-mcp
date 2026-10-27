-- Read-only examples; the service/client must bind its intended registry/surface.
-- SQLite catalog metadata NEVER grants runtime OS or MCP client authority.

SELECT b.binding_id,b.signature,b.kind,e.document_json,e.etag
FROM bindings b JOIN runtime_catalog e ON e.entry_id=b.binding_id
WHERE b.package=:package AND b.owner=:owner AND b.name=:name
ORDER BY b.binding_id LIMIT 100;

-- Union of manifest names and declared permission string constants.
-- A constant without a manifest definition has UNKNOWN protection/attributes.
WITH names(name) AS (
 SELECT name FROM permission_definitions UNION SELECT value FROM permission_symbols
)
SELECT n.name,
 EXISTS(SELECT 1 FROM permission_definitions d WHERE d.name=n.name) AS has_manifest_definition,
 (SELECT count(*) FROM permission_symbols p WHERE p.value=n.name) AS constant_occurrences
FROM names n ORDER BY n.name;

SELECT d.name,d.protection_level,d.attributes_json,s.url,s.revision,sd.document_json AS source_details
FROM permission_definitions d JOIN sources s USING(source_id) JOIN source_details sd USING(source_id)
WHERE d.name=:permission;

-- Examine exact permission variants before selecting a particular source surface.
SELECT s.kind,s.path,s.revision,r.expression_json,r.raw_annotation
FROM permission_relations r JOIN sources s USING(source_id)
WHERE r.binding_id=:binding_id ORDER BY s.kind,s.path,r.line;
