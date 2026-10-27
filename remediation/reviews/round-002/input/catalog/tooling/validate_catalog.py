"""Read-only validation of every entry, provenance FK, source reconciliation and AST."""
import pathlib,json,sqlite3,hashlib,collections,sys,datetime
import jsonschema
P=pathlib.Path(__file__).resolve().parents[1]
def run(output,inventory):
 out=pathlib.Path(output).resolve();inv=pathlib.Path(inventory).resolve();sources=json.loads(inv.read_text())['sources']
 db=sqlite3.connect('file:'+str(out/'catalog.sqlite')+'?mode=ro',uri=True)
 validator=jsonschema.Draft202012Validator(json.loads((P/'contracts/Entry.schema.json').read_text()))
 ast_validator=jsonschema.Draft202012Validator(json.loads((P/'contracts/PermissionExpression.schema.json').read_text()))
 for f in (P/'contracts').glob('*.schema.json'):jsonschema.Draft202012Validator.check_schema(json.loads(f.read_text()))
 failures=[];kinds=collections.Counter();ops=collections.Counter();variants=0;n=0
 def walk(ast):
  ops[ast['op']]+=1
  for v in ast.values():
   if isinstance(v,dict) and 'op' in v:walk(v)
   elif isinstance(v,list):
    for x in v:
     if isinstance(x,dict) and 'op' in x:walk(x)
 for bid,document,digest,etag in db.execute('SELECT entry_id,document_json,record_sha256,etag FROM effective_entries ORDER BY entry_id'):
  doc=json.loads(document);errors=list(validator.iter_errors(doc))
  if errors:failures.extend(dict(entry_id=bid,path=list(e.path),message=e.message) for e in errors)
  assert hashlib.sha256(doc['canonical_signature'].encode()).hexdigest()==bid
  assert hashlib.sha256(document.encode()).hexdigest()==digest
  assert etag=='"cat1:1:'+digest+':'+bid+':0:active"'
  kinds[doc['kind']]+=1;walk(doc['permission']);variants+='permission_variants' in doc['extensions'];n+=1
  for param in doc['extensions'].get('parameters',[]):
   if 'permission' in param:ast_validator.validate(param['permission'])
  if n%20000==0:print('validated entries',n,flush=True)
 assert n==db.execute('SELECT count(*) FROM bindings').fetchone()[0]
 exports=0
 for line in (out/'entries.jsonl').open():
  doc=json.loads(line);exports+=1
  row=db.execute('SELECT record_sha256 FROM effective_entries WHERE entry_id=?',(doc['entry_id'],)).fetchone()
  assert row and hashlib.sha256(line.rstrip('\n').encode()).hexdigest()==row[0]
 assert exports==n
 downloaded=[s for s in sources if s['status']=='downloaded'];assert len(downloaded)==db.execute('SELECT count(*) FROM sources').fetchone()[0]
 for s in downloaded:assert hashlib.sha256((inv.parent/s['local_path']).read_bytes()).hexdigest()==s['sha256']
 reports=[json.loads(r[0]) for r in db.execute('SELECT document_json FROM parse_reports')]
 for r in reports:
  if 'expected_declarations' in r:assert r['expected_declarations']==r['declarations']
 signature_stats={}
 for k in ('sdk-signature','source-signature'):
  rs=[r for r in reports if r['kind']==k];signature_stats[k]=dict(files=len(rs),declarations=sum(r['declarations'] for r in rs),unsupported=sum(r['unsupported'] for r in rs))
 integrity=db.execute('PRAGMA integrity_check').fetchone()[0];fk=db.execute('PRAGMA foreign_key_check').fetchall();assert integrity=='ok' and not fk
 duplicates=db.execute('SELECT count(*) FROM (SELECT binding_id,source_id,line,count(*) n FROM occurrences GROUP BY binding_id,source_id,line HAVING n>1)').fetchone()[0];assert duplicates==0
 unmatched=db.execute('SELECT count(*) FROM annotation_evidence WHERE binding_id IS NULL').fetchone()[0]
 scoped_annotation_ops=collections.Counter()
 for document,scope in db.execute('SELECT document_json,scope FROM annotation_evidence'):
  a=json.loads(document)['annotation']
  if 'permission' in a:ast_validator.validate(a['permission']);scoped_annotation_ops[scope+':'+a['permission']['op']]+=1
 known_defs={x[0] for x in db.execute('SELECT DISTINCT name FROM permission_definitions')};known_symbols={x[0] for x in db.execute('SELECT DISTINCT value FROM permission_symbols')};leaves=collections.Counter()
 for row in db.execute('SELECT expression_json FROM permission_relations'):
  def refs(a):
   if a['op']=='permission':leaves[a['name']]+=1
   for v in a.values():
    if isinstance(v,dict) and 'op' in v:refs(v)
    elif isinstance(v,list):
     for x in v:
      if isinstance(x,dict) and 'op' in x:refs(x)
  refs(json.loads(row[0]))
 result=dict(validated_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),schema_version='1.2.0',entries_validated=n,json_schema_failures=failures,integrity_check=integrity,foreign_key_check=fk,duplicate_occurrences=duplicates,counts={t:db.execute('SELECT count(*) FROM '+t).fetchone()[0] for t in ('sources','bindings','occurrences','binding_declarations','permission_definitions','permission_symbols','permission_relations','aidl_schemas','annotation_evidence','issues')},entry_kinds=dict(kinds),effective_permission_nodes=dict(ops),scoped_annotation_permission_nodes=dict(scoped_annotation_ops),source_dependent_permission_entries=variants,signature_reconciliation=signature_stats,external_annotations_unmatched=unmatched,permission_reference_names=dict(total=len(leaves),with_definition=len(set(leaves)&known_defs),constant_only=len((set(leaves)&known_symbols)-known_defs),without_definition_or_constant=len(set(leaves)-known_defs-known_symbols)),undefined_permission_names=sorted(set(leaves)-known_defs-known_symbols),complete=False)
 db.close();(out/'validation-report.json').write_text(json.dumps(result,indent=2));print(json.dumps(result,indent=2),flush=True)
 if failures:raise ValueError('JSON Schema validation failed')
 return result
if __name__=='__main__':run(sys.argv[1],sys.argv[2])
