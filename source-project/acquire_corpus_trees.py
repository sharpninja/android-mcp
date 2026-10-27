import xml.etree.ElementTree as E,json,pathlib
from concurrent.futures import ThreadPoolExecutor,as_completed
from acquire_trees import tree,R
manifest=E.parse(R/'probes/manifest-stable').getroot()
projects=[]
for p in manifest.findall('project'):
 a=dict(p.attrib);path=a.get('path',a['name'])
 chosen=path.startswith(('frameworks/','packages/modules/','packages/services/')) or path in ('hardware/interfaces','system/hardware/interfaces','system/tools/aidl','libcore','system/core','prebuilts/sdk')
 a.update(selected=chosen,selection_reason='Declared framework/module/service/Binder corpus' if chosen else 'Outside declared bounded corpus; tree not enumerated')
 projects.append(a)
(R/'project-coverage.json').write_text(json.dumps(dict(manifest_revision='5bc9a7ce1cd78dd53613bbfd0ebf506e1e4adb0f',tag='android-17.0.0_r1',projects=projects),indent=2))
def work(p):
 repo=p['name'];cached=R/'trees'/(repo.replace('/','__')+'.json')
 if cached.exists():print('cached',repo,flush=True);return dict(repo=repo,status='enumerated')
 try:tree(repo,'refs/tags/android-17.0.0_r1');return dict(repo=repo,status='enumerated')
 except Exception as e:print('FAILED',repo,str(e),flush=True);return dict(repo=repo,status='failed',error=str(e))
results=[]
with ThreadPoolExecutor(max_workers=6) as pool:
 for r in pool.map(work,[p for p in projects if p['selected']]):
  results.append(r);(R/'tree-acquisition.json').write_text(json.dumps(results,indent=2))
