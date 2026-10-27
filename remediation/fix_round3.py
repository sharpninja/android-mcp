import pathlib,json,re
R=pathlib.Path(__file__).resolve().parent;C=R/'catalog'
p=C/'tooling/build_catalog.py';s=p.read_text()
old="""     bid=sha('java:'+canonical) if canonical else None
     if not sep:
      candidates=[sha('java:'+name) for name in (owner,'package:'+owner)]
      candidates=[candidate for candidate in candidates if db.execute('SELECT 1 FROM bindings WHERE binding_id=?',(candidate,)).fetchone()]
      bid=candidates[0] if len(candidates)==1 else None
     if bid and not db.execute('SELECT 1 FROM bindings WHERE binding_id=?',(bid,)).fetchone():bid=None"""
new="""     target_match_kind='exact';matched_canonical=canonical
     candidates=[canonical] if canonical else []
     if sep and canonical and '(' in canonical:
      head,args=canonical.split('(',1)
      short=re.sub(r'(?<![\\w.$])java\\.lang\\.', '',args)
      if short!=args:candidates.append(head+'('+short)
     elif not sep:candidates=[owner,'package:'+owner]
     matches=[(candidate,sha('java:'+candidate)) for candidate in dict.fromkeys(candidates) if db.execute('SELECT 1 FROM bindings WHERE binding_id=?',(sha('java:'+candidate),)).fetchone()]
     bid=None
     if len(matches)==1:
      matched_canonical,bid=matches[0]
      if matched_canonical!=canonical:target_match_kind='java-lang-spelling-equivalence' if sep else 'package-namespace'
     else:target_match_kind='unresolved-or-ambiguous'"""
assert old in s;s=s.replace(old,new)
s=s.replace("annotation=a,target_resolution='exact' if bid else 'unknown',", "annotation=a,target_resolution='exact' if bid else 'unknown',target_match_kind=target_match_kind,matched_canonical_signature=matched_canonical if bid else None,")
s=s.replace(" db.execute('INSERT INTO active_registry VALUES (1,?)',(registry_id,))\n",'')
s=s.replace("counts={name:db.execute('SELECT count(*) FROM '+name).fetchone()[0]", "counts={name:db.execute('SELECT count(*) FROM '+('registry_entries' if name=='effective_entries' else name)).fetchone()[0]")
s=s.replace("for bid,document in db.execute('SELECT entry_id,document_json FROM effective_entries')", "for bid,document in db.execute('SELECT entry_id,document_json FROM registry_entries WHERE registry_id=?',(registry_id,))")
s=s.replace("db.execute(\"UPDATE registries SET state='published' WHERE registry_id=?\",(registry_id,))", "db.execute(\"UPDATE registries SET state='published' WHERE registry_id=?\",(registry_id,))\n db.execute('INSERT INTO active_registry(singleton,registry_id) VALUES (1,?)',(registry_id,))")
s=s.replace("('entry_contract_version','1.0.0')", "('entry_contract_version','1.0.0'),('rebuilt_with_corrected_extractor','true')")
# Preserve absence evidence alongside positive declarations, never infer explicit-none.
anchor="  doc['extensions']['occurrence_count']=len(rows);content=js(doc);digest=sha(content)"
replacement="""  absence=[a for a in expressions if a['op']=='unknown' and a.get('reason','').startswith('No declared recognized')]
  if len(known)>1 or (known and absence):
   evidence_variants={}
   for expression,source_id,line,annotation in db.execute('SELECT expression_json,source_id,line,annotation FROM permission_relations WHERE binding_id=?',(bid,)):
    node=json.loads(expression);key=js(semantic(node))
    variant=evidence_variants.setdefault(key,dict(node,**{'x-occurrences':[]}))
    variant['x-occurrences'].append(dict(source_id=source_id,line=line,annotation=annotation))
    if node['op']=='unknown' and node.get('reason','').startswith('No declared recognized'):variant['x-absence-of-annotation']=True
   doc['extensions']['permission_variants']=list(evidence_variants.values())
"""+anchor
assert anchor in s;s=s.replace(anchor,replacement);p.write_text(s)

p=C/'tooling/export_permissions.py';s=p.read_text().replace("schema_version='1.2.0'", "schema_version='1.1.0'")
s=s.replace('import pathlib,sqlite3,json,hashlib,sys','import pathlib,sqlite3,json,hashlib,sys,jsonschema')
s=s.replace("  names=[", "  validator=jsonschema.Draft202012Validator(json.loads((pathlib.Path(__file__).resolve().parents[1]/'contracts/PermissionSummary.schema.json').read_text()))\n  names=[")
s=s.replace("    f.write(json.dumps(doc", "    validator.validate(doc)\n    f.write(json.dumps(doc")
p.write_text(s)

p=C/'migrations/001-initial.sql';s=p.read_text()
s=s.replace('validation_json TEXT NOT NULL);', 'validation_json TEXT NOT NULL,UNIQUE(registry_id,state));')
s=s.replace('CREATE TABLE active_registry(singleton INTEGER PRIMARY KEY CHECK(singleton=1),registry_id TEXT NOT NULL REFERENCES registries);',"CREATE TABLE active_registry(singleton INTEGER PRIMARY KEY CHECK(singleton=1),registry_id TEXT NOT NULL REFERENCES registries,published_state TEXT GENERATED ALWAYS AS ('published') VIRTUAL,FOREIGN KEY(registry_id,published_state) REFERENCES registries(registry_id,state) DEFERRABLE INITIALLY DEFERRED);")
s=s.replace('document_json TEXT NOT NULL, created_at TEXT NOT NULL', 'document_json TEXT NOT NULL CHECK(json_valid(document_json)), created_at TEXT NOT NULL')
s=s.replace('updated_at TEXT NOT NULL,CHECK(imported_binding_id', 'updated_at TEXT NOT NULL,FOREIGN KEY(entry_id,revision) REFERENCES entry_history(entry_id,revision) DEFERRABLE INITIALLY DEFERRED,CHECK(imported_binding_id')
s=s.replace('document_json TEXT NOT NULL, actor TEXT NOT NULL', 'document_json TEXT NOT NULL CHECK(json_valid(document_json)), actor TEXT NOT NULL')
s+='''
CREATE UNIQUE INDEX registries_one_published ON registries(state) WHERE state='published';
CREATE TRIGGER active_must_be_published_insert BEFORE INSERT ON active_registry WHEN (SELECT state FROM registries WHERE registry_id=new.registry_id) IS NOT 'published' BEGIN SELECT RAISE(ABORT,'Active registry must be published'); END;
CREATE TRIGGER active_must_be_published_update BEFORE UPDATE OF registry_id ON active_registry WHEN (SELECT state FROM registries WHERE registry_id=new.registry_id) IS NOT 'published' OR (new.registry_id<>old.registry_id AND (SELECT registry_version FROM registries WHERE registry_id=new.registry_id)<=(SELECT registry_version FROM registries WHERE registry_id=old.registry_id)) BEGIN SELECT RAISE(ABORT,'Publication requires a newer published registry'); END;
''';p.write_text(s)
p=C/'migrations/002-evidence-and-publication.sql';s=p.read_text().replace('LEFT JOIN overlays o ON o.imported_binding_id=e.entry_id','LEFT JOIN overlays o ON o.entry_id=e.entry_id')
s=s.replace("FROM overlays o CROSS JOIN catalog_view v WHERE o.imported_binding_id IS NULL AND o.state='active';", "FROM overlays o CROSS JOIN catalog_view v WHERE o.imported_binding_id IS NULL AND o.state='active' AND NOT EXISTS(SELECT 1 FROM effective_entries e WHERE e.entry_id=o.entry_id);")
s=s.replace('PRAGMA user_version=3;', '''CREATE TRIGGER active_local_collision_insert BEFORE INSERT ON active_registry WHEN EXISTS(SELECT 1 FROM overlays o JOIN registry_entries e ON e.entry_id=o.entry_id AND e.registry_id=new.registry_id WHERE o.imported_binding_id IS NULL AND o.state='active') BEGIN SELECT RAISE(ABORT,'Resolve local/import collision before publication'); END;
CREATE TRIGGER active_local_collision_update BEFORE UPDATE OF registry_id ON active_registry WHEN EXISTS(SELECT 1 FROM overlays o JOIN registry_entries e ON e.entry_id=o.entry_id AND e.registry_id=new.registry_id WHERE o.imported_binding_id IS NULL AND o.state='active') BEGIN SELECT RAISE(ABORT,'Resolve local/import collision before publication'); END;
CREATE TRIGGER local_collision_insert BEFORE INSERT ON overlays WHEN new.imported_binding_id IS NULL AND new.state='active' AND EXISTS(SELECT 1 FROM effective_entries e WHERE e.entry_id=new.entry_id) BEGIN SELECT RAISE(ABORT,'Use an imported overlay for an active imported identity'); END;
CREATE TRIGGER local_collision_update BEFORE UPDATE ON overlays WHEN new.imported_binding_id IS NULL AND new.state='active' AND EXISTS(SELECT 1 FROM effective_entries e WHERE e.entry_id=new.entry_id) BEGIN SELECT RAISE(ABORT,'Use an imported overlay for an active imported identity'); END;
PRAGMA user_version=3;''');p.write_text(s)
(C/'schema.sql').write_text('\n'.join(p.read_text() for p in sorted((C/'migrations').glob('*.sql'))))

p=C/'tooling/migrate_v3.py';s=p.read_text();anchor="        if new.execute('PRAGMA integrity_check').fetchone()[0]!='ok'"
s=s.replace(anchor,"        new.execute(\"INSERT OR REPLACE INTO catalog_meta VALUES('rebuilt_with_corrected_extractor','false')\")\n        new.execute('UPDATE catalog_view SET registry_generation=(SELECT registry_version FROM registries WHERE registry_id=(SELECT registry_id FROM active_registry WHERE singleton=1)) WHERE EXISTS(SELECT 1 FROM active_registry)')\n"+anchor);p.write_text(s)

# Constraints JSON holds scope only. Bounds and identity are authoritative SQL columns.
p=C/'contracts/RawGrant.schema.json';d=json.loads(p.read_text());keep=('allowed_targets','allowed_handles','allowed_displays','max_items');d['properties']={k:d['properties'][k] for k in keep};d['required']=list(keep);d['$id']='urn:android-binding-catalog:1.1.0:RawGrantConstraints';d['description']='Only the client_raw_grants.constraints_json scope. Connection, descriptor, code/flag bounds, hash and effect are authoritative SQL columns and forbidden here.';p.write_text(json.dumps(d,indent=2)+'\n')
for p in (C/'contracts').glob('*.json'):
 d=json.loads(p.read_text());defs=d.get('$defs') or d.get('components',{}).get('schemas',{})
 if 'ImportRequest' in defs:
  request=defs['ImportRequest'];request['properties']['registry_version']={'type':'integer','minimum':1};request['properties']['coverage']={'type':'string','minLength':1}
  for name in ('registry_version','coverage'):
   if name not in request['required']:request['required'].append(name)
 if p.name=='openapi.json':d['paths']['/v1/catalog/entries/{entry_id}/history']['get']['responses']['410'].pop('headers',None)
 p.write_text(json.dumps(d,indent=2)+'\n')

p=C/'bridge-config.sql';s=p.read_text().replace('target_key TEXT NOT NULL REFERENCES target_app_profiles(target_id)', 'target_key TEXT NOT NULL')
s=s.replace('audit_size_cap INTEGER NOT NULL CHECK(audit_size_cap>0));', 'audit_size_cap INTEGER NOT NULL CHECK(audit_size_cap>0),audit_retention_authorized INTEGER NOT NULL DEFAULT 0 CHECK(audit_retention_authorized IN (0,1)));')
s=s.replace("CREATE TRIGGER audit_no_delete BEFORE DELETE ON audit_events BEGIN", "CREATE TRIGGER audit_no_delete BEFORE DELETE ON audit_events WHEN coalesce((SELECT audit_retention_authorized FROM bridge_config WHERE singleton_id=1),0)<>1 BEGIN")
dup=('connection_id','descriptor','code_min','code_max','flags_mask','contract_hash','effect')
s=s.replace('PRIMARY KEY(client_id,connection_id,descriptor));', 'PRIMARY KEY(client_id,connection_id,descriptor),CHECK('+ ' AND '.join("json_type(constraints_json,'$."+key+"') IS NULL" for key in dup)+'));')
s=s.replace('CREATE TRIGGER audit_no_update', '''CREATE TRIGGER availability_target_insert BEFORE INSERT ON binding_availability WHEN new.target_key<>'platform:local' AND NOT EXISTS(SELECT 1 FROM target_app_profiles t WHERE t.target_id=new.target_key) BEGIN SELECT RAISE(ABORT,'Unknown availability target'); END;
CREATE TRIGGER availability_target_update BEFORE UPDATE OF target_key ON binding_availability WHEN new.target_key<>'platform:local' AND NOT EXISTS(SELECT 1 FROM target_app_profiles t WHERE t.target_id=new.target_key) BEGIN SELECT RAISE(ABORT,'Unknown availability target'); END;
CREATE TRIGGER audit_no_update''')
s=s.replace('CREATE TABLE tool_binding_aliases(tool_id', 'CREATE TABLE tool_binding_aliases(alias_name TEXT NOT NULL UNIQUE,tool_id')
p.write_text(s)
