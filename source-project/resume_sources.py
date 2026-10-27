"""Resume the exact pinned Google URLs at <=1 request/second globally.
Temporary 429: honor Retry-After (at least 60 seconds), same URL, shared pause.
Permission 401/403: stop globally; never change routes, identities or TLS setup.
Every attempt is appended to a receipt; failed source bytes are never invented.
"""
import pathlib,json,urllib.request,urllib.error,base64,hashlib,time,datetime,threading,email.utils,os,sys
from concurrent.futures import ThreadPoolExecutor,as_completed
R=pathlib.Path(__file__).resolve().parent
sources=json.loads((R/'source-selection.json').read_text())['sources'];receipts=json.loads((R/'download-receipts.json').read_text());groups={}
for s in sources:groups.setdefault(s['git_blob'],[]).append(s)
pending=[(oid,ss) for oid,ss in groups.items() if not (R/'snapshots/blobs'/oid).exists()]
pending.sort(key=lambda item:({'manifest':0,'source-signature':1,'aidl':2,'evidence':3}.get(item[1][0]['kind'],4),item[1][0]['scope']=='test-or-example',item[1][0]['path']))
batch_limit=next((int(a.split('=',1)[1]) for a in sys.argv if a.startswith('--limit=')),len(pending));pending=pending[:batch_limit]
lock=threading.Lock();stop=threading.Event();next_at=0.;paused_until=0.
def now():return datetime.datetime.now(datetime.timezone.utc).isoformat()
def throttle():
 global next_at
 while not stop.is_set():
  with lock:
   wait=max(next_at,paused_until)-time.monotonic()
   if wait<=0:next_at=time.monotonic()+1.0;return True
  time.sleep(min(max(wait,.02),60))
 return False
def record(attempt):
 with lock:
  with (R/'resume-attempts.jsonl').open('a') as f:f.write(json.dumps(attempt)+'\n')
def fetch(item):
 global paused_until
 oid,ss=item;s=ss[0];url=s['url']+'?format=TEXT'
 for retry in range(3):
  if not throttle():return oid,dict(status='not-retrieved',error='Acquisition stopped globally on access denial or exhausted temporary retries')
  attempt=dict(started_at=now(),git_blob=oid,url=url,retry=retry,route='unchanged')
  try:
   r=urllib.request.urlopen(url,timeout=45);b=base64.b64decode(r.read());assert hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()==oid
   dest=R/'snapshots/blobs'/oid;dest.write_bytes(b);receipt=dict(status='downloaded',local_path=str(dest.relative_to(R)),sha256=hashlib.sha256(b).hexdigest(),size_bytes=len(b),retrieved_at=now());attempt.update(status=r.status,sha256=receipt['sha256'],size_bytes=len(b));record(attempt);return oid,receipt
  except urllib.error.HTTPError as e:
   attempt.update(status=e.code,error=str(e),retry_after=e.headers.get('Retry-After'));record(attempt)
   if e.code in (401,403):stop.set();return oid,dict(status='unavailable',error=str(e),denial=True)
   if e.code==429:
    seconds=60*(retry+1);header=e.headers.get('Retry-After')
    if header:
     try:seconds=max(seconds,float(header))
     except ValueError:
      try:seconds=max(seconds,(email.utils.parsedate_to_datetime(header)-datetime.datetime.now(datetime.timezone.utc)).total_seconds())
      except Exception:pass
    with lock:paused_until=max(paused_until,time.monotonic()+seconds)
    if retry<2:continue
   return oid,dict(status='unavailable',error=str(e))
  except Exception as e:attempt.update(status='failed',error=str(e));record(attempt);return oid,dict(status='unavailable',error=str(e))
print('resuming',len(pending),'unique blobs at <=1 request/sec',flush=True)
(R/'source-worker-status.json').write_text(json.dumps(dict(pid=os.getpid(),status='running',started_at=now(),batch_size=len(pending)),indent=2))
completed=0
with ThreadPoolExecutor(max_workers=4) as pool:
 for future in as_completed([pool.submit(fetch,item) for item in pending]):
  oid,receipt=future.result();receipts[oid]=receipt;completed+=1
  if completed%50==0 or receipt['status']!='downloaded':
   (R/'download-receipts.json').write_text(json.dumps(receipts,indent=2))
   print('completed',completed,'/',len(pending),'last',receipt['status'],flush=True)
   (R/'source-worker-status.json').write_text(json.dumps(dict(pid=os.getpid(),status='running',updated_at=now(),completed=completed,batch_size=len(pending)),indent=2))
(R/'download-receipts.json').write_text(json.dumps(receipts,indent=2));print('resume complete',completed,flush=True)
(R/'source-worker-status.json').write_text(json.dumps(dict(pid=os.getpid(),status='batch-complete',completed_at=now(),completed=completed,batch_size=len(pending)),indent=2))
