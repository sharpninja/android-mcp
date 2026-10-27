"""Resume only verified existing caches; preserve the interrupted attempt log."""
import pathlib,subprocess,sys,datetime,json,os
R=pathlib.Path(__file__).resolve().parent;D=R/'cache-build-integrated009';receipt=D/'java-attempt002-status.json'
if receipt.exists():raise FileExistsError('Preserve existing attempt receipt')
def now():return datetime.datetime.now(datetime.timezone.utc).isoformat()
status=dict(started_at=now(),wrapper_pid=os.getpid(),previous_attempt='java-stage.log',previous_session=81131,previous_outcome='Session no longer available, no matching live task worker, no completed report, last checkpoint8500',cache_policy='Existing caches admitted only by source/parser fingerprint, SHA256, gzip CRC and record count; incomplete cache quarantined')
with (D/'java-stage-attempt002.log').open('w') as log:
 child=subprocess.Popen([sys.executable,str(D/'stage_sdk372_sources.py'),'--cache-dir','java-cache-v6'],stdout=log,stderr=subprocess.STDOUT,cwd=R)
 status.update(child_pid=child.pid,status='running');receipt.write_text(json.dumps(status,indent=2));print(json.dumps(status,indent=2),flush=True)
 child.wait();status.update(completed_at=now(),returncode=child.returncode,status='complete' if child.returncode==0 else 'failed');receipt.write_text(json.dumps(status,indent=2));print(json.dumps(status,indent=2),flush=True)
 raise SystemExit(child.returncode)
