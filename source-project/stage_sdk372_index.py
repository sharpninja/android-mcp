"""Stage verified metadata-only SDK ZIP members for offline import."""
import pathlib,sys,json,hashlib,datetime,gzip,collections
R=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(R/'catalog/tooling'))
import sdk_api_index
D=R/'sdk37.2';M=D/'platform-metadata';receipt=json.loads((M/'metadata-range-receipt.json').read_text())
sources=[]
for path,kind in [('android-37.2/data/api-versions.xml','sdk-api-index'),('android-37.2/data/annotations.zip','sdk-annotations'),('android-37.2/data/res/values/attrs_manifest.xml','evidence'),('android-37.2/source.properties','evidence'),('android-37.2/sdk.properties','evidence'),('android-37.2/optional/optional.json','evidence')]:
 p=M/path;b=p.read_bytes();oid=hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest();local=R/'snapshots/blobs'/oid
 if not local.exists():local.write_bytes(b)
 s=dict(repository='google-sdk-platform-metadata',revision='sdk-artifact:platforms;android-37.2:r1:published-sha1-4bec12a02162ff6852df50b5fcf8ee762b050966',path=path,kind=kind,git_blob=oid,git_object_origin='computed local content address; not an upstream Git object',url='https://dl.google.com/android/repository/platform-37.2_r01.zip#'+path,scope='sdk-public' if kind!='evidence' else 'evidence',api_level='37.2',api_major=37,api_minor=2,sdk_package_revision=1,sdk_extension_level=24,channel='stable',license='Original Google SDK metadata; artifact license terms preserved, no license acceptance command run',status='downloaded',local_path=str(local.relative_to(R)),sha256=hashlib.sha256(b).hexdigest(),size_bytes=len(b),retrieved_at=receipt.get('retrieved_at',datetime.datetime.now(datetime.timezone.utc).isoformat()),transport=dict(kind='bounded-sdk-zip-range-member',archive_url='https://dl.google.com/android/repository/platform-37.2_r01.zip',whole_archive_hash_verified=False,metadata_receipt=str((M/'metadata-range-receipt.json').relative_to(R)),archive_member=path))
 if kind=='sdk-api-index':
  result=sdk_api_index.parse(b);rows=result.pop('records');out=D/'api-index-current.jsonl.gz';tmp=out.with_suffix('.tmp')
  with gzip.open(tmp,'wt') as f:
   for row in rows:f.write(json.dumps(row,sort_keys=True,separators=(',',':'))+'\n')
  tmp.replace(out);s['metadata_cache']=dict(path=str(out.relative_to(R)),sha256=hashlib.sha256(out.read_bytes()).hexdigest(),records=len(rows),parser_sha256=hashlib.sha256(pathlib.Path(sdk_api_index.__file__).read_bytes()).hexdigest())
  result.update(current_declarations=len(rows),source_sha256=s['sha256']);(D/'api-index-report.json').write_text(json.dumps(result,indent=2));print(json.dumps({k:v for k,v in result.items() if k not in ('excluded','issues')},indent=2))
 sources.append(s)
(D/'metadata-source-inventory.json').write_text(json.dumps(dict(schema_version='1.0.0',sources=sources),indent=2))
