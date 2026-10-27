import pathlib,json,hashlib,datetime,collections,sqlite3,shutil,xml.etree.ElementTree as ET
R=pathlib.Path(__file__).resolve().parent;S=pathlib.Path('/home/sharpninja/github/2026-10-26/task/android-catalog-20261026');E=R/'evidence'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
inv=json.loads((S/'source-inventory.json').read_text());sources=inv['sources']
release=json.loads((S/'catalog/output/release-002/validation-report.json').read_text())
progress=dict(captured_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),source_inventory_sha256=sha(S/'source-inventory.json'),selected_paths=len(sources),retrieved_paths=sum(s['status']=='downloaded' for s in sources),retrieved_by_kind=dict(collections.Counter(s['kind'] for s in sources if s['status']=='downloaded')),released_snapshot='release-002',released_validation_report_sha256=sha(S/'catalog/output/release-002/validation-report.json'),released_entries=release['entries_validated'],released_source_count=release['counts']['sources'],released_permission_definitions=release['counts']['permission_definitions'],released_schema_integrity_evidence=release['integrity_check'],released_json_schema_failures=release['json_schema_failures'],released_fk_failures=release['foreign_key_check'],released_validation_time=release['validated_at'],source_task_owns_acquisition_and_production_import=True,complete=False,known_gaps=['3,135 declared source paths not retrieved at snapshot','newer acquired AIDL/manifests and core permission definitions not imported into release-002','SDK 37.2 Java source archive acquired but no current binding import into release-002','AIDL structured fields/enumerants/union tags unresolved; wire schemas not proven','no current evidence establishes all required source coverage'])
core=[s for s in sources if s['repository']=='platform/frameworks/base' and s['path']=='core/res/AndroidManifest.xml' and s['status']=='downloaded']
if core:
 p=S/core[0]['local_path'];progress['recovered_core_manifest']=dict(sha256=sha(p),permission_definitions=len(list(ET.fromstring(p.read_bytes()).iter('permission'))))
sdk=S/'sdk37.2/source-archive-receipt.json'
if sdk.exists():progress['sdk_37_2_source_receipt']=json.loads(sdk.read_text())
(E/'progress-001.json').write_text(json.dumps(progress,indent=2))
prior=json.loads((E/'prior-review.json').read_text())
fixes={
'F-01':('Current design still contradicted generated SHA-256 identity. Fixed design to catalog source-signature identity and occurrence model.','test_one_identity_two_immutable_occurrences'),
'F-02':('Current design evaluator omitted binding rules. Replaced with canonical plan evaluator plus restrictive entry-point rules. Offline decision algebra added.','test_binding_rules_dominate_all_aliases'),
'F-03':('Current design API family diverged. Unified routes to OpenAPI; executable config DDL and offline alias generator added. Runtime implementation remains required in a later phase.','test_bridge_ddl_integrity_and_closed_enums; test_generated_alias_identity_and_default_deny'),
'F-04':('Current SQL state/logic vocabulary diverged. Corrected baseline and checked copy migration preserve state and AST root.','test_registry_state_round_trip; test_permission_root_vocabulary; test_offline_copy_migration'),
'F-05':('Current SQL exposed twelve nullable natural PK fields including registry_sources. All corrected to explicit NOT NULL.','test_all_natural_primary_keys_are_not_null'),
'F-06':('Current projection used plural hash/overlay tags. Defined and applied cat1 entry ETags and missing/stale/history behavior; pruned operation responses.','test_cas_tombstones_history_and_cursor; full validate_catalog fixture'),
'F-07':('Current CRUD conflict/collision/cursor transitions incomplete. Added explicit transition table, global overlay-generation invalidation and offline state oracle.','test_import_both_collision_orders_and_rebase; test_cas_tombstones_history_and_cursor'),
'F-08':('Latest parser already corrected old annotation header/owner/symbolic/AIDL issues; regression tests verify. Newly found duplicate AIDL locator corrected; failed import emits validation-failure receipt before raising.','test_symbolic_read_and_header_do_not_leak_owner; test_repeated_aidl_statement_locators_are_distinct; test_hash_failure_does_not_publish')}
ledger=[]
for finding in prior['findings']:
 desc,test=fixes[finding['id']];ledger.append(dict(**finding,current_verification=desc,status='corrected-or-verified-in-candidate; awaiting-independent-review',test_evidence=test))
(E/'finding-ledger-001.json').write_text(json.dumps(dict(prior_review_sha256=sha(E/'prior-review.json'),prior_input_manifest_sha256=sha(E/'prior-input-manifest.json'),findings=ledger,additional_findings=[dict(id='N-01',issue='Repeated AIDL statements shared body.find locator',status='corrected',test='test_repeated_aidl_statement_locators_are_distinct')],overall_acceptance='NOT MET: no current Claude agreement, source coverage incomplete'),indent=2))
for name in ('assets','sources'):
 shutil.copytree(S/'design-input'/name,R/'design'/name,dirs_exist_ok=True)
for name in ('render.mjs','package.json','package-lock.json','render-manifest.json','PELE-LICENSE.txt'):
 shutil.copy2(S/'design-input'/name,R/'design'/name)
assert json.loads((R/'design/render-manifest.json').read_text())['version']=='1.10.0'
for p in (R/'design/assets').glob('*.svg'):ET.fromstring(p.read_bytes())
(E/'portable-assets-check.json').write_text(json.dumps(dict(renderer='Pele',version='1.10.0',new_render=False,source='Retained unchanged original editable Mermaid sources and rendered SVGs; no diagram semantic changes',files={str(p.relative_to(R/'design')):sha(p) for folder in ('assets','sources') for p in (R/'design'/folder).iterdir() if p.is_file()}),indent=2))
print(json.dumps({k:v for k,v in progress.items() if k not in ('sdk_37_2_source_receipt','known_gaps')},indent=2))
