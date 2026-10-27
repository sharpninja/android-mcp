"""Read-only full acquired AIDL corpus reconciliation under reviewed parser."""
import pathlib,sys,json,hashlib,collections,datetime,re
R=pathlib.Path(__file__).resolve().parent
candidate=sys.argv[2] if len(sys.argv)>2 else 'catalog-round008'
if pathlib.PurePath(candidate).name!=candidate:raise ValueError('Candidate must be task-local directory')
C=R/candidate;sys.path.insert(0,str(C/'tooling'))
from parsers import aidl_all
from aidl_members import enrich
from aidl_lex import inventory_counts
name=sys.argv[1] if len(sys.argv)>1 else 'aidl-corpus-audit-round008.json'
if pathlib.PurePath(name).name!=name:raise ValueError('Audit output must be task-local filename')
out=R/name
if out.exists():raise SystemExit('Preserve prior audit')
sources={}
for file in ['source-inventory-release007.json','outside-corpus-audit/source-inventory-batchfull985-after-lowrate001.json']:
 for s in json.loads((R/file).read_text())['sources']:
  if s['kind']=='aidl':sources[tuple(s[k] for k in ('repository','revision','path'))]=s
counts=collections.Counter();rows=[];failures=[]
for i,s in enumerate(sources.values(),1):
 data=(R/s['local_path']).read_bytes()
 if hashlib.sha256(data).hexdigest()!=s['sha256']:raise ValueError('Source mismatch '+s['path'])
 text=data.decode('utf8');lexical=inventory_counts(text);declarations=[];error=None
 try:
  for item in aidl_all(text):
   a,b=item['declaration_span'];fragment=''.join('\n' if c=='\n' else ' ' for c in text[:a])+text[a:b]
   declarations.append(enrich(fragment,item))
 except ValueError as e:error=str(e)
 records=sum(1+len(d['members']) for d in declarations)
 matched=not error and lexical['reconciled'] and lexical['declarations']==len(declarations) and lexical['expected_records']==records
 malformed=[]
 for d in declarations:
  counts['status:'+d['parse_status']]+=1
  for m in d['members']:
   counts['member:'+m['kind']]+=1
   if m['kind'] in ('method','aidl-member') and re.search(r'[{};]|\b(?:parcelable|interface|enum|union)\b',m.get('return_type') or ''):
    malformed.append(dict(canonical=m['canonical'],return_type=m['return_type']))
 row=dict(repository=s['repository'],revision=s['revision'],path=s['path'],sha256=s['sha256'],lexical=lexical,parsed_declarations=len(declarations),parsed_records=records,reconciled=matched,error=error,malformed_return_types=malformed)
 rows.append(row);counts['files']+=1;counts['declarations']+=len(declarations);counts['records']+=records;counts['matched' if matched else 'unreconciled']+=1;counts['malformed_return_types']+=len(malformed)
 if not matched or malformed:failures.append(row)
 if i%2000==0:print('AIDL audit',i,'/',len(sources),'unreconciled',counts['unreconciled'],flush=True)
report=dict(completed_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),candidate=candidate,producer_hashes={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in (C/'tooling').glob('*.py')},counts=dict(counts),failures=failures,sources=rows,limitations='Lexical/source declarations only; no proof of Binder wire layout, transaction IDs, runtime availability or native/generated/HIDL completeness')
out.write_text(json.dumps(report,indent=2));print(json.dumps(dict(counts=counts,sha256=hashlib.sha256(out.read_bytes()).hexdigest()),indent=2))
if failures:raise SystemExit(1)
