PRAGMA foreign_keys=ON;
CREATE TABLE IF NOT EXISTS metadata_state(
 singleton INTEGER PRIMARY KEY CHECK(singleton=1), generation INTEGER NOT NULL CHECK(generation>=0)
);
INSERT OR IGNORE INTO metadata_state VALUES(1,0);
CREATE TABLE IF NOT EXISTS metadata_bases(
 kind TEXT NOT NULL CHECK(kind IN('callback-descriptor','functional-group')),
 resource_id TEXT NOT NULL,
 content_hash TEXT NOT NULL CHECK(length(content_hash)=64 AND content_hash NOT GLOB '*[^0-9a-f]*'),
 document_json TEXT NOT NULL CHECK(json_valid(document_json)),
 PRIMARY KEY(kind,resource_id,content_hash)
);
CREATE TABLE IF NOT EXISTS metadata_revisions(
 kind TEXT NOT NULL CHECK(kind IN('callback-descriptor','functional-group')),
 resource_id TEXT NOT NULL,
 revision INTEGER NOT NULL CHECK(revision>=1),
 origin TEXT NOT NULL CHECK(origin IN('imported','local','overlay')),
 state TEXT NOT NULL CHECK(state IN('active','suppressed','deleted','conflict')),
 base_hash TEXT,
 content_hash TEXT NOT NULL CHECK(length(content_hash)=64 AND content_hash NOT GLOB '*[^0-9a-f]*'),
 document_json TEXT NOT NULL CHECK(json_valid(document_json)),
 reason TEXT NOT NULL CHECK(length(trim(reason))>0),
 PRIMARY KEY(kind,resource_id,revision),
 FOREIGN KEY(kind,resource_id,base_hash) REFERENCES metadata_bases(kind,resource_id,content_hash),
 CHECK(json_type(document_json,'$.schema_version') IS 'text' AND json_extract(document_json,'$.schema_version')='1.0.0'),
 CHECK((kind='functional-group' AND json_type(document_json,'$.group_id') IS 'text' AND json_extract(document_json,'$.group_id')=resource_id)
    OR (kind='callback-descriptor' AND json_type(document_json,'$.callback_id') IS 'text' AND json_extract(document_json,'$.callback_id')=resource_id)),
 CHECK(origin<>'imported' OR (base_hash IS NOT NULL AND base_hash=content_hash))
);
CREATE TABLE IF NOT EXISTS metadata_heads(
 kind TEXT NOT NULL,resource_id TEXT NOT NULL,revision INTEGER NOT NULL CHECK(revision>=1),
 PRIMARY KEY(kind,resource_id),
 FOREIGN KEY(kind,resource_id,revision) REFERENCES metadata_revisions(kind,resource_id,revision)
);
CREATE TRIGGER IF NOT EXISTS metadata_revision_immutable_update BEFORE UPDATE ON metadata_revisions BEGIN SELECT RAISE(ABORT,'Immutable metadata revision');END;
CREATE TRIGGER IF NOT EXISTS metadata_revision_immutable_delete BEFORE DELETE ON metadata_revisions BEGIN SELECT RAISE(ABORT,'Immutable metadata revision');END;
CREATE TRIGGER IF NOT EXISTS metadata_base_immutable_update BEFORE UPDATE ON metadata_bases BEGIN SELECT RAISE(ABORT,'Immutable imported metadata');END;
CREATE TRIGGER IF NOT EXISTS metadata_base_immutable_delete BEFORE DELETE ON metadata_bases BEGIN SELECT RAISE(ABORT,'Immutable imported metadata');END;
CREATE TRIGGER IF NOT EXISTS metadata_head_initial BEFORE INSERT ON metadata_heads WHEN NEW.revision<>1 BEGIN SELECT RAISE(ABORT,'Initial metadata revision must be1');END;
CREATE TRIGGER IF NOT EXISTS metadata_head_cas BEFORE UPDATE ON metadata_heads WHEN NEW.kind<>OLD.kind OR NEW.resource_id<>OLD.resource_id OR NEW.revision<>OLD.revision+1 BEGIN SELECT RAISE(ABORT,'Metadata head requires next revision');END;
CREATE TRIGGER IF NOT EXISTS metadata_head_no_delete BEFORE DELETE ON metadata_heads BEGIN SELECT RAISE(ABORT,'Retain metadata tombstone/history');END;
CREATE TRIGGER IF NOT EXISTS metadata_head_generation_insert AFTER INSERT ON metadata_heads BEGIN UPDATE metadata_state SET generation=generation+1 WHERE singleton=1;END;
CREATE TRIGGER IF NOT EXISTS metadata_head_generation_update AFTER UPDATE ON metadata_heads BEGIN UPDATE metadata_state SET generation=generation+1 WHERE singleton=1;END;

