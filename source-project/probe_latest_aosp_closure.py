"""Resolve public current-release metadata without replacing frozen source snapshots."""
import pathlib,json,urllib.request,base64,hashlib,datetime,time,xml.etree.ElementTree as ET
R=pathlib.Path(__file__).resolve().parent;D=R/'outside-corpus-audit/latest-release-source-closure-v1'
def fetch(url,name,limit=2*1024*1024):
 time.sleep(3)
 with urllib.request.urlopen(url,timeout=45) as response:
  data=response.read(limit+1)
  if len(data)>limit:raise ValueError('Public metadata exceeded bounded response '+name)
  (D/name).write_bytes(data);(D/(name+'.receipt.json')).write_text(json.dumps(dict(url=url,status=response.status,retrieved_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),bytes=len(data),sha256=hashlib.sha256(data).hexdigest()),indent=2));return data
def git_json(url,name):return json.loads(fetch(url,name).decode().split('\n',1)[1])
def head(repo,branch,name):
 refs=git_json('https://android.googlesource.com/'+repo+'/+refs/heads/'+branch+'?format=JSON',name);value=refs.get('refs/heads/'+branch,refs.get(branch))
 if not value:raise ValueError('Exact official branch absent: '+repo+'/'+branch)
 return value['value']
def main():
 D.mkdir(exist_ok=False);result=dict(selection_changed=False,status='running')
 try:
  prior=json.loads((R/'outside-corpus-audit/latest-release-manifest-probe-v1/result.json').read_text());branch=prior['default']['revision'];result['manifest_default_branch']=branch
  commit=head('platform/frameworks/base',branch,'frameworks-base-head.json');old=json.loads((R/'source-inventory-release008.json').read_text());source=next(s for s in old['sources'] if s['path']=='core/res/AndroidManifest.xml');result.update(frameworks_base_head=commit,selected_frameworks_base_commit=source['revision'],selected_core_manifest_same_revision=commit==source['revision'])
  if commit!=source['revision']:
   entries=git_json('https://android.googlesource.com/platform/frameworks/base/+/'+commit+'/core/res/?format=JSON','core-res-tree.json')['entries'];entry=next(e for e in entries if e['name']=='AndroidManifest.xml');raw=base64.b64decode(fetch('https://android.googlesource.com/platform/frameworks/base/+/'+commit+'/core/res/AndroidManifest.xml?format=TEXT','AndroidManifest.xml.base64'),validate=True);oid=hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest();assert oid==entry['id'];(D/'AndroidManifest.xml').write_bytes(raw);ns='{http://schemas.android.com/apk/res/android}';names=lambda data:{p.attrib.get(ns+'name') for p in ET.fromstring(data).findall('permission')};current=names(raw);previous=names((R/source['local_path']).read_bytes());result['latest_core_manifest']=dict(commit=commit,git_blob=oid,sha256=hashlib.sha256(raw).hexdigest(),unique_permission_names=len(current),added_since_selected=sorted(current-previous),absent_since_selected=sorted(previous-current),interpretation='Source snapshot differences, not permission grant changes or proven API introduction/removal bounds')
  super_commit=head('platform/superproject','android-latest-release','superproject-head.json');tree=git_json('https://android.googlesource.com/platform/superproject/+/'+super_commit+'/?format=JSON&recursive=1','superproject-tree.json');links=[e for e in tree['entries'] if e['type']=='commit'];result.update(superproject_commit=super_commit,gitlink_entries=len(links));result['status']='bounded official source closure metadata retrieved'
 except Exception as error:result.update(status='source closure probe stopped; frozen selection unchanged',error=repr(error))
 (D/'result.json').write_text(json.dumps(result,indent=2));print(json.dumps(result,indent=2))
if __name__=='__main__':main()
