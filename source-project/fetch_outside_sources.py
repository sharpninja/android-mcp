"""Retrieve newly proven-relevant source paths; never modifies frozen inventories."""
import pathlib,json,base64,hashlib,urllib.request,urllib.error,time,datetime,email.utils,sys
R=pathlib.Path(__file__).resolve().parent;A=R/'outside-corpus-audit';batch=sys.argv[1] if len(sys.argv)>1 else '070';sources=json.loads((A/('source-inventory-batch'+batch+'.json')).read_text())['sources'];groups={}
for s in sources:groups.setdefault(s['git_blob'],s)
pending=[s for oid,s in groups.items() if not (R/'snapshots/blobs'/oid).exists()];receipts={};log=A/('source-attempts-batch'+batch+'.jsonl')
def now():return datetime.datetime.now(datetime.timezone.utc).isoformat()
def save(status,count):
 (A/('source-worker-batch'+batch+'-status.json')).write_text(json.dumps(dict(status=status,updated_at=now(),completed=count,total=len(pending)),indent=2));(A/('source-receipts-batch'+batch+'.json')).write_text(json.dumps(receipts,indent=2))
save('running',0)
for ordinal,s in enumerate(pending,1):
 oid=s['git_blob'];url=s['url']+'?format=TEXT'
 for attempt in range(3):
  time.sleep(1);record=dict(at=now(),url=url,git_blob=oid,attempt=attempt)
  try:
   with urllib.request.urlopen(url,timeout=45) as response:data=base64.b64decode(response.read());record['status']=response.status
   if hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()!=oid:raise ValueError('Git blob verification failed')
   target=R/'snapshots/blobs'/oid;temp=target.with_suffix('.outside-partial');temp.write_bytes(data);temp.replace(target);receipts[oid]=dict(status='downloaded',local_path=str(target.relative_to(R)),sha256=hashlib.sha256(data).hexdigest(),size_bytes=len(data),retrieved_at=now());record.update(sha256=receipts[oid]['sha256'],bytes=len(data))
   with log.open('a') as f:f.write(json.dumps(record)+'\n')
   break
  except urllib.error.HTTPError as error:
   record.update(status=error.code,retry_after=error.headers.get('Retry-After'))
   with log.open('a') as f:f.write(json.dumps(record)+'\n')
   receipts[oid]=dict(status='unavailable',error=str(error))
   if error.code in (401,403):save('stopped on access denial',ordinal);raise SystemExit(2)
   if error.code==429 and attempt<2:
    pause=60*(attempt+1);header=error.headers.get('Retry-After')
    if header:
     try:pause=max(pause,float(header))
     except ValueError:pause=max(pause,(email.utils.parsedate_to_datetime(header)-datetime.datetime.now(datetime.timezone.utc)).total_seconds())
    save('temporary429; same URL backoff',ordinal)
    while pause>0:step=min(pause,60);time.sleep(step);pause-=step
    continue
   break
  except Exception as error:
   record.update(status='failed',error=str(error));receipts[oid]=dict(status='unavailable',error=str(error))
   with log.open('a') as f:f.write(json.dumps(record)+'\n')
   break
 if ordinal%25==0:save('running',ordinal);print(ordinal,'/',len(pending),flush=True)
save('batch-complete',len(pending));print('outside-source batch complete',len(pending),flush=True)
