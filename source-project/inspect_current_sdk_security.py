import pathlib,urllib.request,json,datetime,hashlib
R=pathlib.Path(__file__).resolve().parent
rev='a56cc077b21e184de5b51a21b39aa5285367b096';u='https://android.googlesource.com/platform/prebuilts/sdk/+/'+rev+'/?format=JSON'
try:
 r=urllib.request.urlopen(u,timeout=35);b=r.read();(R/'probes/sdk-security-tree').write_bytes(b);j=json.loads(b[4:]);receipt=dict(url=u,revision=rev,status=r.status,retrieved_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),sha256=hashlib.sha256(b).hexdigest(),directories=[e['name'] for e in j['entries'] if e['type']=='tree']);print(json.dumps(receipt),flush=True);(R/'probes/sdk-security-tree-receipt.json').write_text(json.dumps(receipt,indent=2))
except Exception as e:print(type(e).__name__,str(e),flush=True)
