PRAGMA foreign_keys=ON;
PRAGMA user_version=1;
CREATE TABLE catalog_meta(key TEXT NOT NULL PRIMARY KEY,value TEXT NOT NULL);
CREATE TABLE sources(source_id TEXT NOT NULL PRIMARY KEY, path TEXT NOT NULL, url TEXT NOT NULL, revision TEXT NOT NULL, retrieved_at TEXT NOT NULL, sha256 TEXT NOT NULL, api_level INTEGER NOT NULL, channel TEXT NOT NULL, kind TEXT NOT NULL, license TEXT NOT NULL);
CREATE TABLE bindings(binding_id TEXT NOT NULL PRIMARY KEY, signature TEXT NOT NULL UNIQUE, package TEXT NOT NULL, owner TEXT, kind TEXT NOT NULL, name TEXT NOT NULL, return_type TEXT, arguments_json TEXT, raw_signature TEXT NOT NULL, codec_status TEXT NOT NULL DEFAULT 'unknown');
CREATE INDEX binding_lookup ON bindings(package,owner,name,binding_id);
CREATE TABLE occurrences(binding_id TEXT NOT NULL REFERENCES bindings, source_id TEXT NOT NULL REFERENCES sources, line INTEGER NOT NULL, PRIMARY KEY(binding_id,source_id,line));
CREATE TABLE binding_declarations(binding_id TEXT NOT NULL,source_id TEXT NOT NULL,line INTEGER NOT NULL,document_json TEXT NOT NULL,PRIMARY KEY(binding_id,source_id,line),FOREIGN KEY(binding_id,source_id,line) REFERENCES occurrences);
CREATE TABLE permission_definitions(name TEXT NOT NULL, source_id TEXT NOT NULL REFERENCES sources, protection_level TEXT, attributes_json TEXT NOT NULL, PRIMARY KEY(name,source_id));
CREATE TABLE permission_relations(id INTEGER PRIMARY KEY,binding_id TEXT NOT NULL REFERENCES bindings,source_id TEXT NOT NULL REFERENCES sources,line INTEGER NOT NULL,annotation TEXT,logic TEXT NOT NULL CHECK(logic IN ('all','any','permission','access','conditional','manual','explicit-none','unknown')),expression_json TEXT NOT NULL,raw_annotation TEXT);
CREATE INDEX permission_lookup ON permission_relations(binding_id);
CREATE TABLE aidl_schemas(source_id TEXT NOT NULL PRIMARY KEY REFERENCES sources, package TEXT, descriptor TEXT, declaration_kind TEXT, raw_schema TEXT NOT NULL, parse_status TEXT NOT NULL, transaction_metadata_json TEXT NOT NULL, codec_status TEXT NOT NULL);
CREATE TABLE issues(id INTEGER PRIMARY KEY, source_id TEXT REFERENCES sources,line INTEGER,category TEXT NOT NULL,reason TEXT NOT NULL,raw TEXT);
-- Contract 1.0.0: source records are append-only. Imports run in one transaction.
CREATE TRIGGER sources_immutable_update BEFORE UPDATE ON sources BEGIN SELECT RAISE(ABORT,'Imported sources are immutable'); END;
CREATE TRIGGER sources_immutable_delete BEFORE DELETE ON sources BEGIN SELECT RAISE(ABORT,'Imported sources are immutable'); END;
CREATE TRIGGER bindings_immutable_update BEFORE UPDATE ON bindings BEGIN SELECT RAISE(ABORT,'Imported bindings are immutable'); END;
CREATE TRIGGER bindings_immutable_delete BEFORE DELETE ON bindings BEGIN SELECT RAISE(ABORT,'Suppress imported records using a local overlay'); END;
CREATE TABLE registries(registry_id TEXT NOT NULL PRIMARY KEY, registry_version INTEGER NOT NULL UNIQUE, schema_version TEXT NOT NULL, android_version TEXT NOT NULL, api_level INTEGER NOT NULL, source_set_hash TEXT NOT NULL, created_at TEXT NOT NULL, state TEXT NOT NULL CHECK(state IN ('staged','validated','rejected','published','retired')), validation_json TEXT NOT NULL);
CREATE TABLE registry_sources(registry_id TEXT NOT NULL REFERENCES registries,source_id TEXT NOT NULL REFERENCES sources,PRIMARY KEY(registry_id,source_id));
CREATE TABLE active_registry(singleton INTEGER PRIMARY KEY CHECK(singleton=1),registry_id TEXT NOT NULL REFERENCES registries);
CREATE TABLE overlays(entry_id TEXT NOT NULL PRIMARY KEY, imported_binding_id TEXT REFERENCES bindings, canonical_signature TEXT NOT NULL, revision INTEGER NOT NULL CHECK(revision>0), base_registry_id TEXT REFERENCES registries, base_record_hash TEXT, state TEXT NOT NULL CHECK(state IN ('active','suppressed','conflict','deleted')), document_json TEXT NOT NULL, created_at TEXT NOT NULL, updated_at TEXT NOT NULL);
CREATE INDEX overlay_lookup ON overlays(imported_binding_id,state);
CREATE UNIQUE INDEX overlay_one_import ON overlays(imported_binding_id) WHERE imported_binding_id IS NOT NULL;
CREATE TABLE entry_history(entry_id TEXT NOT NULL REFERENCES overlays, revision INTEGER NOT NULL, operation TEXT NOT NULL CHECK(operation IN ('create','update','suppress','delete','restore','rebase','conflict')), document_json TEXT NOT NULL, actor TEXT NOT NULL, occurred_at TEXT NOT NULL, PRIMARY KEY(entry_id,revision));
CREATE TRIGGER history_immutable_update BEFORE UPDATE ON entry_history BEGIN SELECT RAISE(ABORT,'History is append-only'); END;
CREATE TRIGGER history_immutable_delete BEFORE DELETE ON entry_history BEGIN SELECT RAISE(ABORT,'History is append-only'); END;
CREATE TABLE import_jobs(idempotency_key TEXT NOT NULL PRIMARY KEY,request_sha256 TEXT NOT NULL,registry_id TEXT REFERENCES registries,state TEXT NOT NULL CHECK(state IN ('staged','validated','rejected','published','retired')),result_json TEXT NOT NULL);
-- Bounded effective-view readers select current registry then join occurrences,
-- registry_sources and overlays. They MUST exclude suppressed/deleted/conflict
-- rows for invocation discovery and reject unknown semantic extensions.

CREATE TRIGGER occurrences_immutable_update BEFORE UPDATE ON occurrences BEGIN SELECT RAISE(ABORT,'Imported source records are append-only'); END;

CREATE TRIGGER occurrences_immutable_delete BEFORE DELETE ON occurrences BEGIN SELECT RAISE(ABORT,'Imported source records are append-only'); END;

CREATE TRIGGER binding_declarations_immutable_update BEFORE UPDATE ON binding_declarations BEGIN SELECT RAISE(ABORT,'Imported source records are append-only'); END;

CREATE TRIGGER binding_declarations_immutable_delete BEFORE DELETE ON binding_declarations BEGIN SELECT RAISE(ABORT,'Imported source records are append-only'); END;

CREATE TRIGGER permission_definitions_immutable_update BEFORE UPDATE ON permission_definitions BEGIN SELECT RAISE(ABORT,'Imported source records are append-only'); END;

CREATE TRIGGER permission_definitions_immutable_delete BEFORE DELETE ON permission_definitions BEGIN SELECT RAISE(ABORT,'Imported source records are append-only'); END;

CREATE TRIGGER permission_relations_immutable_update BEFORE UPDATE ON permission_relations BEGIN SELECT RAISE(ABORT,'Imported source records are append-only'); END;

CREATE TRIGGER permission_relations_immutable_delete BEFORE DELETE ON permission_relations BEGIN SELECT RAISE(ABORT,'Imported source records are append-only'); END;

CREATE TRIGGER aidl_schemas_immutable_update BEFORE UPDATE ON aidl_schemas BEGIN SELECT RAISE(ABORT,'Imported source records are append-only'); END;

CREATE TRIGGER aidl_schemas_immutable_delete BEFORE DELETE ON aidl_schemas BEGIN SELECT RAISE(ABORT,'Imported source records are append-only'); END;

CREATE TRIGGER issues_immutable_update BEFORE UPDATE ON issues BEGIN SELECT RAISE(ABORT,'Imported source records are append-only'); END;

CREATE TRIGGER issues_immutable_delete BEFORE DELETE ON issues BEGIN SELECT RAISE(ABORT,'Imported source records are append-only'); END;

CREATE TRIGGER registry_sources_immutable_update BEFORE UPDATE ON registry_sources BEGIN SELECT RAISE(ABORT,'Imported source records are append-only'); END;

CREATE TRIGGER registry_sources_immutable_delete BEFORE DELETE ON registry_sources BEGIN SELECT RAISE(ABORT,'Imported source records are append-only'); END;

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
CREATE TABLE effective_entries(entry_id TEXT NOT NULL PRIMARY KEY REFERENCES bindings(binding_id), document_json TEXT NOT NULL CHECK(json_valid(document_json)), record_sha256 TEXT NOT NULL, etag TEXT NOT NULL);
CREATE TRIGGER source_details_no_update BEFORE UPDATE ON source_details BEGIN SELECT RAISE(ABORT,'Imported evidence is immutable'); END;
CREATE TRIGGER source_details_no_delete BEFORE DELETE ON source_details BEGIN SELECT RAISE(ABORT,'Imported evidence is immutable'); END;
CREATE TRIGGER annotation_evidence_no_update BEFORE UPDATE ON annotation_evidence BEGIN SELECT RAISE(ABORT,'Imported evidence is immutable'); END;
CREATE TRIGGER annotation_evidence_no_delete BEFORE DELETE ON annotation_evidence BEGIN SELECT RAISE(ABORT,'Imported evidence is immutable'); END;
CREATE TRIGGER permission_symbols_no_update BEFORE UPDATE ON permission_symbols BEGIN SELECT RAISE(ABORT,'Imported evidence is immutable'); END;
CREATE TRIGGER permission_symbols_no_delete BEFORE DELETE ON permission_symbols BEGIN SELECT RAISE(ABORT,'Imported evidence is immutable'); END;
CREATE TRIGGER effective_entries_no_update BEFORE UPDATE ON effective_entries BEGIN SELECT RAISE(ABORT,'Publish a new registry instead'); END;
CREATE TRIGGER effective_entries_no_delete BEFORE DELETE ON effective_entries BEGIN SELECT RAISE(ABORT,'Publish a new registry instead'); END;
-- ETag composition is identical for base, overlay, tombstone and history CAS.
CREATE TABLE catalog_view(singleton INTEGER PRIMARY KEY CHECK(singleton=1),registry_generation INTEGER NOT NULL CHECK(registry_generation>0),overlay_generation INTEGER NOT NULL CHECK(overlay_generation>=0));
INSERT INTO catalog_view VALUES(1,1,0);
CREATE TRIGGER overlay_generation_insert AFTER INSERT ON overlays BEGIN UPDATE catalog_view SET overlay_generation=overlay_generation+1; END;
CREATE TRIGGER overlay_generation_update AFTER UPDATE ON overlays BEGIN UPDATE catalog_view SET overlay_generation=overlay_generation+1; END;
CREATE TRIGGER overlay_generation_delete AFTER DELETE ON overlays BEGIN UPDATE catalog_view SET overlay_generation=overlay_generation+1; END;
CREATE VIEW runtime_catalog AS
 SELECT e.entry_id,coalesce(o.document_json,e.document_json) AS document_json,
 '"cat1:'||v.registry_generation||':'||e.record_sha256||':'||e.entry_id||':'||coalesce(o.revision,0)||':'||coalesce(o.state,'active')||'"' AS etag
 FROM effective_entries e CROSS JOIN catalog_view v LEFT JOIN overlays o ON o.imported_binding_id=e.entry_id
 WHERE o.entry_id IS NULL OR o.state='active'
 UNION ALL
 SELECT o.entry_id,o.document_json,'"cat1:'||v.registry_generation||':local:'||o.entry_id||':'||o.revision||':'||o.state||'"'
 FROM overlays o CROSS JOIN catalog_view v WHERE o.imported_binding_id IS NULL AND o.state='active';
PRAGMA user_version=3;
COMMIT;
