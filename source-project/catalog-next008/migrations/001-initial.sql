PRAGMA foreign_keys=ON;
PRAGMA user_version=1;
CREATE TABLE catalog_meta(key TEXT NOT NULL PRIMARY KEY,value TEXT NOT NULL);
CREATE TABLE sources(source_id TEXT NOT NULL PRIMARY KEY, path TEXT NOT NULL, url TEXT NOT NULL, revision TEXT NOT NULL, retrieved_at TEXT NOT NULL, sha256 TEXT NOT NULL, api_level INTEGER, channel TEXT NOT NULL, kind TEXT NOT NULL, license TEXT NOT NULL);
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
CREATE TABLE registries(registry_id TEXT NOT NULL PRIMARY KEY, registry_version INTEGER NOT NULL UNIQUE CHECK(registry_version>=1), schema_version TEXT NOT NULL, android_version TEXT NOT NULL, api_level INTEGER, source_set_hash TEXT NOT NULL, created_at TEXT NOT NULL, state TEXT NOT NULL CHECK(state IN ('staged','validated','rejected','published','retired')), validation_json TEXT NOT NULL,UNIQUE(registry_id,state));
CREATE TABLE registry_sources(registry_id TEXT NOT NULL REFERENCES registries,source_id TEXT NOT NULL REFERENCES sources,PRIMARY KEY(registry_id,source_id));
CREATE TABLE active_registry(singleton INTEGER PRIMARY KEY CHECK(singleton=1),registry_id TEXT NOT NULL REFERENCES registries,published_state TEXT GENERATED ALWAYS AS ('published') VIRTUAL,FOREIGN KEY(registry_id,published_state) REFERENCES registries(registry_id,state) DEFERRABLE INITIALLY DEFERRED);
CREATE TABLE overlays(entry_id TEXT NOT NULL PRIMARY KEY, imported_binding_id TEXT REFERENCES bindings, canonical_signature TEXT NOT NULL, revision INTEGER NOT NULL CHECK(revision>0), base_registry_id TEXT REFERENCES registries, base_record_hash TEXT, state TEXT NOT NULL CHECK(state IN ('active','suppressed','conflict','deleted')), document_json TEXT NOT NULL CHECK(json_valid(document_json)), created_at TEXT NOT NULL, updated_at TEXT NOT NULL,FOREIGN KEY(entry_id,revision) REFERENCES entry_history(entry_id,revision) DEFERRABLE INITIALLY DEFERRED,CHECK(imported_binding_id IS NULL OR entry_id=imported_binding_id),CHECK(state<>'suppressed' OR imported_binding_id IS NOT NULL),CHECK(state<>'deleted' OR imported_binding_id IS NULL));
CREATE INDEX overlay_lookup ON overlays(imported_binding_id,state);
CREATE UNIQUE INDEX overlay_one_import ON overlays(imported_binding_id) WHERE imported_binding_id IS NOT NULL;
CREATE TABLE entry_history(entry_id TEXT NOT NULL REFERENCES overlays, revision INTEGER NOT NULL, operation TEXT NOT NULL CHECK(operation IN ('create','update','suppress','delete','restore','rebase','conflict')), document_json TEXT NOT NULL CHECK(json_valid(document_json)), actor TEXT NOT NULL, occurred_at TEXT NOT NULL, PRIMARY KEY(entry_id,revision));
CREATE TRIGGER history_immutable_update BEFORE UPDATE ON entry_history BEGIN SELECT RAISE(ABORT,'History is append-only'); END;
CREATE TRIGGER history_immutable_delete BEFORE DELETE ON entry_history BEGIN SELECT RAISE(ABORT,'History is append-only'); END;
CREATE TABLE legacy_import_jobs(idempotency_key TEXT NOT NULL PRIMARY KEY,request_sha256 TEXT NOT NULL,registry_id TEXT REFERENCES registries,state TEXT NOT NULL CHECK(state IN ('staged','validated','rejected','published','retired')),result_json TEXT NOT NULL);
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

CREATE TRIGGER registry_state_transition BEFORE UPDATE OF state ON registries
 WHEN new.state<>old.state AND NOT ((old.state='staged' AND new.state IN ('validated','rejected')) OR (old.state='validated' AND new.state='published') OR (old.state='published' AND new.state='retired'))
 BEGIN SELECT RAISE(ABORT,'Invalid registry state transition'); END;

CREATE TABLE idempotency_records(principal TEXT NOT NULL,operation TEXT NOT NULL,idempotency_key TEXT NOT NULL,request_sha256 TEXT NOT NULL CHECK(length(request_sha256)=64),response_status INTEGER NOT NULL CHECK(response_status BETWEEN 100 AND 599),response_json TEXT NOT NULL CHECK(json_valid(response_json)),resource_id TEXT NOT NULL,PRIMARY KEY(principal,operation,idempotency_key));
CREATE TABLE registry_events(event_id TEXT NOT NULL PRIMARY KEY,actor TEXT NOT NULL,request_id TEXT NOT NULL,registry_id TEXT NOT NULL REFERENCES registries,kind TEXT NOT NULL,occurred_at TEXT NOT NULL,result_json TEXT NOT NULL CHECK(json_valid(result_json)));
CREATE TABLE registry_outbox(event_id TEXT NOT NULL PRIMARY KEY REFERENCES registry_events,affected_entry_ids_json TEXT NOT NULL CHECK(json_valid(affected_entry_ids_json)),delivered_at TEXT);
CREATE TRIGGER registry_events_no_update BEFORE UPDATE ON registry_events BEGIN SELECT RAISE(ABORT,'Registry events are append-only'); END;
CREATE TRIGGER registry_events_no_delete BEFORE DELETE ON registry_events BEGIN SELECT RAISE(ABORT,'Registry events are append-only'); END;
CREATE TRIGGER overlay_revision_update BEFORE UPDATE ON overlays WHEN new.revision<=old.revision BEGIN SELECT RAISE(ABORT,'Every overlay write increments revision'); END;
CREATE TRIGGER overlay_canonical_insert BEFORE INSERT ON overlays WHEN new.imported_binding_id IS NOT NULL AND new.canonical_signature<>(SELECT signature FROM bindings WHERE binding_id=new.imported_binding_id) BEGIN SELECT RAISE(ABORT,'Imported identity/signature mismatch'); END;
CREATE TRIGGER overlay_canonical_update BEFORE UPDATE ON overlays WHEN new.entry_id<>old.entry_id OR new.canonical_signature<>old.canonical_signature OR (new.imported_binding_id IS NOT NULL AND new.canonical_signature<>(SELECT signature FROM bindings WHERE binding_id=new.imported_binding_id)) BEGIN SELECT RAISE(ABORT,'Canonical identity cannot change'); END;

CREATE UNIQUE INDEX registries_one_published ON registries(state) WHERE state='published';
CREATE TRIGGER active_must_be_published_insert BEFORE INSERT ON active_registry WHEN (SELECT state FROM registries WHERE registry_id=new.registry_id) IS NOT 'published' BEGIN SELECT RAISE(ABORT,'Active registry must be published'); END;
CREATE TRIGGER active_must_be_published_update BEFORE UPDATE OF registry_id ON active_registry WHEN (SELECT state FROM registries WHERE registry_id=new.registry_id) IS NOT 'published' OR (new.registry_id<>old.registry_id AND (SELECT registry_version FROM registries WHERE registry_id=new.registry_id)<=(SELECT registry_version FROM registries WHERE registry_id=old.registry_id)) BEGIN SELECT RAISE(ABORT,'Publication requires a newer published registry'); END;
