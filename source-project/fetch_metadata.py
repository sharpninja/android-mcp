import pathlib,urllib.request,json,base64,hashlib,datetime
R=pathlib.Path(__file__).resolve().parent; O=R/'probes'
jobs={
 'sdk-37-tree':('platform/prebuilts/sdk','0f07e8d60ab0aba46129e427101f96ba459a95c1','37.0/'),
 'sdk-current-tree':('platform/prebuilts/sdk','0f07e8d60ab0aba46129e427101f96ba459a95c1','current/'),
 'base-root-tree':('platform/frameworks/base','94b4c163b7dfe5ce3607f7bb8456f9573f7de57d',''),
 'manifest-commit':('platform/manifest','5bc9a7ce1cd78dd53613bbfd0ebf506e1e4adb0f',''),
}
for name,(repo,rev,path) in jobs.items():
 u=f'https://android.googlesource.com/{repo}/+/{rev}/{path}?format=JSON'
 try:
  b=urllib.request.urlopen(u,timeout=30).read();(O/name).write_bytes(b);j=json.loads(b[4:]);print(name,j if 'entries' not in j else [e['name'] for e in j['entries']],flush=True)
 except Exception as e:print(name,str(e),flush=True)
