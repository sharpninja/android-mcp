import pathlib,json
R=pathlib.Path(__file__).resolve().parent;C=R/'catalog'
new_kinds=['aidl-parcelable','aidl-union','aidl-enum','type']
for p in (C/'contracts').glob('*.json'):
 d=json.loads(p.read_text())
 def visit(x,key=None):
  if isinstance(x,dict):
   if '$id' in x:x['$id']=x['$id'].replace('urn:android-binding-catalog:1.0.0:','urn:android-binding-catalog:1.1.0:')
   if 'aidl-interface' in x.get('enum',[]) and 'type' not in x['enum']:x['enum'].append('type')
   if key in ('Entry','Mutation') and 'properties' in x:
    gate={'if':{'properties':{'kind':{'enum':new_kinds}},'required':['kind']},'then':{'properties':{'schema_version':{'pattern':r'^1\.[1-9][0-9]*\.[0-9]+$'}}}}
    if gate not in x.setdefault('allOf',[]):x['allOf'].append(gate)
   for k,v in list(x.items()):visit(v,k)
  elif isinstance(x,list):
   for v in x:visit(v)
 visit(d)
 if p.name=='openapi.json':d['paths']['/v1/catalog/entries/{entry_id}']['delete']['responses'].pop('422',None)
 p.write_text(json.dumps(d,indent=2)+'\n')
p=C/'tooling/build_catalog.py';s=p.read_text().replace("('aidl-parcelable','aidl-union','aidl-enum')","('aidl-parcelable','aidl-union','aidl-enum','type')");p.write_text(s)
p=C/'migrations/002-evidence-and-publication.sql';s=p.read_text().replace("o.imported_binding_id IS NULL AND o.state='active'","o.imported_binding_id IS NULL AND o.state IN ('active','deleted')");p.write_text(s)
(C/'schema.sql').write_text('\n'.join(p.read_text() for p in sorted((C/'migrations').glob('*.sql'))))
p=C/'bridge-config.sql';s=p.read_text().replace('alias_name TEXT NOT NULL UNIQUE,tool_id','alias_name TEXT NOT NULL,tool_id').replace('PRIMARY KEY(tool_id,binding_id),FOREIGN KEY(binding_id,schema_hash) REFERENCES binding_mappings);','PRIMARY KEY(tool_id,binding_id),UNIQUE(alias_name,binding_id),FOREIGN KEY(binding_id,schema_hash) REFERENCES binding_mappings);')
anchor='CREATE TABLE client_binding_rules'
extra="CREATE TRIGGER alias_policy_consistent_insert BEFORE INSERT ON tool_binding_aliases WHEN EXISTS(SELECT 1 FROM tool_binding_aliases a WHERE a.alias_name=new.alias_name AND a.tool_id<>new.tool_id) BEGIN SELECT RAISE(ABORT,'Alias requires one shared policy entry point'); END;\nCREATE TRIGGER alias_policy_consistent_update BEFORE UPDATE ON tool_binding_aliases WHEN EXISTS(SELECT 1 FROM tool_binding_aliases a WHERE a.alias_name=new.alias_name AND a.tool_id<>new.tool_id AND a.rowid<>old.rowid) BEGIN SELECT RAISE(ABORT,'Alias requires one shared policy entry point'); END;\n"
if 'CREATE TRIGGER alias_policy_consistent_insert' not in s:s=s.replace(anchor,extra+anchor)
p.write_text(s)
p=C/'tests/test_sdk_api_index.py';s=p.read_text().replace("self.assertEqual(rows[0]['kind'],'unsupported')","self.assertEqual(rows[0]['kind'],'type')");p.write_text(s)
p=C/'README.md';s=p.read_text();s=s.replace('Current reproduction evidence is evidence/tests-regression-024.txt (85 tests)','Round004 reproduction evidence was evidence/tests-regression-024.txt (85 tests)').replace('New production release004 remains staging; no final production validation is claimed.','That historical snapshot had release004 staging. Current production progress and independent validation are recorded separately in the latest progress evidence; old snapshot scores are not current production assessments.')
s+='''

Index type declarations use additive1.1.0 kind type with type_kind_status unknown, separating known type presence from unknown class/interface/annotation semantics. The latest schemas use1.1.0 IDs and gate new type/AIDL kinds to1.1+ (including minor10+), rejecting those kinds in1.0 documents. Published/validated/retired/rejected registry membership and source links are sealed; copy migration loads staged memberships, restores terminal states, then installs pointers through all collision/base guards. A deleted local tombstone colliding with a new import requires explicit conflict just like an active local addition.
''';p.write_text(s)
p=R/'design/Android MCP Bridge Design.md';s=p.read_text()+'''

One protocol alias may require multiple canonical bindings. tool_binding_aliases stores one row per (alias_name,binding_id), while all rows sharing an alias_name must use one policy tool_id. Generated binding aliases share binding.invoke; convenience plans additionally require every referenced binding grant and their own restrictive entry-point rule. The alias constraint never replaces per-binding or OS authority checks.
''';p.write_text(s)
