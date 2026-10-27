import pathlib
R=pathlib.Path(__file__).resolve().parent;C=R/'catalog'
p=C/'migrations/002-evidence-and-publication.sql';s=p.read_text()
s=s.replace('CREATE TABLE effective_entries(entry_id TEXT NOT NULL PRIMARY KEY REFERENCES bindings(binding_id), document_json TEXT NOT NULL CHECK(json_valid(document_json)), record_sha256 TEXT NOT NULL, etag TEXT NOT NULL);','CREATE TABLE registry_entries(registry_id TEXT NOT NULL REFERENCES registries,entry_id TEXT NOT NULL REFERENCES bindings(binding_id),document_json TEXT NOT NULL CHECK(json_valid(document_json)),record_sha256 TEXT NOT NULL,PRIMARY KEY(registry_id,entry_id));')
s=s.replace('effective_entries_no_update BEFORE UPDATE ON effective_entries','registry_entries_no_update BEFORE UPDATE ON registry_entries').replace('effective_entries_no_delete BEFORE DELETE ON effective_entries','registry_entries_no_delete BEFORE DELETE ON registry_entries')
anchor='CREATE VIEW runtime_catalog AS'
s=s.replace(anchor,'''CREATE TRIGGER registry_generation_switch AFTER UPDATE OF registry_id ON active_registry WHEN old.registry_id<>new.registry_id BEGIN UPDATE catalog_view SET registry_generation=registry_generation+1; END;
CREATE VIEW effective_entries AS
 SELECT e.entry_id,e.document_json,e.record_sha256,
 '\"cat1:'||v.registry_generation||':'||e.record_sha256||':'||e.entry_id||':0:active\"' AS etag
 FROM registry_entries e JOIN active_registry a ON a.registry_id=e.registry_id CROSS JOIN catalog_view v;
'''+anchor)
p.write_text(s)
p=C/'tooling/build_catalog.py';s=p.read_text();anchor=" source_by_id={s['source_id']:s for s in sources};symbols_by_value=collections.defaultdict(set);symbol_rows=[]"
replacement=""" set_hash=sha(js(sorted(s['source_id'] for s in sources)));registry_id=sha('registry:'+set_hash)
 # This private staging database is not published until every validation/export gate passes.
 db.execute('INSERT INTO registries VALUES (?,?,?,?,?,?,?,?,?)',(registry_id,1,'1.2.0','android-17.0.0_r1',37,set_hash,datetime.datetime.now(datetime.timezone.utc).isoformat(),'staged','{}'))
 db.execute('INSERT INTO active_registry VALUES (1,?)',(registry_id,))
"""+anchor
assert s.count(anchor)==1;s=s.replace(anchor,replacement)
s=s.replace("db.execute('INSERT INTO effective_entries VALUES (?,?,?,?)',(bid,content,digest,'\"cat1:1:'+digest+':'+bid+':0:active\"'))","db.execute('INSERT INTO registry_entries VALUES (?,?,?,?)',(registry_id,bid,content,digest))")
anchor=" db.execute('INSERT INTO registries VALUES (?,?,?,?,?,?,?,?,?)',(registry_id,1,'1.2.0','android-17.0.0_r1',37,set_hash,datetime.datetime.now(datetime.timezone.utc).isoformat(),'published',js(validation)))"
assert s.count(anchor)==1;s=s.replace(anchor," db.execute(\"UPDATE registries SET state='published',validation_json=? WHERE registry_id=?\",(js(validation),registry_id))")
s=s.replace("db.executemany('INSERT INTO registry_sources VALUES (?,?)',[(registry_id,s['source_id']) for s in sources]);db.execute('INSERT INTO active_registry VALUES (1,?)',(registry_id,))","db.executemany('INSERT INTO registry_sources VALUES (?,?)',[(registry_id,s['source_id']) for s in sources])")
p.write_text(s)
(C/'schema.sql').write_text('\n'.join(p.read_text() for p in sorted((C/'migrations').glob('*.sql'))))
