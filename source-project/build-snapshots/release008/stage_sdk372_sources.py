"""Enumerate EVERY source-archive member and cache Java declaration metadata.
Source snapshots remain immutable. Non-Java members are retained as evidence.
Computed Git-format hashes are local addresses, not claims of upstream Git pins.
"""
import pathlib,json,hashlib,zipfile,datetime,sys,gzip,collections,os,argparse
R=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(R/'catalog/tooling'))
import java_source,parsers
from java_source import parse
D=R/'sdk37.2';CACHE=D/'java-cache';B=R/'snapshots/blobs';PARSER_SHA=None
def configure(root,cache_dir='java-cache'):
 global R,D,CACHE,B,PARSER_SHA
 sub=pathlib.PurePath(cache_dir)
 if sub.is_absolute() or '..' in sub.parts:raise ValueError('Cache directory must be scoped below sdk37.2')
 R=pathlib.Path(root).resolve();D=R/'sdk37.2';CACHE=D/sub;B=R/'snapshots/blobs'
 CACHE.mkdir(parents=True,exist_ok=True);B.mkdir(parents=True,exist_ok=True)
 from cache_fingerprint import current,P as producer_directory
 if pathlib.Path(java_source.__file__).resolve().parent!=producer_directory or pathlib.Path(parsers.__file__).resolve().parent!=producer_directory:raise ValueError('Loaded parser does not match cache fingerprint producer directory')
 PARSER_SHA=current('java-source')
def verify_cache(cache,stats):
 if not cache.exists() or not cache.stat().st_size:return False
 try:
  if hashlib.sha256(cache.read_bytes()).hexdigest()!=stats.get('cache_sha256'):return False
  with gzip.open(cache,'rt') as f:count=sum(1 for line in f if json.loads(line))
  return count==stats['records']
 except (OSError,EOFError,ValueError,KeyError):return False
def quarantine_incomplete():
 moved=[];q=D/'java-cache-quarantine'/datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%SZ')
 for cache in CACHE.glob('*.jsonl.gz'):
  sp=cache.with_name(cache.name.replace('.jsonl.gz','.stats.json'))
  try:valid=verify_cache(cache,json.loads(sp.read_text()))
  except (OSError,ValueError):valid=False
  if not valid:
   q.mkdir(parents=True,exist_ok=True)
   for p in (cache,sp):
    if p.exists():moved.append(dict(path=str(p.relative_to(R)),bytes=p.stat().st_size,quarantine=str((q/p.name).relative_to(R))));p.rename(q/p.name)
 if moved:(q/'manifest.json').write_text(json.dumps(moved,indent=2))
 return moved
def work(item):
 if PARSER_SHA is None:raise RuntimeError('Configure extraction paths before invoking work')
 source=item;cache=CACHE/(source['git_blob']+'.jsonl.gz');stats_path=CACHE/(source['git_blob']+'.stats.json')
 if stats_path.exists():
  stats=json.loads(stats_path.read_text())
  if stats.get('parser_sha256')==PARSER_SHA and verify_cache(cache,stats):return source,stats
 data=(R/source['local_path']).read_bytes()
 try:result=parse(data,source['path'])
 except Exception as e:
  (D/'java-stage-failure.json').write_text(json.dumps(dict(path=source['path'],blob=source['git_blob'],error=repr(e)),indent=2));raise
 records=result.pop('records');stats=dict(**result,records=len(records),by_kind=dict(collections.Counter(r['kind'] for r in records)),parser_sha256=PARSER_SHA,cache_path=str(cache.relative_to(R)))
 temporary=cache.with_suffix(cache.suffix+'.tmp')
 with gzip.GzipFile(filename=str(temporary),mode='wb',mtime=0,compresslevel=6) as out:
  for r in records:out.write((json.dumps(r,sort_keys=True,separators=(',',':'),ensure_ascii=False)+'\n').encode())
 stats['cache_sha256']=hashlib.sha256(temporary.read_bytes()).hexdigest()
 assert verify_cache(temporary,stats),'Generated cache failed count/CRC validation'
 temporary.replace(cache);st=stats_path.with_suffix('.tmp');st.write_text(json.dumps(stats,indent=2));st.replace(stats_path);return source,stats
if __name__=='__main__':
 args=argparse.ArgumentParser();args.add_argument('--limit',type=int);args.add_argument('--cache-dir',default='java-cache');args.add_argument('--root',default=str(R));args=args.parse_args()
 configure(args.root,args.cache_dir)
 print('Quarantined incomplete caches:',len(quarantine_incomplete()),flush=True)
 receipt=json.loads((D/'source-archive-receipt.json').read_text());archive=D/'source-37.2_r01.zip';assert hashlib.sha1(archive.read_bytes()).hexdigest()==receipt['published_sha1']
 sources=[];stats=[];jobs=[];retrieved=receipt['retrieved_at'];revision='sdk-artifact:sources;android-37.2:r1:sha1-'+receipt['published_sha1']
 with zipfile.ZipFile(archive) as z:
  for info in sorted(z.infolist(),key=lambda x:x.filename):
   if info.is_dir():continue
   data=z.read(info);oid=hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest();p=B/oid
   if not p.exists():p.write_bytes(data)
   source=dict(repository='google-sdk-source-distribution',revision=revision,path=info.filename,git_blob=oid,git_object_origin='computed local content address; not an upstream Git object',kind='java-source' if info.filename.endswith('.java') else 'evidence',url=receipt['url']+'#'+info.filename,scope='sdk-source',api_level='37.2',api_major=37,api_minor=2,sdk_package_revision=1,sdk_extension_level=24,channel='stable',license='Original source headers and SDK source distribution terms; no license-acceptance command run',status='downloaded',local_path=str(p.relative_to(R)),sha256=hashlib.sha256(data).hexdigest(),size_bytes=len(data),retrieved_at=retrieved,transport=dict(kind='sdk-zip-member',archive_url=receipt['url'],archive_local_path=str(archive.relative_to(R)),archive_sha1=receipt['published_sha1'],archive_sha256=receipt['sha256'],archive_member=info.filename,crc32=f'{info.CRC:08x}'))
   sources.append(source)
   if source['kind']=='java-source':jobs.append(source)
 print('SDK37.2 members',len(sources),'Java files',len(jobs),'starting AST extraction',flush=True)
 (D/'source-inventory.json').write_text(json.dumps(dict(schema_version='1.0.0',sdk_api='37.2',package_revision=1,extension_level=24,source_archive=receipt,parse_status='source bytes staged; metadata extraction in progress',sources=sources),indent=2))
 # In-process sequential traversal avoids executor IPC/thread restrictions and
 # keeps only one file's AST/metadata in memory at a time.
 if True:
  if args.limit:jobs=jobs[:args.limit]
  for i,(s,stat) in enumerate(map(work,jobs),1):
   stats.append(stat);s['metadata_cache']=dict(path=stat['cache_path'],sha256=stat['cache_sha256'],parser_sha256=PARSER_SHA,records=stat['records'])
   if i%500==0:
    (D/'java-stage-progress.json').write_text(json.dumps(dict(pid=os.getpid(),processed=i,total=len(jobs),records=sum(x['records'] for x in stats),parse_error_regions=sum(x['parse_errors'] for x in stats)),indent=2));print('SDK AST files',i,'/',len(jobs),'records',sum(x['records'] for x in stats),flush=True)
 if not args.limit:(D/'source-inventory.json').write_text(json.dumps(dict(schema_version='1.0.0',sdk_api='37.2',package_revision=1,extension_level=24,source_archive=receipt,sources=sources),indent=2))
 report=dict(files=len(sources),java_files=len(jobs),declaration_records=sum(x['records'] for x in stats),parse_error_regions=sum(x['parse_errors'] for x in stats),files_with_parse_errors=sum(x['parse_errors']>0 for x in stats),by_kind=dict(sum((collections.Counter(x['by_kind']) for x in stats),collections.Counter())),parser_sha256=PARSER_SHA,source_archive_sha256=receipt['sha256'],source_archive_sha1_verified=True,source_reports=stats)
 (D/('java-trial-report.json' if args.limit else 'java-extraction-report.json')).write_text(json.dumps(report,indent=2));print(json.dumps({k:v for k,v in report.items() if k!='source_reports'},indent=2),flush=True)
