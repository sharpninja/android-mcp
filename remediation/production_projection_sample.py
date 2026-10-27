"""Exercise the pipeline on one exact public AIDL source from completed008.
This is an offline verification subset, not a production release or device probe.
"""
import collections,contextlib,datetime,hashlib,json,pathlib,sqlite3,sys
R=pathlib.Path(__file__).resolve().parent;S=pathlib.Path('/home/sharpninja/github/2026-10-26/task/android-catalog-20261026');sys.path.insert(0,str(R/'catalog/tooling'));sys.path.insert(0,str(R/'catalog/tests'))
import build_catalog,validate_catalog,catalog_projection,device_resolution as dr,test_device_resolution as fixtures,device_view_storage
D=R/'evidence/production008-derived-projection-attempt2';D.mkdir(exist_ok=False)
with contextlib.closing(sqlite3.connect((S/'catalog/output/release-008/catalog.sqlite').as_uri()+'?mode=ro',uri=True)) as original:
    row=original.execute("SELECT a.raw_schema,d.document_json FROM aidl_schemas a JOIN source_details d USING(source_id) JOIN sources s USING(source_id) WHERE s.path LIKE '%IServiceManager.aidl' ORDER BY a.source_id LIMIT 1").fetchone()
    if row is None:raise ValueError('Requested public source absent')
    text,metadata=row;source=json.loads(metadata)
data=text.encode();assert hashlib.sha256(data).hexdigest()==source['sha256'];assert hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()==source['git_blob']
(D/'source.aidl').write_bytes(data);source['local_path']='source.aidl';original_source_id=source.pop('source_id');source['x-original-production-source-id']=original_source_id
inventory=dict(catalog=dict(registry_version=1,android_version='Offline one-source008verification subset',api_level=37,coverage='Target37verification fixture; original source applicability unchanged/possibly unknown; not full production coverage'),sources=[source]);(D/'source-inventory.json').write_text(json.dumps(inventory,indent=2)+'\n')
build_catalog.build(D/'source-inventory.json',D/'catalog');validation=validate_catalog.run(D/'catalog',D/'source-inventory.json',D/'validation.json')
with contextlib.closing(sqlite3.connect((D/'catalog/catalog.sqlite').as_uri()+'?mode=ro',uri=True)) as db:
    registry=db.execute('SELECT registry_id FROM active_registry').fetchone()[0];entities=list(catalog_projection.load_inventory(db,registry));entry=json.loads(db.execute('SELECT document_json FROM effective_entries ORDER BY entry_id LIMIT 1').fetchone()[0])
facts=fixtures.facts();snapshot=dr.resolve(entities,facts,registry_id=registry,catalog_generation=1,overlay_generation=0,capability_epoch=0,view_generation=1)
with contextlib.closing(sqlite3.connect(D/'configuration-fixture.sqlite')) as db:
    db.executescript((R/'catalog/bridge-config.sql').read_text());db.execute("INSERT INTO connection_profiles(profile_id,backend_kind,display_name,enabled,row_version,android_user_id,endpoint_metadata_json,verification_strength) VALUES('fixture','self_adb','fixture',0,1,0,'{}','fixture')");db.commit()
    store=device_view_storage.DeviceViewStore(db,facts,registry_id=registry,catalog_generation=1,overlay_generation=0,inventory_count=len(entities));store.publish(snapshot,facts=facts,expected_generation=0);assert store.read()['snapshot']==snapshot;store.invalidate('verification-fixture-change');assert store.read()['stale']
report=dict(captured_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),scope='One exact public source from completed008 through current producer/catalog projection/resolver/SQLite; fixture facts only, not production import or observed device',source_path=source['path'],source_sha256=source['sha256'],original_production_source_id=original_source_id,source_bytes_unchanged=True,source_original_modified=False,entries=len(entities),independent_validation=validation['device_inventory'],integrity=validation['integrity_check'],foreign_key_failures=validation['foreign_key_check'],fixture_resolved_states=dict(collections.Counter(v['state'] for v in snapshot['entries'])),sqlite_atomic_activation_and_invalidation=True,entry_sample=entry,inventory_sample=next(v for v in entities if v['entry_id']==entry['entry_id']),limitations=['Imported types outside this source remain explicitly unresolved','Actual platform/module/interface facts and authority were not probed','This small verification subset is not a replacement production catalog'])
(R/'evidence/production008-derived-projection-sample.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps({k:v for k,v in report.items() if k not in ('entry_sample','inventory_sample')}))
