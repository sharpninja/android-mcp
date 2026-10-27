"""Metadata-only permission union and concrete AST evidence samples, read-only."""
import pathlib,sqlite3,json,hashlib,sys,collections
out=pathlib.Path(sys.argv[1]).resolve();db=sqlite3.connect('file:'+str(out/'catalog.sqlite')+'?mode=ro',uri=True)
names=[r[0] for r in db.execute('SELECT name FROM permission_definitions UNION SELECT value FROM permission_symbols ORDER BY 1')]
with (out/'permissions.jsonl').open('w') as f:
 for name in names:
  definitions=[dict(source_id=s,protection_level=p,metadata=json.loads(a)) for s,p,a in db.execute('SELECT source_id,protection_level,attributes_json FROM permission_definitions WHERE name=?',(name,))]
  symbols=[dict(symbol=s,source_id=sid,line=n) for s,sid,n in db.execute('SELECT symbol,source_id,line FROM permission_symbols WHERE value=? ORDER BY symbol,source_id,line',(name,))]
  doc=dict(schema_version='1.1.0',permission_id=hashlib.sha256(('permission:'+name).encode()).hexdigest(),name=name,manifest_definitions=definitions,symbols=symbols,metadata_status='manifest-evidence-present' if definitions else 'constant-only; protection-level-and-attributes-unknown',os_authority='unknown',mcp_client_grants='not-inferred')
  f.write(json.dumps(doc,sort_keys=True,separators=(',',':'))+'\n')
samples=[]
for logic in ('all','any','value','conditional','manual','explicit-none','unknown'):
 for bid,sid,line,expr,raw in db.execute('SELECT binding_id,source_id,line,expression_json,raw_annotation FROM permission_relations WHERE logic=? LIMIT 3',(logic,)):
  samples.append(dict(logic=logic,binding_id=bid,source_id=sid,line=line,expression=json.loads(expr),raw_annotation=json.loads(raw) if raw else None))
for sid,locator,bid,scope,document in db.execute("SELECT source_id,locator,binding_id,scope,document_json FROM annotation_evidence WHERE document_json LIKE '%RequiresPermission.Read%' OR document_json LIKE '%RequiresPermission.Write%'"):
 samples.append(dict(source_id=sid,locator=locator,binding_id=bid,scope=scope,evidence=json.loads(document)))
(out/'permission-expression-samples.json').write_text(json.dumps(samples,indent=2))
summary=dict(permission_names=len(names),manifest_definition_records=db.execute('SELECT count(*) FROM permission_definitions').fetchone()[0],manifest_defined_names=db.execute('SELECT count(DISTINCT name) FROM permission_definitions').fetchone()[0],constant_occurrences=db.execute('SELECT count(*) FROM permission_symbols').fetchone()[0],distinct_constant_values=db.execute('SELECT count(DISTINCT value) FROM permission_symbols').fetchone()[0],public_sdk_manifest_permission_constants=db.execute("SELECT count(DISTINCT p.value) FROM permission_symbols p JOIN sources s USING(source_id) WHERE s.kind='sdk-signature' AND p.symbol LIKE 'android.Manifest.permission.%'").fetchone()[0],complete=False)
(out/'permission-coverage.json').write_text(json.dumps(summary,indent=2));print(json.dumps(summary,indent=2))
