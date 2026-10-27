import urllib.request, json, pathlib, datetime, hashlib
ROOT=pathlib.Path(__file__).resolve().parent
urls={
 'sdk-heads':'https://android.googlesource.com/platform/prebuilts/sdk/+refs/heads?format=JSON',
 'base-tags':'https://android.googlesource.com/platform/frameworks/base/+refs/tags?format=JSON',
 'manifest-heads':'https://android.googlesource.com/platform/manifest/+refs/heads?format=JSON',
 'platform-release-notes':'https://developer.android.com/tools/releases/platforms',
}
out=ROOT/'probes';out.mkdir(exist_ok=True)
receipts=[]
for key,url in urls.items():
 try:
  r=urllib.request.urlopen(url,timeout=40);b=r.read();(out/key).write_bytes(b)
  item=dict(key=key,url=url,status=r.status,bytes=len(b),sha256=hashlib.sha256(b).hexdigest(),retrieved_at=datetime.datetime.now(datetime.timezone.utc).isoformat())
 except Exception as e:item=dict(key=key,url=url,error=str(e))
 receipts.append(item);print(json.dumps(item),flush=True)
(out/'receipts.json').write_text(json.dumps(receipts,indent=2))
