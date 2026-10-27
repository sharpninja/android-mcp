"""Pin reviewed round007, then merge the isolated source annotation guard."""
import pathlib,json,hashlib,shutil,datetime
R=pathlib.Path(__file__).resolve().parent;H=pathlib.Path('/home/sharpninja/github/2026-10-26/task-3/remediation/integration');manifest=json.loads((H/'round007-source-actions.json').read_text());source=pathlib.Path(manifest['candidate_root']);base=R/'catalog-source-followup';target=R/'catalog-round007'
if target.exists():raise ValueError('Preserve earlier candidate')
shutil.copytree(base,target,ignore=shutil.ignore_patterns('__pycache__'));files=[]
for item in manifest['files']:
    path=source/item['file'];data=path.read_bytes()
    if hashlib.sha256(data).hexdigest()!=item['candidate_sha256']:raise ValueError('Reviewed file hash changed '+item['file'])
    out=target/item['file'];out.parent.mkdir(parents=True,exist_ok=True);out.write_bytes(data);files.append(dict(path=item['file'],reviewed_sha256=item['candidate_sha256']))
old=(base/'tooling/build_catalog.py').read_text();path=target/'tooling/build_catalog.py';new=path.read_text();anchor="      target_match_kind='current-sdk-index-exact'\n";end="     else:\n      from annotation_link import fallback_supported\n"
if old.count(anchor)!=1 or new.count(anchor)!=1:raise ValueError('Unexpected annotation branch layout')
branch=old.split(anchor,1)[1].split(end,1)[0]
if 'snapshot_declared_type_matches' not in branch:raise ValueError('Missing source type guard')
new=new.replace(anchor,anchor+branch,1)
plain="      if matched_canonical!=canonical:target_match_kind='java-lang-spelling-equivalence' if sep else 'package-namespace'"
qualified=next(line for line in old.splitlines() if line.startswith('      if matched_canonical!=canonical:'))
if new.count(plain)!=1:raise ValueError('Unexpected annotation evidence layout')
new=new.replace(plain,qualified,1);path.write_text(new)
receipt=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),review_manifest=str(H/'round007-source-actions.json'),target=str(target),source_annotation_patch='integration/same-snapshot-annotations-integration-v2.json',frozen006_modified=False,owner_originals_modified=False,files=[dict(record,integrated_sha256=hashlib.sha256((target/record['path']).read_bytes()).hexdigest()) for record in files])
(R/'round007-source-integration-receipt.json').write_text(json.dumps(receipt,indent=2));print('Integrated reviewed files',len(files),'and source type guard')
