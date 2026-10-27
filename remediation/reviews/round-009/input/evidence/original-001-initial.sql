PRAGMA foreign_keys=ON;
PRAGMA user_version=1;
CREATE TABLE catalog_meta(key TEXT PRIMARY KEY,value TEXT NOT NULL);
CREATE TABLE sources(source_id TEXT PRIMARY KEY, path TEXT NOT NULL, url TEXT NOT NULL, revision TEXT NOT NULL, retrieved_at TEXT NOT NULL, sha256 TEXT NOT NULL, api_level INTEGER NOT NULL, channel TEXT NOT NULL, kind TEXT NOT NULL, license TEXT NOT NULL);
CREATE TABLE bindings(binding_id TEXT PRIMARY KEY, signature TEXT NOT NULL UNIQUE, package TEXT NOT NULL, owner TEXT, kind TEXT NOT NULL, name TEXT NOT NULL, return_type TEXT, arguments_json TEXT, raw_signature TEXT NOT NULL, codec_status TEXT NOT NULL DEFAULT 'unknown');
CREATE INDEX binding_lookup ON bindings(package,owner,name,binding_id);
CREATE TABLE occurrences(binding_id TEXT NOT NULL REFERENCES bindings, source_id TEXT NOT NULL REFERENCES sources, line INTEGER NOT NULL, PRIMARY KEY(binding_id,source_id,line));
CREATE TABLE binding_declarations(binding_id TEXT NOT NULL,source_id TEXT NOT NULL,line INTEGER NOT NULL,document_json TEXT NOT NULL,PRIMARY KEY(binding_id,source_id,line),FOREIGN KEY(binding_id,source_id,line) REFERENCES occurrences);
CREATE TABLE permission_definitions(name TEXT NOT NULL, source_id TEXT NOT NULL REFERENCES sources, protection_level TEXT, attributes_json TEXT NOT NULL, PRIMARY KEY(name,source_id));
CREATE TABLE permission_relations(id INTEGER PRIMARY KEY,binding_id TEXT NOT NULL REFERENCES bindings,source_id TEXT NOT NULL REFERENCES sources,line INTEGER NOT NULL,annotation TEXT,logic TEXT NOT NULL CHECK(logic IN ('all','any','value','conditional','manual','explicit-none','unknown')),expression_json TEXT NOT NULL,raw_annotation TEXT);
CREATE INDEX permission_lookup ON permission_relations(binding_id);
CREATE TABLE aidl_schemas(source_id TEXT PRIMARY KEY REFERENCES sources, package TEXT, descriptor TEXT, declaration_kind TEXT, raw_schema TEXT NOT NULL, parse_status TEXT NOT NULL, transaction_metadata_json TEXT NOT NULL, codec_status TEXT NOT NULL);
CREATE TABLE issues(id INTEGER PRIMARY KEY, source_id TEXT REFERENCES sources,line INTEGER,category TEXT NOT NULL,reason TEXT NOT NULL,raw TEXT);
-- Contract 1.0.0: source records are append-only. Imports run in one transaction.
CREATE TRIGGER sources_immutable_update BEFORE UPDATE ON sources BEGIN SELECT RAISE(ABORT,'Imported sources are immutable'); END;
CREATE TRIGGER sources_immutable_delete BEFORE DELETE ON sources BEGIN SELECT RAISE(ABORT,'Imported sources are immutable'); END;
CREATE TRIGGER bindings_immutable_update BEFORE UPDATE ON bindings BEGIN SELECT RAISE(ABORT,'Imported bindings are immutable'); END;
CREATE TRIGGER bindings_immutable_delete BEFORE DELETE ON bindings BEGIN SELECT RAISE(ABORT,'Suppress imported records using a local overlay'); END;
CREATE TABLE registries(registry_id TEXT PRIMARY KEY, registry_version INTEGER NOT NULL UNIQUE, schema_version TEXT NOT NULL, android_version TEXT NOT NULL, api_level INTEGER NOT NULL, source_set_hash TEXT NOT NULL, created_at TEXT NOT NULL, status TEXT NOT NULL CHECK(status IN ('staged','published','retired')), validation_json TEXT NOT NULL);
CREATE TABLE registry_sources(registry_id TEXT REFERENCES registries,source_id TEXT REFERENCES sources,PRIMARY KEY(registry_id,source_id));
CREATE TABLE active_registry(singleton INTEGER PRIMARY KEY CHECK(singleton=1),registry_id TEXT NOT NULL REFERENCES registries);
CREATE TABLE overlays(entry_id TEXT PRIMARY KEY, imported_binding_id TEXT REFERENCES bindings, canonical_signature TEXT NOT NULL, revision INTEGER NOT NULL CHECK(revision>0), base_registry_id TEXT REFERENCES registries, base_record_hash TEXT, state TEXT NOT NULL CHECK(state IN ('active','suppressed','conflict','deleted')), document_json TEXT NOT NULL, created_at TEXT NOT NULL, updated_at TEXT NOT NULL);
CREATE INDEX overlay_lookup ON overlays(imported_binding_id,state);
CREATE UNIQUE INDEX overlay_one_import ON overlays(imported_binding_id) WHERE imported_binding_id IS NOT NULL;
CREATE TABLE entry_history(entry_id TEXT NOT NULL REFERENCES overlays, revision INTEGER NOT NULL, operation TEXT NOT NULL CHECK(operation IN ('create','update','suppress','delete','restore','rebase','conflict')), document_json TEXT NOT NULL, actor TEXT NOT NULL, occurred_at TEXT NOT NULL, PRIMARY KEY(entry_id,revision));
CREATE TRIGGER history_immutable_update BEFORE UPDATE ON entry_history BEGIN SELECT RAISE(ABORT,'History is append-only'); END;
CREATE TRIGGER history_immutable_delete BEFORE DELETE ON entry_history BEGIN SELECT RAISE(ABORT,'History is append-only'); END;
CREATE TABLE import_jobs(idempotency_key TEXT PRIMARY KEY,request_sha256 TEXT NOT NULL,registry_id TEXT REFERENCES registries,status TEXT NOT NULL CHECK(status IN ('staged','validated','rejected','published')),result_json TEXT NOT NULL);
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
