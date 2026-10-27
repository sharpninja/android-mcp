"""Read-only permission metadata export to a separate NEW output directory."""
import pathlib,sqlite3,json,hashlib,sys,jsonschema

def run(catalog,output):
 source=pathlib.Path(catalog).resolve();out=pathlib.Path(output).resolve()
 if source==out:raise ValueError('Never write into an immutable release directory')
 if out.exists() and list(out.iterdir()):raise ValueError('Output must be empty')
 out.mkdir(parents=True,exist_ok=True)
 db=sqlite3.connect((source/'catalog.sqlite').as_uri()+'?mode=ro',uri=True)
 try:
  validator=jsonschema.Draft202012Validator(json.loads((pathlib.Path(__file__).resolve().parents[1]/'contracts/PermissionSummary.schema.json').read_text()))
  names=[r[0] for r in db.execute('SELECT name FROM permission_definitions UNION SELECT value FROM permission_symbols ORDER BY 1')]
  with (out/'permissions.jsonl').open('x') as f:
   for name in names:
    definitions=[dict(source_id=s,protection_level=p,metadata=json.loads(a)) for s,p,a in db.execute('SELECT source_id,protection_level,attributes_json FROM permission_definitions WHERE name=?',(name,))]
    symbols=[dict(symbol=s,source_id=sid,line=n) for s,sid,n in db.execute('SELECT symbol,source_id,line FROM permission_symbols WHERE value=? ORDER BY symbol,source_id,line',(name,))]
    doc=dict(schema_version='1.1.0',permission_id=hashlib.sha256(('permission:'+name).encode()).hexdigest(),name=name,manifest_definitions=definitions,symbols=symbols,metadata_status='manifest-evidence-present' if definitions else 'constant-only; protection-level-and-attributes-unknown',os_authority='unknown',mcp_client_grants='not-inferred')
    validator.validate(doc)
    f.write(json.dumps(doc,sort_keys=True,separators=(',',':'))+'\n')
  samples=[]
  for logic in ('permission','all','any','access','conditional','manual','explicit-none','unknown'):
   for bid,sid,line,expr,raw in db.execute('SELECT binding_id,source_id,line,expression_json,raw_annotation FROM permission_relations WHERE logic=? LIMIT 3',(logic,)):
    samples.append(dict(logic=logic,binding_id=bid,source_id=sid,line=line,expression=json.loads(expr),raw_annotation=json.loads(raw) if raw else None))
  (out/'permission-expression-samples.json').write_text(json.dumps(samples,indent=2))
  summary=dict(permission_names=len(names),manifest_definition_records=db.execute('SELECT count(*) FROM permission_definitions').fetchone()[0],source_catalog=str(source),complete=False)
  (out/'permission-coverage.json').write_text(json.dumps(summary,indent=2));return summary
 finally:db.close()

if __name__=='__main__':print(json.dumps(run(sys.argv[1],sys.argv[2]),indent=2))
