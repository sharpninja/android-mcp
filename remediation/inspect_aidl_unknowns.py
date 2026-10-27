"""Explain the two remaining unsupported statements using published source only."""
import pathlib,sys,sqlite3,json,datetime,collections
R=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(R/'catalog/tooling'));import parsers,aidl_members
P=pathlib.Path('/home/sharpninja/github/2026-10-26/task/android-catalog-20261026/catalog/output/release-006/catalog.sqlite')
db=sqlite3.connect(P.as_uri()+'?mode=ro',uri=True);found=[];scanned=0
try:
 groups=dict(db.execute('SELECT parse_status,count(*) FROM aidl_schemas GROUP BY parse_status'))
 for sid,path,text,status in db.execute("SELECT a.source_id,s.path,a.raw_schema,a.parse_status FROM aidl_schemas a JOIN sources s USING(source_id) WHERE a.parse_status LIKE '%partial%'"):
  scanned+=1
  for d in parsers.aidl_all(text):
   span=d.get('declaration_span');fragment=''.join('\n' if c=='\n' else ' ' for c in text[:span[0]])+text[span[0]:span[1]] if span else text
   for m in aidl_members.enrich(fragment,d)['members']:
    if m['kind']=='unsupported':found.append(dict(source_id=sid,path=path,original_status=status,owner=m['owner'],line=m['line'],raw=m['raw'],reason=m['extras']['parse_error']))
finally:db.close()
out=dict(captured_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),source_database=str(P),mode='read-only',source_files_modified=False,production_status_groups=groups,candidate_partial_files_scanned=scanned,unsupported_statements=found,matched_full_corpus_count=len(found)==2,scope='Source explanation only; no rewrite or runtime/wire inference. Received after009freeze.')
(R/'evidence/round009-unsupported-aidl-explanation.json').write_text(json.dumps(out,indent=2)+'\n');print(json.dumps(out))
