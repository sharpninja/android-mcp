"""Record exact bounded offline regression invocation, exit and input hashes."""
import datetime,hashlib,json,os,pathlib,re,subprocess,sys,time
R=pathlib.Path(__file__).resolve().parent;number=sys.argv[1];log=R/'evidence'/('tests-regression-'+number+'.txt');receipt=log.with_suffix('.receipt.json');assert not log.exists() and not receipt.exists()
args=[sys.executable]+(['-O'] if '--optimized' in sys.argv else [])+['-W','error::ResourceWarning','-m','unittest','discover','-s',str(R/'catalog/tests')];started=datetime.datetime.now(datetime.timezone.utc).isoformat();t=time.monotonic();env=dict(os.environ,PYTHONDONTWRITEBYTECODE='1')
with log.open('w') as stream:result=subprocess.run(args,stdout=stream,stderr=subprocess.STDOUT,env=env)
text=log.read_text();match=re.search(r'Ran (\d+) tests in ([0-9.]+)s',text);warnings='ResourceWarning' in text or 'Exception ignored' in text
files={str(p.relative_to(R)):hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted((R/'catalog').rglob('*')) if p.is_file() and '__pycache__' not in p.parts and p.suffix in ('.py','.json','.sql','.txt')}
data=dict(started_at=started,completed_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),invocation=args,environment_overrides={'PYTHONDONTWRITEBYTECODE':'1'},exit_code=result.returncode,tests=int(match[1]) if match else None,elapsed_seconds=time.monotonic()-t,warning_or_unraisable_text=warnings,passed=result.returncode==0 and match is not None and not warnings and '\nOK\n' in text,input_sha256=files,output_sha256=hashlib.sha256(log.read_bytes()).hexdigest(),scope='Offline fixtures; no Android/network/service deployment')
receipt.write_text(json.dumps(data,indent=2)+'\n');print(json.dumps({k:v for k,v in data.items() if k!='input_sha256'}));raise SystemExit(0 if data['passed'] else 1)
