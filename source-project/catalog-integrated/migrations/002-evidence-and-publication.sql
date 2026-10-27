-- Additive migration from version 1. Entry/API JSON contracts retain version 1.0.0;
-- additions live under their explicitly reserved extensions/x-* namespaces.
PRAGMA foreign_keys=ON;
BEGIN IMMEDIATE;
CREATE TABLE source_details(source_id TEXT NOT NULL PRIMARY KEY REFERENCES sources, document_json TEXT NOT NULL CHECK(json_valid(document_json)));
CREATE TABLE parse_reports(source_id TEXT NOT NULL PRIMARY KEY REFERENCES sources, document_json TEXT NOT NULL CHECK(json_valid(document_json)));
ALTER TABLE binding_declarations ADD COLUMN record_sha256 TEXT;
CREATE TABLE annotation_evidence(source_id TEXT NOT NULL REFERENCES sources, locator TEXT NOT NULL, binding_id TEXT REFERENCES bindings, scope TEXT NOT NULL, document_json TEXT NOT NULL CHECK(json_valid(document_json)), PRIMARY KEY(source_id,locator));
CREATE INDEX annotation_target ON annotation_evidence(binding_id);
CREATE TABLE permission_symbols(symbol TEXT NOT NULL, value TEXT NOT NULL, source_id TEXT NOT NULL REFERENCES sources, line INTEGER NOT NULL, PRIMARY KEY(symbol,source_id,line));
CREATE TABLE registry_entries(registry_id TEXT NOT NULL REFERENCES registries,entry_id TEXT NOT NULL REFERENCES bindings(binding_id),document_json TEXT NOT NULL CHECK(json_valid(document_json)),record_sha256 TEXT NOT NULL,PRIMARY KEY(registry_id,entry_id));
CREATE TRIGGER source_details_no_update BEFORE UPDATE ON source_details BEGIN SELECT RAISE(ABORT,'Imported evidence is immutable'); END;
CREATE TRIGGER source_details_no_delete BEFORE DELETE ON source_details BEGIN SELECT RAISE(ABORT,'Imported evidence is immutable'); END;
CREATE TRIGGER annotation_evidence_no_update BEFORE UPDATE ON annotation_evidence BEGIN SELECT RAISE(ABORT,'Imported evidence is immutable'); END;
CREATE TRIGGER annotation_evidence_no_delete BEFORE DELETE ON annotation_evidence BEGIN SELECT RAISE(ABORT,'Imported evidence is immutable'); END;
CREATE TRIGGER permission_symbols_no_update BEFORE UPDATE ON permission_symbols BEGIN SELECT RAISE(ABORT,'Imported evidence is immutable'); END;
CREATE TRIGGER permission_symbols_no_delete BEFORE DELETE ON permission_symbols BEGIN SELECT RAISE(ABORT,'Imported evidence is immutable'); END;
CREATE TRIGGER registry_entries_no_update BEFORE UPDATE ON registry_entries BEGIN SELECT RAISE(ABORT,'Publish a new registry instead'); END;
CREATE TRIGGER registry_entries_no_delete BEFORE DELETE ON registry_entries BEGIN SELECT RAISE(ABORT,'Publish a new registry instead'); END;
-- ETag composition is identical for base, overlay, tombstone and history CAS.
CREATE TABLE catalog_view(singleton INTEGER PRIMARY KEY CHECK(singleton=1),registry_generation INTEGER NOT NULL CHECK(registry_generation>0),overlay_generation INTEGER NOT NULL CHECK(overlay_generation>=0));
INSERT INTO catalog_view VALUES(1,1,0);
CREATE TRIGGER overlay_generation_insert AFTER INSERT ON overlays BEGIN UPDATE catalog_view SET overlay_generation=overlay_generation+1; END;
CREATE TRIGGER overlay_generation_update AFTER UPDATE ON overlays BEGIN UPDATE catalog_view SET overlay_generation=overlay_generation+1; END;
CREATE TRIGGER overlay_generation_delete AFTER DELETE ON overlays BEGIN UPDATE catalog_view SET overlay_generation=overlay_generation+1; END;
CREATE TRIGGER registry_generation_switch AFTER UPDATE OF registry_id ON active_registry WHEN old.registry_id<>new.registry_id BEGIN UPDATE catalog_view SET registry_generation=max(registry_generation+1,(SELECT registry_version FROM registries WHERE registry_id=new.registry_id)); END;
CREATE TRIGGER parse_reports_no_update BEFORE UPDATE ON parse_reports BEGIN SELECT RAISE(ABORT,'Imported reports are immutable'); END;
CREATE TRIGGER parse_reports_no_delete BEFORE DELETE ON parse_reports BEGIN SELECT RAISE(ABORT,'Imported reports are immutable'); END;
CREATE TRIGGER registry_generation_initial AFTER INSERT ON active_registry BEGIN UPDATE catalog_view SET registry_generation=max(registry_generation,(SELECT registry_version FROM registries WHERE registry_id=new.registry_id)); END;
CREATE VIEW effective_entries AS
 SELECT e.entry_id,e.document_json,e.record_sha256,
 '"cat1:'||v.registry_generation||':'||e.record_sha256||':'||e.entry_id||':0:active"' AS etag
 FROM registry_entries e JOIN active_registry a ON a.registry_id=e.registry_id CROSS JOIN catalog_view v;
CREATE VIEW runtime_catalog AS
 SELECT e.entry_id,coalesce(o.document_json,e.document_json) AS document_json,
 '"cat1:'||v.registry_generation||':'||e.record_sha256||':'||e.entry_id||':'||coalesce(o.revision,0)||':'||coalesce(o.state,'active')||'"' AS etag
 FROM effective_entries e CROSS JOIN catalog_view v LEFT JOIN overlays o ON o.entry_id=e.entry_id
 WHERE o.entry_id IS NULL OR o.state='active'
 UNION ALL
 SELECT o.entry_id,o.document_json,'"cat1:'||v.registry_generation||':local:'||o.entry_id||':'||o.revision||':'||o.state||'"'
 FROM overlays o CROSS JOIN catalog_view v WHERE o.imported_binding_id IS NULL AND o.state='active' AND NOT EXISTS(SELECT 1 FROM effective_entries e WHERE e.entry_id=o.entry_id);
CREATE TRIGGER active_local_collision_insert BEFORE INSERT ON active_registry WHEN EXISTS(SELECT 1 FROM overlays o JOIN registry_entries e ON e.entry_id=o.entry_id AND e.registry_id=new.registry_id WHERE o.imported_binding_id IS NULL AND o.state='active') BEGIN SELECT RAISE(ABORT,'Resolve local/import collision before publication'); END;
CREATE TRIGGER active_local_collision_update BEFORE UPDATE OF registry_id ON active_registry WHEN EXISTS(SELECT 1 FROM overlays o JOIN registry_entries e ON e.entry_id=o.entry_id AND e.registry_id=new.registry_id WHERE o.imported_binding_id IS NULL AND o.state='active') BEGIN SELECT RAISE(ABORT,'Resolve local/import collision before publication'); END;
CREATE TRIGGER local_collision_insert BEFORE INSERT ON overlays WHEN new.imported_binding_id IS NULL AND new.state='active' AND EXISTS(SELECT 1 FROM effective_entries e WHERE e.entry_id=new.entry_id) BEGIN SELECT RAISE(ABORT,'Use an imported overlay for an active imported identity'); END;
CREATE TRIGGER local_collision_update BEFORE UPDATE ON overlays WHEN new.imported_binding_id IS NULL AND new.state='active' AND EXISTS(SELECT 1 FROM effective_entries e WHERE e.entry_id=new.entry_id) BEGIN SELECT RAISE(ABORT,'Use an imported overlay for an active imported identity'); END;
PRAGMA user_version=3;
COMMIT;
