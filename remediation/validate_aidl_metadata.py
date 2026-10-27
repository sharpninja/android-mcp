"""Read-only actual-source smoke, declaration metadata only; not a production build."""
import pathlib,sys,json,hashlib,collections,datetime
R=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(R/'catalog/tooling'))
from parsers import aidl
from aidl_members import enrich
S=pathlib.Path('/home/sharpninja/github/2026-10-26/task/android-catalog-20261026');inventory=S/'source-inventory-release004.json';original=inventory.read_bytes();spec=json.loads(original)
counts=collections.Counter();kinds=collections.Counter();unknown=[];errors=[]
for source in spec['sources']:
 if source['kind']!='aidl' or source['status']!='downloaded':continue
 path=inventory.parent/source['local_path'];data=path.read_bytes();counts['files']+=1
 if hashlib.sha256(data).hexdigest()!=source['sha256']:raise ValueError('Source digest mismatch '+source['path'])
 try:
  parsed=enrich(data.decode(),aidl(data.decode()));kinds[str(parsed['declaration_kind'])]+=1
  for row in parsed['members']:
   counts['members']+=1;counts[row['kind']]+=1
   if row['kind']=='unsupported':unknown.append(dict(repository=source['repository'],path=source['path'],line=row['line'],reason=row['extras'].get('parse_error','Unsupported declaration syntax')))
 except (ValueError,UnicodeError) as error:errors.append(dict(repository=source['repository'],path=source['path'],reason=str(error)))
 if counts['files']%3000==0:print(dict(counts),flush=True)
assert inventory.read_bytes()==original
report=dict(completed_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),inventory_sha256=hashlib.sha256(original).hexdigest(),counts=dict(counts),declaration_kinds=dict(kinds),unresolved_members=unknown,unparsed_files=errors,source_files_modified=False,production_database_generated=False,wire_tags_inferred=False,limitations='Read-only parser smoke with hash-verified bytes; unknown syntax retained; no runtime/codec/import-resolution validation or all-repository completeness claim.')
(R/'evidence/aidl-actual-source-smoke.json').write_text(json.dumps(report,indent=2)+'\n');print(dict(completed=True,counts=dict(counts),unparsed_files=len(errors)))
