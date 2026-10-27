"""Supplemental reference-only permission inventory, never an OS permission grant."""
import pathlib,sqlite3,json,hashlib,collections,sys,jsonschema
KNOWN={'permission','all','any','access','conditional','manual','explicit-none','unknown'}
def names(ast):
 if not isinstance(ast,dict) or ast.get('op') not in KNOWN or ast.get('op') in ('unknown','manual','explicit-none'):return set()
 if ast['op']=='permission':return {ast['name']} if isinstance(ast.get('name'),str) and ast['name'] else set()
 result=set()
 for key,value in ast.items():
  if key.startswith('x-'):continue
  if isinstance(value,dict):result|=names(value)
  elif isinstance(value,list):
   for item in value:result|=names(item)
 return result
def run(catalog,output):
 source=pathlib.Path(catalog).resolve();out=pathlib.Path(output).resolve()
 if source==out or out.is_relative_to(source):raise ValueError('Do not modify immutable publication directory')
 if out.exists():raise ValueError('New evidence directory required')
 db=sqlite3.connect((source/'catalog.sqlite').as_uri()+'?mode=ro',uri=True)
 try:
  registry=db.execute('SELECT registry_id FROM active_registry WHERE singleton=1').fetchone()
  if registry is None:raise ValueError('Published immutable registry pin required')
  validator=jsonschema.Draft202012Validator(json.loads((pathlib.Path(__file__).resolve().parent/'PermissionReferenceEvidence.schema.json').read_text()))
  known={name for name, in db.execute('SELECT name FROM permission_definitions UNION SELECT value FROM permission_symbols')};references=collections.defaultdict(list);scanned=0
  for bid,sid,line,expression,raw in db.execute("SELECT binding_id,source_id,line,expression_json,raw_annotation FROM permission_relations WHERE logic IN ('permission','all','any','access','conditional')"):
   ast=json.loads(expression);scanned+=1
   for name in sorted(names(ast)-known):references[name].append(dict(binding_id=bid,source_id=sid,line=line,permission_expression=ast,raw_annotation=json.loads(raw) if raw else None))
  out.mkdir(parents=True);rows=[]
  for name,refs in sorted(references.items()):
   doc=dict(schema_version='1.1.0',kind='reference-only-permission-evidence',source_registry_id=registry[0],permission_id=hashlib.sha256(('permission:'+name).encode()).hexdigest(),name=name,manifest_definition_proven=False,constant_value_proven=False,protection_level=None,references=refs,os_authority='unknown',mcp_client_grants='not-inferred');validator.validate(doc);rows.append(doc)
  with (out/'permission-references.jsonl').open('w') as stream:
   for item in rows:stream.write(json.dumps(item,sort_keys=True,separators=(',',':'))+'\n')
  report=dict(schema_version='1.1.0',source_catalog=str(source),source_registry_id=registry[0],payload_sha256=hashlib.sha256((out/'permission-references.jsonl').read_bytes()).hexdigest(),known_definition_or_constant_names=len(known),reference_only_names=len(rows),known_plus_reference_names=len(known)+len(rows),relation_expressions_scanned=scanned,reference_occurrences=sum(len(v) for v in references.values()),complete=False,status='supplemental source evidence; does not claim these permissions exist on an OS',primary_permission_summary_unchanged=True)
  (out/'coverage.json').write_text(json.dumps(report,indent=2));return report
 finally:db.close()
if __name__=='__main__':print(json.dumps(run(sys.argv[1],sys.argv[2]),indent=2))
