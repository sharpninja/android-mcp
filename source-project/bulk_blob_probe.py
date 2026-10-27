"""Test the standard Google Git protocol for two selected text blobs only."""
import pathlib,json,subprocess,hashlib,datetime
R=pathlib.Path(__file__).resolve().parent
sources=json.loads((R/'source-selection.json').read_text())['sources'];repo='platform/frameworks/base';missing=[]
for s in sources:
 if s['repository']==repo and not (R/'snapshots/blobs'/s['git_blob']).exists() and s['git_blob'] not in [x['git_blob'] for x in missing]:missing.append(s)
 if len(missing)==2:break
d=R/'tree-cache'/repo.replace('/','__');oids=[s['git_blob'] for s in missing]
p=subprocess.run(['git','-C',str(d),'fetch','--no-tags','--no-write-fetch-head','--depth=1','https://android.googlesource.com/'+repo,*oids],capture_output=True,text=True,timeout=90)
receipt=dict(time=datetime.datetime.now(datetime.timezone.utc).isoformat(),repository=repo,requested_paths=[s['path'] for s in missing],git_blobs=oids,exit_code=p.returncode,stderr=p.stderr,strategy='standard same-origin Git selected-blob request; no checkout or branch mutation')
if p.returncode==0:
 for s in missing:
  b=subprocess.run(['git','-C',str(d),'cat-file','blob',s['git_blob']],capture_output=True,check=True).stdout
  assert hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()==s['git_blob']
  (R/'snapshots/blobs'/s['git_blob']).write_bytes(b)
 receipt['stored_blobs']=len(missing)
(R/'bulk-blob-probe.json').write_text(json.dumps(receipt,indent=2));print(json.dumps(receipt),flush=True)
