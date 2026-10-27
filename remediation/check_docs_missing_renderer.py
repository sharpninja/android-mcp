"""Verify missing-renderer fail-closed behavior without changing a pinned dependency."""
from pathlib import Path
import subprocess,json,datetime,hashlib
R=Path(__file__).resolve().parent;E=R/'design-c4/build/receipts';prefix=E/'missing-renderer-negative-001'
if prefix.with_suffix('.completion.json').exists():raise SystemExit('Receipt exists')
request=json.loads((E/'gradle-package-015.request.json').read_text());args=request['invocation'][:]
args[-1]='STRUCTURIZR_CLI=/nonexistent/android-mcp-renderer.sh ./gradlew --offline --no-daemon -g .gradle-home verifyToolchain'
started=datetime.datetime.now(datetime.timezone.utc).isoformat()
with prefix.with_suffix('.stdout.txt').open('x') as out,prefix.with_suffix('.stderr.txt').open('x') as err:
    p=subprocess.run(args,stdout=out,stderr=err)
text=prefix.with_suffix('.stdout.txt').read_text()+prefix.with_suffix('.stderr.txt').read_text()
expected=p.returncode!=0 and 'Use the documented digest-pinned local Structurizr container' in text
result=dict(started_at=started,completed_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),invocation=args,actual_exit_code=p.returncode,expected_failure_confirmed=expected,reason='Selected renderer is deliberately absent; no dependency file/cache changed, no network or installation',stdout_sha256=hashlib.sha256(prefix.with_suffix('.stdout.txt').read_bytes()).hexdigest(),stderr_sha256=hashlib.sha256(prefix.with_suffix('.stderr.txt').read_bytes()).hexdigest(),scope='Documentation dependency guard only; not a failed app build')
prefix.with_suffix('.completion.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result));raise SystemExit(0 if expected else 1)
