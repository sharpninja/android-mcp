"""Read-only evidence hashing for the single combined review owner."""
import pathlib,json,hashlib,datetime,sys
R=pathlib.Path(__file__).resolve().parent
version=sys.argv[1] if len(sys.argv)>1 else 'v1'
if not version.replace('-','').isalnum():raise ValueError('Safe evidence version')
out=R/('integration/combined-hv-source-inputs-'+version+'.json')
if out.exists():raise ValueError('Preserve previous review evidence manifest')
def digest(path):
 h=hashlib.sha256()
 with path.open('rb') as stream:
  while chunk:=stream.read(4*1024*1024):h.update(chunk)
 return h.hexdigest()
def record(name):
 p=R/name
 return dict(path=name,bytes=p.stat().st_size,sha256=digest(p)) if p.is_file() else dict(path=name,status='not yet produced')
names=['release010-evidence/release-manifest.json','validation-report-release010.json','build-release011-inputs.json','build-release011-status.json','coverage-manifest-release011.json','catalog/output/release-011/validation.json','validation-report-release011.json','permission-export-release011/permission-coverage.json','production-reconciliation-release011.json','export-reconciliation-release011.json','device-inventory-reconciliation-release011.json','release011-evidence/release-manifest.json','annotation-archive-comparison-006-to-011.json','integration/release011-staging-counts-export-phase.json','integration/round012-source-local-integration.json','integration/manifest-source012-actions.json','integration/manifest-source012-full-regression.log','integration/manifest-source012-observable-regression.log','integration/manifest-source012-optimized-regression.log','integration/interface-module-producer-v1-actions.json','integration/exact-interface-constraints-v1-actions.json','integration/apex-module-producer-v2-actions.json','integration/callback-functional-producer-v2-actions.json','integration/callback-audit-release011-v2/report.json','integration/functional-audit-release011-v1/report.json','integration/permission-reference-only-v1.1-actions.json','permission-reference-evidence-release011-v2/coverage.json','PermissionReferenceEvidence.schema.json','integration/next012-acquisition-preparation-v1.json','integration/SOURCE-BUILD-INTERFACE.md','integration/unsupported-manifests-normalization-v1.json','integration/scoped-permission-alias-device-policy-v1.json']
baseline=R/'build-snapshots/release011/catalog';candidate=R/'catalog-integrated012-sourcev2'
relative={str(p.relative_to(folder)) for folder in (baseline,candidate) for p in folder.rglob('*') if p.is_file() and '__pycache__' not in p.parts};differences=[]
for name in sorted(relative):
 a=baseline/name;b=candidate/name;left=digest(a) if a.is_file() else None;right=digest(b) if b.is_file() else None
 if left!=right:differences.append(dict(file=name,production011_sha256=left,candidate_sha256=right))
status=json.loads((R/'build-release011-status.json').read_text())
result=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),root=str(R),combined_review_owner='01a1793a-14dc-75d4-868d-7cb151f7702d',source_review_invoked=False,
 completed010='Fully validated immutable release010:802936entries,44397sources; exact counts/hashes in release010-evidence/release-manifest.json',
 release011_status=status,production011_build_complete=any(v['step']=='catalog/tooling/build_catalog.py' and v['returncode']==0 for v in status['steps']),production011_pipeline_complete=status['status']=='complete',
 candidate=dict(path=str(candidate),normal_tests=272,optimized_tests=272,production_equals_candidate=False,differences=differences,
  changes=['Inherited AIDL interface oneway propagated to members','Append-only wire proof revisions/CAS consumer fixture','Six late XML declarations normalized with raw/receipt preservation; empty fixture distinguished; unsupported binary retained as base64','Phase/counter logging added for future build observability']),
 source_handoffs_not_in_011=['Exact1199AIDL+592HIDL interface predicates require builder mapping/source-ID and proof input binding before production use','APEX163manifests/160build module declarations are source facts, not installed module intervals','Callback13fixture and functional6fixture producers have full normalized011 baseline scans but are not automatic stream/group implementation','Supplemental reference-only permission evidence1.1 preserves2names/12references with registry pin; primary PermissionSummary remains unchanged','Scoped Java permission alias helper is fixture-only: no same-snapshot generated Manifest.java witness; no production AST promoted'],
 scope_limits=['No global current-source completeness or98/98claim','Selected45,768 release011 acquisition paths complete; next acquisition draft46,075paths is not a production build','Native/generated/type/import/lifecycle/source annotation gaps remain named; unknown is not permission-free or runtime authority','No Android bridge/service/UI implemented or device operations performed by source task','Kotlin server/UI, internalARC protocol, Gradle and Markdown/StructurizrSVG design changes belong to combined design owner','No Library upload; explicit consent pending'],evidence=[record(name) for name in names])
out.write_text(json.dumps(result,indent=2));print(json.dumps(dict(path=str(out),sha256=digest(out),production011_pipeline_complete=result['production011_pipeline_complete'],candidate_difference_files=len(differences)),indent=2))
