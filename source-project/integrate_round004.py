"""Pin the reviewed next-candidate files; preserve005 and earlier candidates."""
import pathlib,json,hashlib,shutil,datetime
R=pathlib.Path(__file__).resolve().parent;H=pathlib.Path('/home/sharpninja/github/2026-10-26/task-3/remediation/integration');manifest=json.loads((H/'round004-source-actions.json').read_text());source=pathlib.Path(manifest['candidate_root']);target=R/'catalog-round004'
if target.exists():raise ValueError('Preserve existing next candidate')
shutil.copytree(R/'catalog-next',target,ignore=shutil.ignore_patterns('__pycache__'))
copied=[]
for item in manifest['files']:
 path=source/item['file']
 if not path.exists():path=source/'catalog'/item['file']
 data=path.read_bytes()
 if hashlib.sha256(data).hexdigest()!=item['candidate_sha256']:raise ValueError('Handoff hash changed '+item['file'])
 out=target/item['file'];out.parent.mkdir(parents=True,exist_ok=True);out.write_bytes(data);copied.append(dict(path=item['file'],sha256=item['candidate_sha256']))
(R/'round004-integration-receipt.json').write_text(json.dumps(dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),source=str(source),target=str(target),active005_modified=False,owner_originals_modified=False,files=copied),indent=2));print('Pinned',len(copied),'reviewed files')
