"""Build from an immutable copy of tested extractor/contracts, then validate.
Usage: python run_catalog_release.py 004 source-inventory-release004.json
"""
import pathlib,hashlib,json,sys,datetime,subprocess,shutil,re,os
R=pathlib.Path(__file__).resolve().parent
release=sys.argv[1]
if not re.fullmatch(r'\d{3}',release):raise ValueError('Three-digit release identifier required')
I=R/sys.argv[2];O=R/'catalog/output'/('release-'+release);S=R/'build-snapshots'/('release'+release)
catalog=R/(sys.argv[3] if len(sys.argv)>3 else 'catalog-integrated');T=R/('sqlite-temp-release'+release);T.mkdir(exist_ok=True)
resume='--resume' in sys.argv
if resume:
 if not S.exists() or O.exists():raise ValueError('Resume allowed only after a fixture-only failure, before any production output exists')
else:
 if S.exists() or O.exists():raise ValueError('Preserve existing build snapshot/output; select a new release')
 S.mkdir(parents=True)
 for name in ('tooling','contracts','migrations','tests'):
  shutil.copytree(catalog/name,S/'catalog'/name,ignore=shutil.ignore_patterns('__pycache__'))
 for p in catalog.iterdir():
  if p.is_file():shutil.copyfile(p,S/'catalog'/p.name)
 shutil.copytree(R/'evidence',S/'evidence')
 driver_name=json.loads(I.read_text()).get('stage_source_driver','stage_sdk372_sources.py')
 driver=(R/driver_name).resolve()
 if R not in driver.parents:raise ValueError('Stage driver escapes task directory')
 shutil.copyfile(driver,S/'stage_sdk372_sources.py')
 availability=R/'startup-availability-evidence-v1'
 if availability.exists():
  shutil.copytree(availability,S/'startup-availability-evidence-v1')
  for name in ('sdk_availability_evidence.py','test_sdk_availability_evidence.py'):
   shutil.copyfile(R/name,S/name)
# Stage-module import is pure; fixtures inject their own temporary data roots.
def now():return datetime.datetime.now(datetime.timezone.utc).isoformat()
def digest(p):
 h=hashlib.sha256()
 with p.open('rb') as f:
  while b:=f.read(4*1024*1024):h.update(b)
 return h.hexdigest()
inventory_metadata=json.loads(I.read_text());cache_evidence_root=(R/inventory_metadata.get('cache_evidence_root','.')).resolve()
if cache_evidence_root!=R and R not in cache_evidence_root.parents:raise ValueError('Cache evidence root must remain in the task directory')
inputs=[I,R/('coverage-manifest-release'+release+'.json'),R/'requirements-extraction.txt',cache_evidence_root/'sdk37.2/java-extraction-report.json',cache_evidence_root/'sdk37.2/api-index-report.json']+[p for p in S.rglob('*') if p.is_file()]
if (cache_evidence_root/'input-code-manifest.json').exists():inputs.append(cache_evidence_root/'input-code-manifest.json')
inputs.append(pathlib.Path(__file__).resolve())
for extra in [R/("aidl-corpus-audit-round"+release+"-v2-final.json"),R/("preflight_permissions_round"+release+".py"),R/("round"+release+"-v2-source-integration-receipt.json")]:
 if extra.exists():inputs.append(extra)
preflight=R/('permission-preflight-release'+release+'.json')
if preflight.exists():inputs.append(preflight)
manifest_path=R/('build-release'+release+'-inputs.json')
if resume:
 manifest=json.loads(manifest_path.read_text())
 for record in manifest['files']:
  if digest(R/record['path'])!=record['sha256']:raise ValueError('Frozen input changed: '+record['path'])
 old_status=R/('build-release'+release+'-status.json');previous=json.loads(old_status.read_text())
 if previous['status']!='failed' or any(step['step']!='catalog/tests' or step['returncode']==0 for step in previous['steps']):raise ValueError('Only a confirmed fixture-only failure can be resumed')
 attempt=1;saved=R/('build-release'+release+'-attempt'+str(attempt)+'-status.json')
 while saved.exists():attempt+=1;saved=R/('build-release'+release+'-attempt'+str(attempt)+'-status.json')
 shutil.copyfile(old_status,saved)
else:
 manifest=dict(started_at=now(),inventory=str(I.relative_to(R)),code_snapshot=str(S.relative_to(R)),temporary_directory=str(T),files=[dict(path=str(p.relative_to(R)),sha256=digest(p),bytes=p.stat().st_size) for p in sorted(inputs)])
 manifest_path.write_text(json.dumps(manifest,indent=2))
log=R/('build-release'+release+'.log');results=[]
steps=[['catalog/tests','fixtures'],['catalog/tooling/build_catalog.py',str(I),str(O)],['catalog/tooling/validate_catalog.py',str(O),str(I),str(R/('validation-report-release'+release+'.json'))],['catalog/tooling/export_permissions.py',str(O),str(R/('permission-export-release'+release))]]
with log.open('a' if resume else 'w') as f:
 for args in steps:
  started=now();f.write('\nSTEP '+json.dumps(args)+'\n');f.flush()
  command=[sys.executable,'-m','unittest','discover','-s',str(S/args[0]),'-v'] if args[1]=='fixtures' else [sys.executable,str(S/args[0]),*args[1:]]
  child_env=dict(os.environ,SQLITE_TMPDIR=str(T),TMPDIR=str(T),PYTHONDONTWRITEBYTECODE='1')
  p=subprocess.Popen(command,stdout=f,stderr=subprocess.STDOUT,cwd=R,env=child_env)
  (R/("build-release"+release+"-status.json")).write_text(json.dumps(dict(status="running",code_snapshot=str(S.relative_to(R)),steps=results,current_step=dict(step=args[0],started_at=started,pid=p.pid,command=command)),indent=2))
  p.wait()
  results.append(dict(step=args[0],started_at=started,completed_at=now(),returncode=p.returncode));(R/('build-release'+release+'-status.json')).write_text(json.dumps(dict(status='running' if p.returncode==0 and len(results)<len(steps) else 'complete' if p.returncode==0 else 'failed',code_snapshot=str(S.relative_to(R)),steps=results),indent=2))
  if p.returncode:raise SystemExit(p.returncode)
print(json.dumps(dict(output=str(O),steps=results),indent=2))
