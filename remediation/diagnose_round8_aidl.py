import pathlib,sqlite3,json,sys
R=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(R/'catalog/tooling'));import parsers,aidl_members,aidl_lex
report=json.loads((R/'evidence/round008-current-parser-corpus-audit.json').read_text());db=sqlite3.connect('file:/home/sharpninja/github/2026-10-26/task/android-catalog-20261026/catalog/output/release-006/catalog.sqlite?mode=ro',uri=True)
try:
 for mismatch in report['mismatches'][:8]:
  sid=mismatch['source_id'];text=db.execute('SELECT raw_schema FROM aidl_schemas WHERE source_id=?',(sid,)).fetchone()[0];path=db.execute('SELECT path FROM sources WHERE source_id=?',(sid,)).fetchone()[0]
  declarations=parsers.aidl_all(text);details=[]
  for d in declarations:
   span=d.get('declaration_span');fragment=''.join('\n' if c=='\n' else ' ' for c in text[:span[0]])+text[span[0]:span[1]] if span else text
   e=aidl_members.enrich(fragment,d);details.append(dict(descriptor=d['descriptor'],span=span,members=[m['raw'][:200] for m in e['members']]))
  print(json.dumps(dict(mismatch=mismatch,path=path,source_excerpt=text[:1800],details=details)))
finally:db.close()
