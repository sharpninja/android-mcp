"""Read-only full008 data -> current offline projection/resolver scale evidence.
Not a production rebuild: historical parser defects and missing lifecycle rows stay.
No device observations. Outputs are local evidence, never a replacement release.
"""
import collections,contextlib,datetime,hashlib,itertools,json,pathlib,resource,sqlite3,sys,time
R=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(R/'catalog/tooling'));sys.path.insert(0,str(R/'catalog/tests'))
import catalog_projection as cp,device_resolution as dr,test_device_resolution as fixtures
source=pathlib.Path('/home/sharpninja/github/2026-10-26/task/android-catalog-20261026/catalog/output/release-008/catalog.sqlite');D=R/'evidence/round012-scale-attempt2';D.mkdir(exist_ok=False);began=time.monotonic();phases={};before=source.stat();db=sqlite3.connect(source.as_uri()+'?mode=ro',uri=True);db.execute('PRAGMA query_only=ON');out=sqlite3.connect(D/'derived-inventory.sqlite');out.execute('CREATE TABLE inventory(entry_id TEXT PRIMARY KEY,entity_kind TEXT NOT NULL,source_kinds_json TEXT NOT NULL,document_json TEXT NOT NULL)');out.execute('PRAGMA journal_mode=WAL');out.execute('PRAGMA synchronous=NORMAL')
def progress(phase,**kw):
 (D/'progress.json').write_text(json.dumps(dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),phase=phase,elapsed_seconds=round(time.monotonic()-began,3),**kw),indent=2)+'\n');print(phase,kw,flush=True)
try:
 progress('loading historical008 source metadata');sources={sid:json.loads(raw) for sid,raw in db.execute('SELECT source_id,document_json FROM source_details')};t=time.monotonic();projection=cp.Projection(db,sources);phases['projection_index_seconds']=time.monotonic()-t
 expected=db.execute('SELECT count(*) FROM bindings').fetchone()[0];inventory=[];tags={};h=hashlib.sha256();counts=collections.Counter();t=time.monotonic()
 progress('projecting historical008 declarations',expected=expected)
 with (D/'device_inventory.jsonl').open('w') as stream:
  query='SELECT binding_id,source_id,line,document_json FROM binding_declarations ORDER BY binding_id,source_id,line'
  for key,rows in itertools.groupby(db.execute(query),key=lambda r:r[0]):
   rows=list(rows);projected=[(sid,line,cp.js(projection.project(json.loads(raw),sid))) for _,sid,line,raw in rows];entity=projection.entity(key,projected);dr.validate(entity,'InventoryEntity');raw=dr.canonical(entity);stream.write(raw+'\n');h.update((raw+'\n').encode());inventory.append(entity);kinds=sorted({sources[sid]['kind'] for _,sid,_,_ in rows});tags[key]=kinds;counts[entity['entity_kind']]+=1;out.execute('INSERT INTO inventory VALUES(?,?,?,?)',(key,entity['entity_kind'],json.dumps(kinds),raw))
   if len(inventory)%25000==0:out.commit();progress('projecting',entries=len(inventory),expected=expected)
 out.commit();assert len(inventory)==expected==799527;phases['project_and_validate_seconds']=time.monotonic()-t;assert dr.inventory_digest(inventory)==h.hexdigest()
 facts=fixtures.facts();facts['platform']={'major':37,'minor':2};facts['extensions']={};facts['modules']={};facts['interfaces']={};facts['oem']=None;facts['build_fingerprint']=None;facts['evidence_ids']=['offline-scale-fixture-no-device-probe'];progress('full inventory resolution',entries=len(inventory));t=time.monotonic();snapshot=dr.resolve(inventory,facts,registry_id=hashlib.sha256(b'offline008-derived-verification-not-production').hexdigest(),catalog_generation=1,overlay_generation=0,capability_epoch=0,view_generation=1);phases['full_resolve_validate_seconds']=time.monotonic()-t;by_source=collections.defaultdict(collections.Counter);by_entity=collections.defaultdict(collections.Counter);reasons=collections.Counter()
 for row in snapshot['entries']:
  by_entity[row['entity_kind']][row['state']]+=1
  for kind in tags[row['entry_id']]:by_source[kind][row['state']]+=1
  reasons.update(row['reason_codes'])
 after=source.stat();assert (before.st_size,before.st_mtime_ns)==(after.st_size,after.st_mtime_ns)
 report=dict(completed_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),scope='Entire historical008-shaped registry projected/resolved by current code with offline37.2 fixture facts; no production publication, devices, OS authority or coverage completion',source_path=str(source),source_size=before.st_size,source_mtime_ns=before.st_mtime_ns,source_bytes_metadata_unchanged=True,entries=len(inventory),source_count=len(sources),inventory_sha256=h.hexdigest(),snapshot_inventory_sha256=snapshot['inventory_sha256'],entity_kinds=dict(counts),states_by_entity_kind={k:dict(v) for k,v in by_entity.items()},states_by_source_kind={k:dict(v) for k,v in by_source.items()},source_kind_counts_can_overlap=True,reasons=dict(reasons),phases=phases,total_seconds=time.monotonic()-began,max_rss_kib=resource.getrusage(resource.RUSAGE_SELF).ru_maxrss,facts=facts,producer_files={str(p.relative_to(R)):hashlib.sha256(p.read_bytes()).hexdigest() for p in [R/'catalog/tooling/catalog_projection.py',R/'catalog/tooling/device_resolution.py',R/'catalog/tooling/dispatch_manifest.py',R/'catalog/contracts/DeviceResolution.schema.json',R/'catalog/contracts/DeviceView.schema.json']},limitations=['Old immutable declarations retain pre-R9/R10 extractor defects; current AIDL import fix cannot retrospectively invent missing imports','Historical008 lacks3409lifecycle rows; source snapshot identities are original008 and not relabeled','Fixture availability does not prove actual device presence, codecs, grants or global source completeness','This checks full in-memory resolution scale, not full SQLite activation of799527rows'])
 (D/'result.json').write_text(json.dumps(report,indent=2)+'\n');progress('complete',entries=len(inventory),result=str(D/'result.json'))
except Exception as e:
 (D/'failure.json').write_text(json.dumps(dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),type=type(e).__name__,error=str(e),elapsed=time.monotonic()-began),indent=2)+'\n');raise
finally:db.close();out.close()
