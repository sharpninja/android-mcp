import pathlib,sys,sqlite3,json,datetime,hashlib,collections
R=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(R/'catalog/tooling'))
from parsers import aidl_all
from aidl_members import enrich
release=sys.argv[1] if len(sys.argv)>1 else '005'
destination=R/'evidence'/('aidl-multideclaration-audit.json' if release=='005' else 'aidl-multideclaration-audit-release'+release+'.json')
if destination.exists():raise SystemExit('Refuse to replace an existing audit receipt: '+str(destination))
S=pathlib.Path('/home/sharpninja/github/2026-10-26/task/android-catalog-20261026/catalog/output')/('release-'+release)/'catalog.sqlite';db=sqlite3.connect(S.as_uri()+'?mode=ro',uri=True);counts=collections.Counter();affected=[]
try:
 for sid,text in db.execute('SELECT source_id,raw_schema FROM aidl_schemas'):
  counts['files']+=1
  try:
   declarations=aidl_all(text);counts['top_level_declarations']+=len(declarations)
   if len(declarations)>1:counts['multiple_declaration_files']+=1
   dotted=any('.' in d.get('declaration_name','') for d in declarations)
   if dotted:counts['dotted_declaration_files']+=1
   if len(declarations)>1 or dotted:affected.append(dict(source_id=sid,declarations=[d.get('descriptor') for d in declarations],lines=[d.get('declaration_line') for d in declarations]))
  except ValueError as error:counts['explicit_unparsed_files']+=1;affected.append(dict(source_id=sid,error=str(error)))
finally:db.close()
report=dict(completed_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),database=str(S),database_open_mode='read-only',counts=dict(counts),affected=affected,source_files_modified=False,limitations='Top-level lexical declaration audit only; no new production publication or wire-schema claim.')
destination.write_text(json.dumps(report,indent=2));print(json.dumps(dict(completed=True,counts=dict(counts))))
