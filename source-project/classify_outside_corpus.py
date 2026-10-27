"""Evidence-based metadata audit of projects outside the original source corpus.
Fetches commit/tree objects only into a NEW task-local bare-cache directory.
No checkout, blob/build download, device operation or global Git setting change.
"""
import pathlib,json,datetime,collections,time,sys,re,xml.etree.ElementTree as E
R=pathlib.Path(__file__).resolve().parent
import acquire_trees
D=R/'outside-corpus-audit';D.mkdir(exist_ok=True);acquire_trees.R=D
projects=json.loads((R/'project-coverage.json').read_text());manifest=E.parse(R/'probes/manifest-stable').getroot();default=manifest.find('default');default_rev=default.get('revision') if default is not None else None
by_name={p.get('name'):dict(p.attrib) for p in manifest.findall('project')}
def now():return datetime.datetime.now(datetime.timezone.utc).isoformat()
def match(path):
 leaf=path.rsplit('/',1)[-1]
 if path.endswith('.aidl'):return 'aidl'
 if leaf=='AndroidManifest.xml':return 'manifest'
 if '/api/' in '/'+path and leaf.endswith('current.txt'):return 'signature'
 if leaf in ('RequiresPermission.java','RequiresNoPermission.java','EnforcePermission.java','PermissionManuallyEnforced.java'):return 'permission-annotation-definition'
 return None
entries=[]
for project in projects['projects']:
 if project['selected']:continue
 p=dict(project);p.update(classification='unknown',reason='Complete pinned file-tree metadata has not been enumerated; omission is not evidence of absence',evidence=dict(manifest_commit=projects['manifest_revision'],manifest_path=p['path'],project_name=p['name']),requested_revision=by_name.get(p['name'],{}).get('revision',default_rev or 'refs/tags/'+projects['tag']));entries.append(p)
entries.sort(key=lambda p:(0 if p['path'].startswith(('system/','hardware/')) else 1,p['path']))
def save(status):
 doc=dict(updated_at=now(),status=status,original_selected_projects=102,outside_projects=len(entries),global_complete=False,scope='AOSP public SDK/Binder/AIDL/permission source discovery; original release source inventory remains immutable',counts=dict(collections.Counter(p['classification'] for p in entries)),classification_rules=dict(relevant='Pinned complete tree contains at least one declared source-kind path; matching paths enumerated in full',irrelevant='Pinned tree is empty, so it contains no source entries',unknown='Tree unavailable/uninspected, or no declared-kind match while native/generated/other Binder sources cannot be ruled out'),projects=entries)
 tmp=D/'classification.json.tmp';tmp.write_text(json.dumps(doc,indent=2));tmp.replace(D/'classification.json')
save('initialized; no global completeness claim')
limit=next((int(a.split('=',1)[1]) for a in sys.argv if a.startswith('--limit=')),20);completed=0
for project in entries[:limit]:
 repo=project['name'];cached=D/'trees'/(repo.replace('/','__')+'.json')
 network_fetch=not cached.exists()
 if not re.fullmatch(r'[A-Za-z0-9_./-]+',repo) or '..' in pathlib.PurePosixPath(repo).parts:raise ValueError('Unsafe manifest project name')
 for attempt in range(3):
  try:
   if not cached.exists():acquire_trees.tree(repo,project['requested_revision'])
   tree=json.loads(cached.read_text());matches=[dict(path=e['path'],kind=match(e['path']),git_blob=e['oid']) for e in tree['entries'] if e['kind']=='blob' and match(e['path'])]
   project.update(revision=tree['revision'],tree_entries=len(tree['entries']),tree_evidence=str(cached.relative_to(R)),matches=matches,matching_paths=len(matches))
   if matches:project.update(classification='relevant',reason='Complete pinned tree contains declared source kinds; all matching paths enumerated')
   elif not tree['entries']:project.update(classification='irrelevant',reason='Verified empty pinned tree')
   else:project['reason']='Complete tree has no declared-kind match; native, generated or other Binder/interface sources remain unexcluded'
   break
  except Exception as error:
   message=str(error);project['error']=message;project['attempted_at']=now()
   if re.search(r'\b(?:401|403)\b',message):project['reason']='Access denied; stopped audit without alternate route';save('stopped on access denial');raise SystemExit(2)
   if '429' in message and attempt<2:
    save('temporary429; same target, conservative backoff');time.sleep(60);continue
   project['reason']='Metadata enumeration failed; remains unknown';break
 completed+=1
 if network_fetch or completed%10==0 or completed==limit:save('running' if completed<limit else 'bounded batch complete')
 print(json.dumps(dict(completed=completed,repo=repo,classification=project['classification'],matching_paths=project.get('matching_paths'))),flush=True)
 if network_fetch:time.sleep(2)
save('bounded batch complete');print(json.dumps(dict(audited=completed,counts=dict(collections.Counter(p['classification'] for p in entries))),indent=2))
