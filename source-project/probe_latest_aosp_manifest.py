"""Bounded public source-revision discovery. Does not switch any selected corpus."""
import pathlib,urllib.request,hashlib,json,datetime,time,xml.etree.ElementTree as ET,base64
R=pathlib.Path(__file__).resolve().parent;D=R/'outside-corpus-audit/latest-release-manifest-probe-v1'
if D.exists():raise FileExistsError('Preserve original source-revision probe')
D.mkdir()
def fetch(url,name,limit=2*1024*1024):
 time.sleep(3)
 with urllib.request.urlopen(url,timeout=45) as response:
  raw=response.read(limit+1);status=response.status
  if len(raw)>limit:raise ValueError('Public metadata exceeded declared bound')
  (D/name).write_bytes(raw);receipt=dict(url=url,status=status,retrieved_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),bytes=len(raw),sha256=hashlib.sha256(raw).hexdigest());(D/(name+'.receipt.json')).write_text(json.dumps(receipt,indent=2));return raw
try:
 raw=fetch('https://android.googlesource.com/platform/manifest/+refs/heads/android-latest-release?format=JSON','latest-head.json');refs=json.loads(raw.decode().split('\n',1)[1]);head=refs.get('refs/heads/android-latest-release',refs.get('android-latest-release'))
 if not head:raise ValueError('Exact requested branch absent from official ref response')
 commit=head['value'];assert len(commit)==40 and all(c in '0123456789abcdef' for c in commit)
 raw=fetch('https://android.googlesource.com/platform/manifest/+/'+commit+'/default.xml?format=TEXT','default.xml.base64');xml=base64.b64decode(raw,validate=True);(D/'default.xml').write_bytes(xml);tree=ET.fromstring(xml)
 result=dict(status='official latest-release manifest head pinned',commit=commit,default=tree.find('default').attrib,projects=len(tree.findall('project')),selected_frameworks_base=[p.attrib for p in tree.findall('project') if p.get('name')=='platform/frameworks/base'],selection_changed=False,limitation='Branch name is discovery metadata, not proof of API37.2 corpus correspondence or shipping device availability')
except Exception as error:
 result=dict(status='probe failed; no corpus change',error=repr(error),selection_changed=False)
(D/'result.json').write_text(json.dumps(result,indent=2));print(json.dumps(result,indent=2))
