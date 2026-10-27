"""Task-local build dependencies only; no host installation, settings or remote service."""
import pathlib,urllib.request,hashlib,json,zipfile,datetime
R=pathlib.Path(__file__).resolve().parents[1];T=R/'.tools';T.mkdir(parents=True,exist_ok=True)
def get(url):
 with urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'Android-MCP-local-documentation-build'}),timeout=60) as r:return r.read()
pinned=json.loads((R/'toolchain-lock.json').read_text());rows=[]
def save(url,p,expected=None):
 raw=p.read_bytes() if p.exists() else get(url);digest=hashlib.sha256(raw).hexdigest();expected=expected or next((v['sha256'] for v in pinned['files'] if v['file']==str(p.relative_to(R))),None)
 if expected is None:raise ValueError('Unpinned build dependency '+p.name)
 if digest!=expected:raise ValueError('Checksum mismatch '+p.name)
 p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(raw);rows.append(dict(file=str(p.relative_to(R)),url=url,size_bytes=len(raw),sha256=digest));return raw
version='8.14.3';url='https://services.gradle.org/distributions/gradle-'+version+'-bin.zip';expected=next(v['sha256'] for v in pinned['files'] if v['file']=='.tools/gradle-'+version+'-bin.zip');published=get(url+'.sha256').decode().strip().split()[0]
if published!=expected:raise ValueError('Published Gradle checksum differs from committed lock')
raw=save(url,T/('gradle-'+version+'-bin.zip'),expected)
with zipfile.ZipFile(T/('gradle-'+version+'-bin.zip')) as archive:
 for item in archive.infolist():
  target=(T/item.filename).resolve()
  if not target.is_relative_to(T.resolve()):raise ValueError('Unsafe build dependency path')
 archive.extractall(T)
for p in (T/('gradle-'+version)/'bin').iterdir():p.chmod(p.stat().st_mode|0o111)
base='https://raw.githubusercontent.com/gradle/gradle/v'+version+'/'
for name in ['gradlew','gradlew.bat','gradle/wrapper/gradle-wrapper.jar']:save(base+name,R/name)
(R/'gradlew').chmod(0o755)
properties='distributionBase=GRADLE_USER_HOME\ndistributionPath=wrapper/dists\ndistributionUrl=https\\://services.gradle.org/distributions/gradle-'+version+'-bin.zip\ndistributionSha256Sum='+expected+'\nnetworkTimeout=10000\nvalidateDistributionUrl=true\nzipStoreBase=GRADLE_USER_HOME\nzipStorePath=wrapper/dists\n'
if (R/'gradle/wrapper/gradle-wrapper.properties').read_text()!=properties:raise ValueError('Committed wrapper properties differ from pinned expected configuration')
puml='1.2025.10';url='https://repo.maven.apache.org/maven2/net/sourceforge/plantuml/plantuml/'+puml+'/plantuml-'+puml+'.jar';save(url,T/('plantuml-'+puml+'.jar'))
(R/'toolchain-bootstrap-receipt.json').write_text(json.dumps(dict(created_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),files=rows,original_lock_sha256=hashlib.sha256((R/'toolchain-lock.json').read_bytes()).hexdigest()),indent=2)+'\n');print(json.dumps(dict(files=len(rows),gradle=version,plantuml=puml,task_local_only=True)))

import subprocess,os
marp=R/'.tools/marp';marp.mkdir(parents=True,exist_ok=True)
for original,target in [('marp-package.json','package.json'),('marp-package-lock.json','package-lock.json')]:
 import shutil
 shutil.copyfile(R/'tooling'/original,marp/target)
subprocess.run(['npm','--userconfig',os.devnull,'--cache',str(R/'.tools/npm-cache'),'ci','--prefix',str(marp),'--ignore-scripts','--no-audit','--no-fund'],check=True)
