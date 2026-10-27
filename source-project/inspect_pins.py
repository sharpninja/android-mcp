import urllib.request,json,pathlib,base64
from concurrent.futures import ThreadPoolExecutor
root=pathlib.Path(__file__).resolve().parent/'probes'
jobs={
 'sdk-release-tree':'https://android.googlesource.com/platform/prebuilts/sdk/+/0f07e8d60ab0aba46129e427101f96ba459a95c1/?format=JSON',
 'sdk-main-tree':'https://android.googlesource.com/platform/prebuilts/sdk/+/3af7c93524be6f51e092b87b17f009f13ee98b43/?format=JSON',
 'manifest-stable':'https://android.googlesource.com/platform/manifest/+/5bc9a7ce1cd78dd53613bbfd0ebf506e1e4adb0f/default.xml?format=TEXT',
 'base-commit':'https://android.googlesource.com/platform/frameworks/base/+/94b4c163b7dfe5ce3607f7bb8456f9573f7de57d?format=JSON',
 'sdk-tags':'https://android.googlesource.com/platform/prebuilts/sdk/+refs/tags?format=JSON',
}
def get(item):
 k,u=item
 try:
  b=urllib.request.urlopen(u,timeout=45).read();b=base64.b64decode(b) if 'format=TEXT' in u else b;(root/k).write_bytes(b)
  if 'tree' in k: print(k,[(x['name'],x['type']) for x in json.loads(b[4:])['entries']],flush=True)
  else:print(k,len(b),flush=True)
 except Exception as e:print(k,str(e),flush=True)
with ThreadPoolExecutor(max_workers=4) as pool:list(pool.map(get,jobs.items()))
