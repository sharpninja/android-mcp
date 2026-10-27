import pathlib,json,hashlib,datetime,shutil,difflib
R=pathlib.Path(__file__).resolve().parent;E=R/'evidence';S=pathlib.Path('/home/sharpninja/github/2026-10-26/task/android-catalog-20261026')
now=lambda:datetime.datetime.now(datetime.timezone.utc).isoformat()
write=lambda p,x:p.write_text(json.dumps(x,indent=2)+'\n')
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
coverage=S/'coverage-manifest-release004.json';shutil.copyfile(coverage,E/'source-coverage-release004.json')
project=json.loads((E/'project-coverage.json').read_text());rows=project['projects'];columns=list(dict.fromkeys(k for row in rows for k in row));values=[]
for row in rows:
 for value in row.values():
  if isinstance(value,str) and value not in values:values.append(value)
encoded=[[{'s':values.index(row[k])} if isinstance(row.get(k),str) else row.get(k) for k in columns] for row in rows]
compact={k:v for k,v in project.items() if k!='projects'};compact.update(columns=columns,string_codebook=values,projects=encoded,encoding='Each {s:N} is string_codebook[N]; columns indexes each row; null is missing/null')
decoded=[{k:values[v['s']] if isinstance(v,dict) else v for k,v in zip(columns,row) if v is not None} for row in encoded]
assert decoded==rows
(E/'project-coverage.compact.json').write_text(json.dumps(compact,separators=(',',':')))
report=json.loads((R/'reviews/round-003/review-report.reassembled.json').read_text())
changes={
'R3-01':('missing-production-input','All37873 selected paths downloaded per latest pinned coverage; release004 staging only;985 excluded repositories unenumerated and missing SDK semantics remain gaps.'),
'R3-02':('candidate-fixed','Method-only unique java.lang equivalence plus source annotation_link shadow/type-variable guards; SDK37.2 exact-index gate preserved.'),
'R3-03':('candidate-fixed','PermissionSummary emits own1.1.0 schema and validates every export document.'),
'R3-04':('storage-validated-semantic-rebuild-pending','Full135508 migrated entries validated current schema/AST/identity/hash/integrity/FK;552 declaration-variant omissions explicit; corrected semantic rebuild not claimed.'),
'R3-05':('candidate-fixed-production-pending','Integrated copied Java cache/API-index/Java-context/JVM equivalence importer and tests. Actual source004 not published/validated.'),
'R3-06':('candidate-fixed','Raw scope JSON has four scope fields only; authoritative identity/code/flags/hash/effect in SQL, uint32 constraints.'),
'R3-07':('candidate-fixed','One-published unique index, immediate pointer checks, deferred published-state FK; overlay/history deferred FK and JSON checks; staged validation.'),
'R3-08':('candidate-fixed','Active local/import collisions blocked at pointer/overlay writes; conflict suppressed; view joins stable entry identity once.'),
'R3-09':('candidate-fixed','Positive declared aggregate documented separately from absence; permission_variants retains ASTs/locators/absence facts.'),
'R3-10':('candidate-fixed','Reserved platform:local availability and verified app targets; explicit bounded local-owner audit maintenance transaction.'),
'R3-11':('candidate-fixed','Generated aliases and generic use binding.invoke; known raw additionally binder.transact; reference oracle verifies both.'),
'R3-12':('candidate-fixed','Import metadata required, history410 ETag removed, future semantic nodes quarantined raw in unknown wrapper.'),
'R3-13':('primary-source-corrected','Official Settings.System documents1..255. Retain documented writes; GET exposes observed int32 plus range/anomaly status without clamping.'),
'R3-14':('candidate-fixed','Generation seeded from active registry_version; migrated metadata rebuilt_with_corrected_extractor=false preserved.')}
findings=[]
for finding in report['findings']:
 status,change=changes[finding['id']];findings.append(dict(**finding,candidate_status=status,candidate_change=change,test_evidence='tests-regression-024.txt; legacy-populated-current-schema-validation.json',reviewer_revalidation_pending=True))
write(E/'round003-finding-resolutions.json',dict(previous_report_sha256=sha(R/'reviews/round-003/review-report.reassembled.json'),created_at=now(),findings=findings,additional_fix='AIDL structured fields/enumerants/interface constants inventoried by aidl_members.py; wire tags/layout/codecs and unsupported syntax explicitly unknown.',source_files_modified=False))
write(E/'finding-ledger-004.json',dict(previous_scores=report['scores'],previous_overall=report['overall'],findings=findings,acceptance='No self-score or overall acceptance claimed; pending fresh independent review.'))
progress=json.loads((E/'progress-003.json').read_text());progress.update(captured_at=now(),selected_paths=37873,retrieved_paths=37873,retrieved_by_kind=json.loads(coverage.read_text())['source_paths']['downloaded_by_kind'],production_target='release004 staging, no final database/validation',current_coverage_sha256=sha(coverage),tests_passed=85,source_pipeline_integrated=True,known_gaps=['985 excluded repositories require all-current-bindings relevance classification/file enumeration','release004 has not published or passed final validation','12 empty official Java members; lifecycle index missing field types/class kind/constants/generics/throws/annotations','Java wildcard/type-variable/inheritance resolution remains conservative','AIDL imports/runtime types/wire metadata/codecs still unverified; candidate now inventories structured fields/enumerants','552 legacy declaration variant omissions; legacy semantic applicability not rebuilt'],complete=False)
write(E/'progress-004.json',progress)
point=json.loads((E/'tree-sitter-copied-component-evidence.json').read_text());point.update(integrated_into_remediation_builder=True,production_cache_generation_performed=False,fixture_evidence='tests-regression-024.txt',exact_crash_connection='inferred, not independently proven');write(E/'tree-sitter-copied-component-evidence.json',point)
helper=R/'catalog/tooling/aidl_members.py';shutil.copyfile(helper,R/'integration/aidl_members.py')
write(R/'integration/aidl-members-integration.json',dict(created_at=now(),helper=str(helper),sha256=sha(helper),tests=str(R/'catalog/tests/test_aidl_members.py'),builder_regression='test_structured_aidl_members_survive_publication',integration=['Import enrich from aidl_members','Replace d=aidl(text) by d=enrich(text,aidl(text))','Include aidl_members.py in processing_fingerprint'],source_parser_unchanged=True,java_cache_regeneration_required=False,limitations='Declaration metadata only, no inferred wire tags/layout/codecs; unsupported raw syntax retained; do not hotpatch a running build.'))
status=json.loads((R/'LIVE-STATUS.json').read_text());status.update(updated_at=now(),tests_passed=85,current_phase='Preparing fresh round004 frozen review; round00314findings addressed except production coverage/rebuild blockers',migration_validation='evidence/legacy-populated-current-schema-validation.json',tests_evidence='evidence/tests-regression-024.txt',source_java_integrated=True,primary_input_blocker='Release004 staging not validated;985 excluded repositories and SDK semantics remain unresolved',aidl_integration='integration/aidl-members-integration.json',review_pid=None);write(R/'LIVE-STATUS.json',status)
p=R/'catalog/README.md';s=p.read_text();s+='''

## Current reviewed-candidate pipeline

The copied source pipeline supports pinned Java-source JSONL caches and current SDK lifecycle-index caches. Java declarations and JVM index entries have separate identities until conservative equivalence is proven. Java wildcard/import/type-variable/inheritance semantics and index-absent types/constants remain unknown where unproven. Dependencies are pinned in requirements-extraction.txt; no dependency installation was performed by this task. Point positions use tuple indexing; the diagnosed binding getter defect is distinct from the inferred connection to the prior exact crash.

aidl_members.py inventories structured parcelable/union fields, enumerants and interface constants as source metadata, preserving raw initializers and unknown syntax. Declaration ordinals are never asserted as wire tags. Imported runtime types, wire layouts/codecs, generated transactions and device reachability remain separate unresolved facts. This helper does not modify the Java cache parser.

Current reproduction evidence is evidence/tests-regression-024.txt (85 tests) and evidence/legacy-populated-current-schema-validation.json (135508 legacy entries; storage validation passes, semantic rebuild false,552 variant omissions). New production release004 remains staging; no final production validation is claimed. Acquisition100% of selected paths does not establish all-current-bindings coverage across985 excluded repositories.
''';p.write_text(s)
shutil.copyfile(S/'requirements-extraction.txt',R/'catalog/requirements-extraction.txt')
print(json.dumps(dict(prepared_at=now(),tests=85,source_active_files_modified=False,project_table_roundtrip=True)))
