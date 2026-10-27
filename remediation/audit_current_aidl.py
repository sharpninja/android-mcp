"""Exercise corrected parsers over complete published AIDL source text, read-only."""
import pathlib,sys,sqlite3,json,datetime,collections,hashlib,re
R=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(R/'catalog/tooling'))
import parsers,aidl_members,aidl_lex
release=sys.argv[2] if len(sys.argv)>2 else '006'
if not re.fullmatch(r'[0-9]{3}',release):raise ValueError('Numeric three-digit release required')
P=pathlib.Path('/home/sharpninja/github/2026-10-26/task/android-catalog-20261026/catalog/output')/('release-'+release)/'catalog.sqlite'
target=R/'evidence'/('round008-current-parser-corpus-audit'+('-'+sys.argv[1] if len(sys.argv)>1 else '')+'.json')
if target.exists():raise ValueError('Preserve existing audit')
counts=collections.Counter();statuses=collections.Counter();mismatches=[];quarantines=[]
db=sqlite3.connect(P.as_uri()+'?mode=ro',uri=True)
try:
 for sid,text in db.execute('SELECT source_id,raw_schema FROM aidl_schemas'):
  counts['files']+=1;expected=aidl_lex.inventory_counts(text)
  try:
   declarations=parsers.aidl_all(text);enriched=[]
   for item in declarations:
    span=item.get('declaration_span');fragment=text
    if span:fragment=''.join('\n' if c=='\n' else ' ' for c in text[:span[0]])+text[span[0]:span[1]]
    enriched.append(aidl_members.enrich(fragment,item))
   actual=len(enriched)+sum(len(d['members']) for d in enriched)
   unknown=len(enriched)==1 and enriched[0]['descriptor'] is None
   if not expected['reconciled'] or unknown:
    quarantines.append(dict(source_id=sid,expected=expected,actual=actual,reason=[d['reason'] for d in enriched]))
   elif actual!=expected['expected_records'] or len(enriched)!=expected['declarations']:
    mismatches.append(dict(source_id=sid,expected=expected,actual=actual,actual_type_declarations=len(enriched)))
   counts['type_declarations']+=len(enriched);counts['records']+=actual
   for d in enriched:
    statuses[d['parse_status']]+=1
    counts['unsupported_statements']+=sum(m['kind']=='unsupported' for m in d['members'])
  except ValueError as error:quarantines.append(dict(source_id=sid,expected=expected,reason=str(error)))
finally:db.close()
out=dict(completed_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),database=str(P),source_mode='read-only',source_files_modified=False,
 counts=dict(counts),declaration_statuses=dict(statuses),mismatches=mismatches,quarantines=quarantines,
 code_sha256={name:hashlib.sha256((R/'catalog/tooling'/name).read_bytes()).hexdigest() for name in ('parsers.py','aidl_members.py','aidl_lex.py')},
 limitations='Full syntactic no-drop exercise of source text, not new published data or independent grammar/wire correctness proof. Unsupported statements retain raw source/reasons.')
target.write_text(json.dumps(out,indent=2)+'\n');print(json.dumps(dict(counts=dict(counts),statuses=dict(statuses),mismatches=len(mismatches),quarantines=len(quarantines))),flush=True)
if mismatches:raise SystemExit(1)
