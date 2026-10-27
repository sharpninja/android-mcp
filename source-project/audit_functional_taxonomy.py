import pathlib,sqlite3,json,sys,collections,datetime,hashlib
from functional_source_facts import taxonomy
R=pathlib.Path(__file__).resolve().parent;release=sys.argv[1]
if len(release)!=3 or not release.isdecimal():raise ValueError('Three digit release')
D=R/('integration/functional-audit-release'+release+'-v1');D.mkdir(exist_ok=False)
database=R/('catalog/output/release-'+release+'/catalog.sqlite');db=sqlite3.connect(database.as_uri()+'?mode=ro&immutable=1',uri=True);counts=collections.Counter()
try:
 with (D/'type-search-index.jsonl').open('w') as index,(D/'functional-evidence.jsonl').open('w') as facts:
  for raw, in db.execute("SELECT document_json FROM registry_entries WHERE entry_id IN (SELECT binding_id FROM bindings WHERE kind IN ('class','interface','enum','annotation','type','aidl-interface','aidl-parcelable','aidl-union','aidl-enum'))"):
   doc=json.loads(raw);result=taxonomy(doc);counts['types_scanned']+=1;counts[result['classification']]+=1
   index.write(json.dumps({k:result[k] for k in ('entry_id','namespace_search_evidence','classification')},sort_keys=True)+'\n')
   if result['functional_evidence']:
    facts.write(json.dumps(result,sort_keys=True)+'\n')
    for v in result['functional_evidence']:counts[v['confidence']]+=1
 report=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),release=release,registry_id=db.execute('SELECT registry_id FROM active_registry WHERE singleton=1').fetchone()[0],counts=dict(counts),tests=6,producer_sha256=hashlib.sha256((R/'functional_source_facts.py').read_bytes()).hexdigest(),complete=False,scope='All normalized type rows; Android service annotation axes and namespace search index, not generated functional groups',limitations=['Many-to-many functional group policy and atomic group sizes are not inferred','Member/type/registration links remain in full catalog and callback evidence; dependency closure belongs to group reference consumer','Descriptions not present in signatures remain unknown; no synthesized description treated as source fact'],files=[])
 for p in sorted(D.glob('*.jsonl')):report['files'].append(dict(path=p.name,bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
 (D/'report.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
finally:db.close()
