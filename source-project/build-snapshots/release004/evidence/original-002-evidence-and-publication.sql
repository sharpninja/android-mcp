-- Additive migration from version 1. Entry/API JSON contracts retain version 1.0.0;
-- additions live under their explicitly reserved extensions/x-* namespaces.
PRAGMA foreign_keys=ON;
BEGIN IMMEDIATE;
CREATE TABLE source_details(source_id TEXT PRIMARY KEY REFERENCES sources, document_json TEXT NOT NULL CHECK(json_valid(document_json)));
CREATE TABLE parse_reports(source_id TEXT PRIMARY KEY REFERENCES sources, document_json TEXT NOT NULL CHECK(json_valid(document_json)));
ALTER TABLE binding_declarations ADD COLUMN record_sha256 TEXT;
CREATE TABLE annotation_evidence(source_id TEXT NOT NULL REFERENCES sources, locator TEXT NOT NULL, binding_id TEXT REFERENCES bindings, scope TEXT NOT NULL, document_json TEXT NOT NULL CHECK(json_valid(document_json)), PRIMARY KEY(source_id,locator));
CREATE INDEX annotation_target ON annotation_evidence(binding_id);
CREATE TABLE permission_symbols(symbol TEXT NOT NULL, value TEXT NOT NULL, source_id TEXT NOT NULL REFERENCES sources, line INTEGER NOT NULL, PRIMARY KEY(symbol,source_id,line));
CREATE TABLE effective_entries(entry_id TEXT PRIMARY KEY REFERENCES bindings(binding_id), document_json TEXT NOT NULL CHECK(json_valid(document_json)), record_sha256 TEXT NOT NULL, etag TEXT NOT NULL);
CREATE TRIGGER source_details_no_update BEFORE UPDATE ON source_details BEGIN SELECT RAISE(ABORT,'Imported evidence is immutable'); END;
CREATE TRIGGER source_details_no_delete BEFORE DELETE ON source_details BEGIN SELECT RAISE(ABORT,'Imported evidence is immutable'); END;
CREATE TRIGGER annotation_evidence_no_update BEFORE UPDATE ON annotation_evidence BEGIN SELECT RAISE(ABORT,'Imported evidence is immutable'); END;
CREATE TRIGGER annotation_evidence_no_delete BEFORE DELETE ON annotation_evidence BEGIN SELECT RAISE(ABORT,'Imported evidence is immutable'); END;
CREATE TRIGGER permission_symbols_no_update BEFORE UPDATE ON permission_symbols BEGIN SELECT RAISE(ABORT,'Imported evidence is immutable'); END;
CREATE TRIGGER permission_symbols_no_delete BEFORE DELETE ON permission_symbols BEGIN SELECT RAISE(ABORT,'Imported evidence is immutable'); END;
CREATE TRIGGER effective_entries_no_update BEFORE UPDATE ON effective_entries BEGIN SELECT RAISE(ABORT,'Publish a new registry instead'); END;
CREATE TRIGGER effective_entries_no_delete BEFORE DELETE ON effective_entries BEGIN SELECT RAISE(ABORT,'Publish a new registry instead'); END;
CREATE VIEW runtime_catalog AS
 SELECT e.entry_id,coalesce(o.document_json,e.document_json) AS document_json,
 CASE WHEN o.entry_id IS NULL THEN e.etag ELSE '"overlay:'||o.entry_id||':'||o.revision||'"' END AS etag
 FROM effective_entries e LEFT JOIN overlays o ON o.imported_binding_id=e.entry_id
 WHERE o.entry_id IS NULL OR o.state='active'
 UNION ALL
 SELECT o.entry_id,o.document_json,'"overlay:'||o.entry_id||':'||o.revision||'"'
 FROM overlays o WHERE o.imported_binding_id IS NULL AND o.state='active';
PRAGMA user_version=2;
COMMIT;
