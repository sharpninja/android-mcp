"""Read-only comparison of frozen review code and source owner's isolated candidate."""
import datetime,hashlib,json,pathlib
R=pathlib.Path(__file__).resolve().parent
C=R/'reviews/round-008/input/catalog'
S=pathlib.Path('/home/sharpninja/github/2026-10-26/task/android-catalog-20261026/catalog-round007')
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest() if p.is_file() else None
rows=[]
for p in sorted(C.rglob('*')):
    if not p.is_file() or p.suffix not in ('.py','.json','.md','.sql','.txt'):continue
    relative=p.relative_to(C);q=S/relative
    rows.append(dict(file=str(relative),review008_sha256=sha(p),source_candidate_sha256=sha(q),identical=sha(p)==sha(q)))
extras=[str(p.relative_to(S)) for p in sorted(S.rglob('*')) if p.is_file() and p.suffix in ('.py','.json','.md','.sql','.txt') and '__pycache__' not in p.parts and not (C/p.relative_to(S)).exists()]
out=dict(captured_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),review_frozen_root=str(C),source_candidate_root=str(S),source_active_files_modified=False,
 description='Comparison only, not permission to overwrite source-owned files; source-only tests are not counted as the125review tests.',
 files=rows,source_only_files=extras,matches=sum(r['identical'] for r in rows),differences=[r['file'] for r in rows if not r['identical']])
target=R/'integration/round008-source-comparison.json';target.write_text(json.dumps(out,indent=2)+'\n')
state=json.loads((R/'LIVE-STATUS.json').read_text());state.update(updated_at=out['captured_at'],frozen_snapshot_tests='evidence/round008-frozen-tests.txt',frozen_snapshot_tests_passed=125,source_candidate_comparison=str(target.relative_to(R)))
(R/'LIVE-STATUS.json').write_text(json.dumps(state,indent=2)+'\n')
print(json.dumps({k:out[k] for k in ('matches','differences','source_only_files')}))
