import hashlib,json,pathlib
R=pathlib.Path(__file__).resolve().parent;C=R/'catalog';S=pathlib.Path('/home/sharpninja/github/2026-10-26/task/android-catalog-20261026');a=json.loads((S/'integration/source011-structured-shapes-actions.json').read_text());assert hashlib.sha256(pathlib.Path(a['patch']).read_bytes()).hexdigest()==a['patch_sha256'];candidate=pathlib.Path(a['candidate'])
for row in a['files']:
 p=C/row['file'];source=candidate/row['file'];assert hashlib.sha256(source.read_bytes()).hexdigest()==row['candidate_sha256']
 assert (hashlib.sha256(p.read_bytes()).hexdigest() if p.exists() else None)==row['base_sha256'],row['file'];p.write_bytes(source.read_bytes())
(R/'evidence/source011-structured-shapes-merge.json').write_text(json.dumps(dict(source_actions=str(S/'integration/source011-structured-shapes-actions.json'),patch_sha256=a['patch_sha256'],files=a['files'],source_files_modified=False,independent_acceptance=False),indent=2)+'\n');print('Merged exact guarded owner structured-shape delta and7fixtures; frozen012 untouched')
