import pathlib,json,hashlib,datetime,shutil
R=pathlib.Path(__file__).resolve().parent;E=R/'evidence';S=pathlib.Path('/home/sharpninja/github/2026-10-26/task/android-catalog-20261026');B=S/'build-snapshots/release005/catalog';C=R/'catalog'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
rows=[]
for folder in ('tooling','contracts'):
 for p in sorted((B/folder).glob('*')):
  if not p.is_file() or p.suffix not in ('.py','.json'):continue
  name=str(p.relative_to(B));candidate=C/name;rows.append(dict(file=name,production_sha256=sha(p),candidate_sha256=sha(candidate) if candidate.exists() else None,matches_candidate=candidate.exists() and sha(p)==sha(candidate)))
inventory=S/'source-inventory-release005.json';input_receipt=S/'build-release005-inputs.json'
report=dict(captured_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),release='005',production_code_root=str(B),inventory_sha256=sha(inventory),build_inputs_sha256=sha(input_receipt),files=rows,production_equals_latest_candidate=all(r['matches_candidate'] for r in rows),limitations='Production005 predates later fixes; differences are explicit, not relabeled. Next build integration/caches remain separate from005 import evidence.')
(E/'production005-code-provenance.json').write_text(json.dumps(report,indent=2))
for name in ('production-reconciliation-release005.json','export-reconciliation-release005.json','build-release005-status.json','build-release005-inputs.json'):
 if (S/name).exists():shutil.copyfile(S/name,E/name)
print(json.dumps(dict(inventory_sha256=report['inventory_sha256'],files=len(rows),matches=sum(r['matches_candidate'] for r in rows))))
