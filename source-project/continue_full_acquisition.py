"""Sequential continuation: never duplicate the active supplemental downloader.
Waits for its explicit terminal checkpoint, then retrieves only still-missing
objects in the completed full-tree inventory. Denials are terminal, not retried.
"""
import pathlib,json,datetime,time,subprocess,sys
R=pathlib.Path(__file__).resolve().parent;A=R/'outside-corpus-audit';receipt=R/'full-acquisition-continuation-status.json'
if receipt.exists():raise SystemExit('Preserve prior continuation; inspect its outcome before any restart')
def save(phase,**extra):
    receipt.write_text(json.dumps(dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),phase=phase,**extra),indent=2))
save('waiting for explicit batchpaused774 completion')
while True:
    status=json.loads((A/'source-worker-batchpaused774-status.json').read_text())
    if 'denial' in status['status']:save('stopped: predecessor access denial',predecessor=status);raise SystemExit(2)
    if status['status']=='batch-complete':break
    time.sleep(20)
full=A/'source-worker-batchfull985-status.json'
if full.exists():save('blocked: full985 worker already has a receipt; inspect before restarting');raise SystemExit(3)
save('retrieving full985 remaining objects sequentially')
with (R/'source-download-batchfull985.log').open('x') as log:
    result=subprocess.run([sys.executable,str(R/'fetch_outside_sources.py'),'full985'],stdout=log,stderr=subprocess.STDOUT,cwd=R)
if result.returncode:save('full985 retrieval ended unsuccessfully; do not retry blindly',returncode=result.returncode);raise SystemExit(result.returncode)
save('reconciling exact acquired source bytes')
with (R/'source-reconciliation-batchfull985.log').open('x') as log:
    result=subprocess.run([sys.executable,str(R/'reconcile_outside_sources.py'),'full985','after-sequential-fetch'],stdout=log,stderr=subprocess.STDOUT,cwd=R)
save('complete' if result.returncode==0 else 'reconciliation failed',returncode=result.returncode)
raise SystemExit(result.returncode)
