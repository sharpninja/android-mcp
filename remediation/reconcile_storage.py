import pathlib,re
R=pathlib.Path(__file__).resolve().parent;C=R/'catalog'
p=C/'bridge-config.sql';s=p.read_text()
s=s.replace('registry_revision INTEGER NOT NULL CHECK(registry_revision>=1));\nCREATE TABLE tool_binding_aliases','registry_revision INTEGER NOT NULL CHECK(registry_revision>=1),executor_kind TEXT NOT NULL DEFAULT \'native\' CHECK(executor_kind IN (\'native\',\'adb\',\'avalonia\')));\nCREATE TABLE tool_binding_aliases')
s=re.sub(r'CREATE TABLE raw_backend_grants[^\n]+\n','',s)
s=s.replace("'admitted','completed','failed','canceled','unknown'", "'admitted','dispatched','completed','failed','canceled','unknown'")
s=s.replace('completed_at INTEGER,idempotency_key TEXT);','completed_at INTEGER,idempotency_key TEXT,connection_profile_id TEXT REFERENCES connection_profiles,target_id TEXT REFERENCES target_app_profiles,backend_kind TEXT,dispatch_time INTEGER);')
a=s.index('CREATE TABLE connection_profiles');b=s.index('CREATE TABLE binding_availability',a)
s=s[:a]+'''CREATE TABLE connection_profiles(profile_id TEXT NOT NULL PRIMARY KEY,backend_kind TEXT NOT NULL CHECK(backend_kind IN ('self_adb','external_adb','avalonia_runtime')),display_name TEXT NOT NULL,enabled INTEGER NOT NULL DEFAULT 0 CHECK(enabled IN (0,1)),row_version INTEGER NOT NULL CHECK(row_version>=1),device_serial_or_binding TEXT,android_user_id INTEGER NOT NULL CHECK(android_user_id>=0),endpoint_metadata_json TEXT NOT NULL CHECK(json_valid(endpoint_metadata_json)),peer_fingerprint TEXT,verification_strength TEXT NOT NULL,secret_ciphertext BLOB,wrapping_key_alias TEXT,paired_at INTEGER,revoked_at INTEGER,CHECK((secret_ciphertext IS NULL)=(wrapping_key_alias IS NULL)));
CREATE TABLE target_app_profiles(target_id TEXT NOT NULL PRIMARY KEY,connection_profile_id TEXT NOT NULL REFERENCES connection_profiles ON DELETE RESTRICT,package_name TEXT NOT NULL,signing_identity TEXT,identity_verification_method TEXT NOT NULL,runtime_protocol TEXT,runtime_endpoint TEXT,server_identity TEXT,enabled INTEGER NOT NULL DEFAULT 0 CHECK(enabled IN (0,1)),row_version INTEGER NOT NULL CHECK(row_version>=1));
CREATE TABLE client_raw_grants(client_id TEXT NOT NULL REFERENCES clients ON DELETE CASCADE,connection_id TEXT NOT NULL REFERENCES connection_profiles,descriptor TEXT NOT NULL,code_min INTEGER NOT NULL CHECK(code_min>=0),code_max INTEGER NOT NULL CHECK(code_max>=code_min),flags_mask INTEGER NOT NULL CHECK(flags_mask>=0),contract_hash TEXT NOT NULL CHECK(length(contract_hash)=64),effect TEXT NOT NULL CHECK(effect IN ('deny','ask','allow')),constraints_json TEXT NOT NULL CHECK(json_valid(constraints_json)),row_version INTEGER NOT NULL CHECK(row_version>=1),PRIMARY KEY(client_id,connection_id,descriptor));
'''+s[b:]
s=s.replace('target_key TEXT NOT NULL REFERENCES targets','target_key TEXT NOT NULL REFERENCES target_app_profiles(target_id)')
a=s.index('CREATE TABLE service_preferences');b=s.index('CREATE TRIGGER audit_no_update',a)
s=s[:a]+'''CREATE TABLE resource_grants(handle_id TEXT NOT NULL PRIMARY KEY,client_id TEXT NOT NULL REFERENCES clients ON DELETE CASCADE,exact_uri_ciphertext BLOB NOT NULL,key_alias TEXT NOT NULL,modes TEXT NOT NULL CHECK(modes IN ('read','write','read_write')),enabled INTEGER NOT NULL CHECK(enabled IN (0,1)),revision INTEGER NOT NULL CHECK(revision>=1),created_at INTEGER NOT NULL);
CREATE TABLE bridge_config(singleton_id INTEGER PRIMARY KEY CHECK(singleton_id=1),row_version INTEGER NOT NULL CHECK(row_version>=1),desired_state TEXT NOT NULL CHECK(desired_state IN ('running','stopped')),start_on_boot INTEGER NOT NULL DEFAULT 0 CHECK(start_on_boot IN (0,1)),last_stop_reason TEXT,fgs_type TEXT NOT NULL,transport_profile TEXT NOT NULL CHECK(transport_profile IN ('paired_tls','reduced_http')),loopback_address TEXT NOT NULL,port_preference INTEGER NOT NULL CHECK(port_preference BETWEEN 0 AND 65535),server_identity_key_alias TEXT NOT NULL,retention_days INTEGER NOT NULL CHECK(retention_days>=0),audit_size_cap INTEGER NOT NULL CHECK(audit_size_cap>0));
CREATE TABLE client_logging_config(client_id TEXT NOT NULL PRIMARY KEY REFERENCES clients ON DELETE CASCADE,mode TEXT NOT NULL CHECK(mode IN ('off','metadata','diagnostic')),paused INTEGER NOT NULL CHECK(paused IN (0,1)),logging_revision INTEGER NOT NULL CHECK(logging_revision>=1),retention_seconds INTEGER NOT NULL CHECK(retention_seconds>=0),byte_quota INTEGER NOT NULL CHECK(byte_quota>=0),record_byte_cap INTEGER NOT NULL CHECK(record_byte_cap>=0),diagnostic_expires_at INTEGER,diagnostic_boot_id TEXT,redaction_profile_version INTEGER NOT NULL CHECK(redaction_profile_version>=1),updated_at INTEGER NOT NULL);
CREATE TABLE client_call_logs(log_id TEXT NOT NULL PRIMARY KEY,client_id TEXT NOT NULL REFERENCES clients ON DELETE CASCADE,call_id TEXT NOT NULL,tool_id TEXT NOT NULL REFERENCES tools,backend_kind TEXT NOT NULL,started_at INTEGER NOT NULL,finished_at INTEGER,decision_code TEXT NOT NULL,outcome_code TEXT NOT NULL,policy_revision INTEGER NOT NULL CHECK(policy_revision>=1),logging_revision INTEGER NOT NULL CHECK(logging_revision>=1),expires_at INTEGER NOT NULL,size_bytes INTEGER NOT NULL CHECK(size_bytes>=0),payload_ciphertext BLOB,key_alias TEXT,CHECK((payload_ciphertext IS NULL)=(key_alias IS NULL)));
CREATE TABLE logging_counters(client_id TEXT NOT NULL PRIMARY KEY REFERENCES clients ON DELETE CASCADE,optional_dropped_count INTEGER NOT NULL CHECK(optional_dropped_count>=0),quota_dropped_count INTEGER NOT NULL CHECK(quota_dropped_count>=0),last_drop_at INTEGER);
CREATE INDEX call_logs_client_time ON client_call_logs(client_id,started_at);
CREATE INDEX call_logs_expiry ON client_call_logs(expires_at);
CREATE INDEX call_logs_client_expiry ON client_call_logs(client_id,expires_at);
CREATE INDEX credentials_client_state ON credentials(client_id,state);
CREATE INDEX rules_by_tool ON client_tool_rules(tool_id);
CREATE INDEX executions_client_time ON executions(client_id,admitted_at);
CREATE INDEX executions_connection_time ON executions(connection_profile_id,admitted_at);
CREATE INDEX targets_connection_enabled ON target_app_profiles(connection_profile_id,enabled);
CREATE INDEX resource_client_enabled ON resource_grants(client_id,enabled);
CREATE INDEX audit_client_time ON audit_events(client_id,occurred_at);
CREATE INDEX audit_time ON audit_events(occurred_at);
CREATE INDEX outbox_delivery ON change_outbox(delivered_at,change_id);
CREATE UNIQUE INDEX execution_idempotency ON executions(client_id,tool_id,idempotency_key) WHERE idempotency_key IS NOT NULL;
'''+s[b:]
p.write_text(s)

p=C/'migrations/001-initial.sql';s=p.read_text().replace('api_level INTEGER NOT NULL','api_level INTEGER')
s=s.replace("CREATE TABLE overlays(entry_id TEXT NOT NULL PRIMARY KEY, imported_binding_id TEXT REFERENCES bindings,", "CREATE TABLE overlays(entry_id TEXT NOT NULL PRIMARY KEY, imported_binding_id TEXT REFERENCES bindings,")
s=s.replace('updated_at TEXT NOT NULL);','updated_at TEXT NOT NULL,CHECK(imported_binding_id IS NULL OR entry_id=imported_binding_id));')
# Retain unattested legacy import-job receipts separately from the authoritative API store.
s=s.replace('CREATE TABLE import_jobs(', 'CREATE TABLE legacy_import_jobs(')
s+='''
CREATE TABLE idempotency_records(principal TEXT NOT NULL,operation TEXT NOT NULL,idempotency_key TEXT NOT NULL,request_sha256 TEXT NOT NULL CHECK(length(request_sha256)=64),response_status INTEGER NOT NULL CHECK(response_status BETWEEN 100 AND 599),response_json TEXT NOT NULL CHECK(json_valid(response_json)),resource_id TEXT NOT NULL,PRIMARY KEY(principal,operation,idempotency_key));
CREATE TABLE registry_events(event_id TEXT NOT NULL PRIMARY KEY,actor TEXT NOT NULL,request_id TEXT NOT NULL,registry_id TEXT NOT NULL REFERENCES registries,kind TEXT NOT NULL,occurred_at TEXT NOT NULL,result_json TEXT NOT NULL CHECK(json_valid(result_json)));
CREATE TABLE registry_outbox(event_id TEXT NOT NULL PRIMARY KEY REFERENCES registry_events,affected_entry_ids_json TEXT NOT NULL CHECK(json_valid(affected_entry_ids_json)),delivered_at TEXT);
CREATE TRIGGER registry_events_no_update BEFORE UPDATE ON registry_events BEGIN SELECT RAISE(ABORT,'Registry events are append-only'); END;
CREATE TRIGGER registry_events_no_delete BEFORE DELETE ON registry_events BEGIN SELECT RAISE(ABORT,'Registry events are append-only'); END;
CREATE TRIGGER overlay_revision_update BEFORE UPDATE ON overlays WHEN new.revision<=old.revision BEGIN SELECT RAISE(ABORT,'Every overlay write increments revision'); END;
CREATE TRIGGER overlay_canonical_insert BEFORE INSERT ON overlays WHEN new.imported_binding_id IS NOT NULL AND new.canonical_signature<>(SELECT signature FROM bindings WHERE binding_id=new.imported_binding_id) BEGIN SELECT RAISE(ABORT,'Imported identity/signature mismatch'); END;
CREATE TRIGGER overlay_canonical_update BEFORE UPDATE ON overlays WHEN new.entry_id<>old.entry_id OR new.canonical_signature<>old.canonical_signature OR (new.imported_binding_id IS NOT NULL AND new.canonical_signature<>(SELECT signature FROM bindings WHERE binding_id=new.imported_binding_id)) BEGIN SELECT RAISE(ABORT,'Canonical identity cannot change'); END;
'''
p.write_text(s)
(C/'schema.sql').write_text('\n'.join(p.read_text() for p in sorted((C/'migrations').glob('*.sql'))))
p=C/'tooling/migrate_v3.py';s=p.read_text()
s=s.replace("                cols=','.join", "                destination_table='legacy_import_jobs' if table=='import_jobs' else table\n                cols=','.join")
s=s.replace("new.execute('INSERT INTO \"'+table+'\"('+cols+", "new.execute('INSERT INTO \"'+destination_table+'\"('+cols+")
s=s.replace("actual=new.execute('SELECT count(*) FROM \"'+table+'\"')", "actual=new.execute('SELECT count(*) FROM \"'+('legacy_import_jobs' if table=='import_jobs' else table)+'\"')")
p.write_text(s)
p=C/'tests/test_contract_models.py';s=p.read_text().replace("INSERT INTO client_logging_policy VALUES('fixture','full-raw',1,1,1,1)","INSERT INTO client_logging_config(client_id,mode,paused,logging_revision,retention_seconds,byte_quota,record_byte_cap,redaction_profile_version,updated_at) VALUES('fixture','full-raw',0,1,1,1,1,1,0)");p.write_text(s)
p=R/'design/Android MCP Bridge Design.md';s=p.read_text().replace('documented raw 1 to 255','documented raw 0 to 255').replace('catalog_snapshots','registries / registry_sources / registry_entries').replace('catalog_overlays','overlays').replace('catalog_change_events','entry_history / registry_events / registry_outbox').replace('raw_backend_grants keyed by backend/service','client_raw_grants keyed by connection/descriptor and bounded transaction codes/flags')
s=s.replace('state CHECK admitted/completed/failed/canceled/unknown','state CHECK admitted/dispatched/completed/failed/canceled/unknown')
s=s.replace('| POST /v1/catalog/registries/{registry_id}/publish |','| GET /v1/catalog/registries/{registry_id} | Registry metadata, state and validation evidence. |\n| POST /v1/catalog/registries/{registry_id}/publish |')
s+='''

The raw grant DDL is client_raw_grants(client_id, connection_id, descriptor, code_min, code_max, flags_mask, contract_hash, effect, constraints_json, row_version). The JSON constraint schema is closed: exact connection/descriptor/target/display handles, inclusive nonnegative code range, and allowed flag bits; empty target/handle sets deny, and there is no implicit wildcard. Resolve known transactions before checking raw grants: a raw grant can never substitute for a known binding rule. Unknown raw access is a broad authority with explicitly limited observability; ask/allow is available only within every stated bound.
'''
p.write_text(s)
