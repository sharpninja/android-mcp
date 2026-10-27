import pathlib,json,shutil,hashlib,datetime
R=pathlib.Path(__file__).resolve().parent;E=R/'evidence';S=pathlib.Path('/home/sharpninja/github/2026-10-26/task/android-catalog-20261026');report=json.loads((R/'reviews/round-004/review-report.json').read_text())
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
write=lambda p,x:p.write_text(json.dumps(x,indent=2)+'\n')
now=lambda:datetime.datetime.now(datetime.timezone.utc).isoformat()
changes={
'F-01':('missing-production-input','Source production target now005, still staging;004 superseded.985excluded repositories and missing semantics unresolved. No production success claimed.'),
'F-02':('candidate-fixed','Shared aidl_lex ignores angle shifts/comparisons for AIDL statement separators; interface constants preserve subsequent methods; generic type headers skipped. Actual-source smoke reveals925additional member occurrences, including417methods.'),
'F-03':('candidate-fixed','Design raw constraints description now exact four scope-only JSON fields; all authoritative bounds stay SQL columns.'),
'F-04':('candidate-fixed','Additive1.1.0 kinds aidl-parcelable/aidl-union/aidl-enum identify parsed type declarations; source unsupported labels/issues only for actual unresolved syntax. Wire resolution remains unknown.'),
'F-05':('candidate-fixed','BEFORE INSERT/UPDATE pointer triggers reject active or suppressed imported overlays whose exact base hash is missing or changed, until conflict plus history is committed.'),
'F-06':('candidate-fixed','Append-only entry_history_reasons sidecar stores owner justification; HistoryEvent.reason returned by join. Registry.api_level nullable in all duplicated contract definitions. Offline oracle retains supplied reasons.'),
'F-07':('candidate-fixed','Explicit API index locator alignment ValueError survives python-O; optimized mocked alignment failure and all SDK index fixtures pass.'),
'F-08':('candidate-fixed','Canonicalizationv2 enumerates alljava/java-source/aidl/jvm namespaces and package infix; proven equivalence never silently retargets identity/grants.'),
'F-09':('candidate-fixed','permission_reader.read executable offline helper wraps unknown semantic values with retained parsed JSON and validates; known malformed nodes/unsupported major versions reject. Source bytes preserved separately, generated AST gate remains strict.')}
findings=[]
for f in report['findings']:
 status,change=changes[f['id']];findings.append(dict(**f,candidate_status=status,candidate_change=change,test_evidence='tests-regression-027.txt; round004-before.txt; round004-optimized.txt; round004-sdk-index-optimized.txt; legacy-populated-round4-schema-validation.json',reviewer_revalidation_pending=True))
ledger=dict(created_at=now(),previous_report_sha256=sha(R/'reviews/round-004/review-report.json'),previous_scores=report['scores'],previous_overall=report['overall'],findings=findings,additional_note='Signature per-line reconciliation is a no-drop locator-coverage guard, not independent proof of source grammar correctness. A mutant parser dropping a real fixture member is rejected before publication. Official current SDK index is separate evidence; production import and full coverage remain pending.')
write(E/'finding-ledger-005.json',ledger);write(E/'round004-finding-resolutions.json',ledger)
coverage=S/'coverage-manifest-release005.json';shutil.copyfile(coverage,E/'source-coverage-release005.json')
status=json.loads((S/'build-release005-status.json').read_text());write(E/'production-release005-status.json',dict(captured_at=now(),source_status=status,source_status_sha256=sha(S/'build-release005-status.json'),final_database_present=(S/'catalog/output/release-005/catalog.sqlite').exists(),final_validation_present=(S/'catalog/output/release-005/validation-report.json').exists(),production_success_claimed=False))
p=json.loads((E/'progress-004.json').read_text());p.update(captured_at=now(),production_target='release005 staging;004 superseded before publication',current_coverage_sha256=sha(coverage),tests_passed=93,source_task_owns_acquisition_and_production_import=True,latest_independent_scores=report['scores'],latest_independent_overall=report['overall'],candidate_changed_since_review=True,acceptance_claimed=False)
p['known_gaps']=[x.replace('release004','release005') for x in p['known_gaps']];write(E/'progress-005.json',p)
p=R/'catalog/README.md';text=p.read_text();text+='''

Signature reconciliation is a source-line no-drop guard, not an independently established semantic member count. Its lexer includes unsupported nonblank declaration lines; a mutant extractor dropping a fixture method fails publication. Grammar correctness uses targeted golden fixtures; actual current SDK membership is separately evidenced by the official lifecycle index, not inferred from this same line filter. Missing source/SDK semantics and production coverage remain explicit gaps. See tests-regression-027.txt and the latest finding ledger; no self-score is claimed.
''';p.write_text(text)
print(json.dumps(dict(prepared=now(),production_success_claimed=False,findings=len(findings))))
