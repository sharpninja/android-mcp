"""Build and validate immutable release003 from its frozen combined inventory."""
import pathlib,hashlib,json,sys,datetime,subprocess
R=pathlib.Path(__file__).resolve().parent;O=R/'catalog/output/release-003';I=R/'source-inventory-release003.json'
def now():return datetime.datetime.now(datetime.timezone.utc).isoformat()
inputs=[I,R/'coverage-manifest-release003.json',R/'requirements-extraction.txt']
for folder in ('catalog/tooling','catalog/contracts','catalog/migrations'):
 inputs.extend(p for p in (R/folder).rglob('*') if p.is_file() and '__pycache__' not in p.parts)
manifest=dict(started_at=now(),inventory=str(I.relative_to(R)),files=[dict(path=str(p.relative_to(R)),sha256=hashlib.sha256(p.read_bytes()).hexdigest(),bytes=p.stat().st_size) for p in sorted(inputs)])
(R/'build-release003-inputs.json').write_text(json.dumps(manifest,indent=2));log=R/'build-release003.log';steps=[['catalog/tooling/build_catalog.py',str(I),str(O)],['catalog/tooling/validate_catalog.py',str(O),str(I)],['catalog/tooling/export_permissions.py',str(O)]]
results=[]
with log.open('w') as f:
 for args in steps:
  started=now();f.write('\nSTEP '+json.dumps(args)+'\n');f.flush();p=subprocess.run([sys.executable,str(R/args[0]),*args[1:]],stdout=f,stderr=subprocess.STDOUT,cwd=R)
  results.append(dict(step=args[0],started_at=started,completed_at=now(),returncode=p.returncode));(R/'build-release003-status.json').write_text(json.dumps(dict(status='running' if p.returncode==0 and len(results)<len(steps) else 'complete' if p.returncode==0 else 'failed',steps=results),indent=2))
  if p.returncode:raise SystemExit(p.returncode)
print(json.dumps(dict(output=str(O),steps=results),indent=2))
