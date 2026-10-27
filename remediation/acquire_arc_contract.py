"""Read-only public upstream source acquisition, narrowly scoped to ARC protocol/runtime contracts."""
import urllib.request,json,pathlib,hashlib,datetime
R=pathlib.Path(__file__).resolve().parent;D=R/'arc-upstream';D.mkdir(exist_ok=True)
api='https://api.github.com/repos/sharpninja/Avalonia.RemoteControl'
def get(url):
 with urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'Android-MCP-design-contract-review','Accept':'application/vnd.github+json'}),timeout=45) as response:return response.read()
commit=json.loads(get(api+'/commits/master'));pin=commit['sha'];tree=json.loads(get(api+'/git/trees/'+pin+'?recursive=1'))
if tree.get('truncated'):raise ValueError('Incomplete upstream tree')
paths=[x['path'] for x in tree['tree'] if x['type']=='blob'];selected=[]
for path in paths:
 name=path.rsplit('/',1)[-1]
 if name.upper().startswith(('LICENSE','COPYING','NOTICE')) or path in ('Directory.Build.props','Directory.Packages.props') or path.startswith('src/Avalonia.RemoteControl.Protocol/') and path.endswith(('.proto','.cs','.csproj')) or path.startswith('src/Avalonia.RemoteControl.Runtime/') and path.endswith(('.cs','.csproj')) or path.startswith('tests/Avalonia.RemoteControl.Tests/') and any(k in name for k in ('Bridge','Authorization','Session')) and path.endswith('.cs') or path.startswith('src/Avalonia.RemoteControl.Client/') and any(k in name for k in ('Bridge','Marker')) and path.endswith('.cs'):
  selected.append(path)
rows=[]
for path in selected:
 raw=get('https://raw.githubusercontent.com/sharpninja/Avalonia.RemoteControl/'+pin+'/'+path);p=D/pin/path;p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(raw);rows.append(dict(path=path,size_bytes=len(raw),sha256=hashlib.sha256(raw).hexdigest()))
manifest=dict(acquired_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),repository='https://github.com/sharpninja/Avalonia.RemoteControl',commit=pin,commit_date=commit['commit']['committer']['date'],selected_source_scope='Protocol, Runtime, relevant bridge/client/tests and license metadata only; no repository databases, secrets, unrelated configs or software installation',files=rows,root_license_paths=[p for p in paths if p.upper().startswith(('LICENSE','COPYING','NOTICE'))],all_paths=paths)
(D/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n');print(json.dumps(dict(commit=pin,files=len(rows),bytes=sum(r['size_bytes'] for r in rows),root_license_paths=manifest['root_license_paths'])))
