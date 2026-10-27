"""Task-local post-validation reconciliation; never network, upload, or publish externally.
Waits for the existing builder instead of launching a duplicate. Any failure
preserves existing files and records the exact failed command for recovery.
"""
import pathlib,json,sys,datetime,time,subprocess,re,os
R=pathlib.Path(__file__).resolve().parent
v=sys.argv[1]
if not re.fullmatch(r'\d{3}',v):raise ValueError('Three-digit release required')
status=R/('local-followups-release'+v+'.json');log=R/('local-followups-release'+v+'.log')
if status.exists():raise SystemExit('Inspect existing follow-up; refusing duplicate')
receipt=dict(release=v,pid=os.getpid(),pid_scope='followup runner namespace',started_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),status='waiting for existing build/validation/permission export',steps=[],network=False,upload=False)
def save():
 receipt['updated_at']=datetime.datetime.now(datetime.timezone.utc).isoformat();tmp=status.with_suffix('.tmp');tmp.write_text(json.dumps(receipt,indent=2));tmp.replace(status)
save();deadline=time.monotonic()+6*3600
while True:
 try:build=json.loads((R/('build-release'+v+'-status.json')).read_text())
 except (OSError,json.JSONDecodeError):build={}
 if build.get('status')=='failed':receipt.update(status='blocked: existing build failed',build=build);save();raise SystemExit(1)
 if build.get('status')=='complete':break
 if time.monotonic()>deadline:receipt.update(status='waiting timeout; underlying job untouched');save();raise SystemExit(2)
 time.sleep(5)
commands=[(R/('production-reconciliation-release'+v+'.json'),['production_reconciliation.py',v]),(R/('export-reconciliation-release'+v+'.json'),['production_reconciliation.py',v,'--exports']),(R/('annotation-archive-comparison-006-to-'+v+'.json'),['compare_annotation_archives.py','006',v]),(R/('device-inventory-reconciliation-release'+v+'.json'),['device_inventory_reconciliation_v2.py',v]),(R/('release'+v+'-evidence/release-manifest.json'),['finish_catalog_release_v3.py',v]),(R/('deliverables/release'+v+'-evidence-delivery.json'),['package_release_evidence_v4.py',v])]
with log.open('x') as stream:
 for evidence,args in commands:
  if evidence.exists():receipt['steps'].append(dict(command=args,status='pre-existing evidence retained',path=str(evidence.relative_to(R))));save();continue
  receipt.update(status='running local follow-up',current_command=args);save();stream.write('STEP '+json.dumps(args)+'\n');stream.flush()
  child=subprocess.run([sys.executable,str(R/args[0]),*args[1:]],cwd=R,stdout=stream,stderr=subprocess.STDOUT)
  receipt['steps'].append(dict(command=args,returncode=child.returncode,completed_at=datetime.datetime.now(datetime.timezone.utc).isoformat()));save()
  if child.returncode:receipt.update(status='blocked: local follow-up failed; evidence retained',failed_command=args);save();raise SystemExit(child.returncode)
receipt.pop('current_command',None);receipt.update(status='complete local validation/evidence; no upload',completed_at=datetime.datetime.now(datetime.timezone.utc).isoformat());save();print(json.dumps(receipt,indent=2))
