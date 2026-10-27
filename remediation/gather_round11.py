"""Read-only production008 evidence capture; no producer/cache changes."""
import datetime,hashlib,json,pathlib,shutil,sqlite3
R=pathlib.Path(__file__).resolve().parent;E=R/'evidence';S=pathlib.Path('/home/sharpninja/github/2026-10-26/task/android-catalog-20261026')
now=lambda:datetime.datetime.now(datetime.timezone.utc).isoformat();sha=lambda b:hashlib.sha256(b).hexdigest()
for name in ('permission-coverage.json','permission-expression-samples.json'):
    shutil.copyfile(S/'permission-export-release008'/name,E/name.replace('.json','-release008.json'))
shutil.copyfile(S/'build-release008-inputs.json',E/'build-release008-inputs.json')
raw=(E/'coverage-manifest-release008.json').read_bytes();data=json.loads(raw);compact=json.dumps(data,separators=(',',':'),ensure_ascii=False).encode();assert json.loads(compact)==data
(E/'coverage-manifest-release008.compact.json').write_bytes(compact)
(E/'coverage-transport-round011.json').write_text(json.dumps(dict(captured_at=now(),semantic_roundtrip_equal=True,original=dict(file='coverage-manifest-release008.json',size_bytes=len(raw),sha256=sha(raw)),transport=dict(file='coverage-manifest-release008.compact.json',size_bytes=len(compact),sha256=sha(compact)),operation='Whitespace-only serialization; every key, path, row, count and unresolved gap preserved; original retained'),indent=2)+'\n')
raw=(S/'outside-corpus-audit/classification.json').read_bytes();data=json.loads(raw);(E/'outside-corpus-classification-round011-full.json').write_bytes(raw)
reasons=list(dict.fromkeys(p.get('reason','') for p in data['projects']));summary={k:v for k,v in data.items() if k!='projects'};summary.update(captured_at=now(),full_report_sha256=sha(raw),columns=['name','path','classification','reason_index','revision','tree_entries','matching_paths'],reason_codebook=reasons,projects=[[p.get('name'),p.get('path'),p.get('classification'),reasons.index(p.get('reason','')),p.get('revision'),p.get('tree_entries'),p.get('matching_paths')] for p in data['projects']],scope_note='Every outside project retained; matched path strings remain in hashed full report locally, not in this summary. Classification is not completed import.')
(E/'outside-corpus-classification-latest-summary.json').write_text(json.dumps(summary,separators=(',',':'))+'\n')
p=S/'catalog/output/release-008/catalog.sqlite';before=p.stat();h=hashlib.sha256()
with p.open('rb') as f:
    for block in iter(lambda:f.read(8*1024*1024),b''):h.update(block)
after=p.stat();assert (before.st_size,before.st_mtime_ns)==(after.st_size,after.st_mtime_ns)
(E/'catalog-size-release008.json').write_text(json.dumps(dict(captured_at=now(),database=str(p),size_bytes=after.st_size,sha256=h.hexdigest(),mtime_ns=after.st_mtime_ns,stable_during_capture=True,read_only=True,reviewed_directly_by_claude=False),indent=2)+'\n')
schema=json.loads((E/'validation-report-release008.json').read_text());permission=json.loads((E/'permission-coverage-release008.json').read_text())
assert schema['entries_validated']==799527 and schema['integrity_check']=='ok' and not schema['json_schema_failures'] and not schema['foreign_key_check']
print(json.dumps(dict(captured_at=now(),entries=schema['entries_validated'],sources=schema['counts']['sources'],unmatched_annotations=schema['external_annotations_unmatched'],permission_names=permission['permission_names'],definitions=permission['manifest_definition_records'],source_files_modified=False)))
