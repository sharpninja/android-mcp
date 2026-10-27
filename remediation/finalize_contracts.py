import json,pathlib
R=pathlib.Path(__file__).resolve().parent;C=R/'catalog'
p=C/'contracts/openapi.json';d=json.loads(p.read_text());original=json.loads(pathlib.Path('/home/sharpninja/github/2026-10-26/task/android-catalog-20261026/catalog/contracts/openapi.json').read_text())
d['info']['description']='Specification only. No HTTP server, authentication provider or remote listener enabled. Catalog administration never grants Android invocation permission. Normative state, ETag and publication semantics: ../API-CONTRACT.md.'
for path,methods in d['paths'].items():
 for method,op in methods.items():
  op['description']=original['paths'][path][method].get('description','')
  if '{entry_id}' in path:
   op['description']+=' Current entry preconditions always use the cat1 ETag in API-CONTRACT.md.'
  if path.endswith('/history'):
   op['description']='Append-only history, paginated with generation-bound cursors. Response ETag is the current administrative cat1 ETag, not a historical revision. Existing imported entries without overlays return empty history plus their current ETag.'
   op['responses']['410']=original['paths'][path][method]['responses']['410']
  if method=='put':op['description']='Full validated Mutation replacement of local overlay only; canonical identity and imported evidence cannot change. Conflicts require x-resolution with current base hash; tombstones require current cat1 ETag and restore to active. Atomic history/revision append. See API-CONTRACT.md.'
  if method=='delete':op['description']='Create and return suppression/deletion tombstone Entry with new cat1 ETag (200). Already tombstoned and matching tag returns identical tombstone, unchanged revision (200). Conflicts return 409 until explicitly resolved. No recursive cascade.'
  for param in op.get('parameters',[]):
   if param.get('name')=='If-Match':
    param['required']=False
    param['description']='Semantically required. Missing returns 428 before generic validation; malformed/weak/list/wildcard returns 400; stale cat1 tag returns 412 with current tag. Obtain current tag from entry GET or history, including conflict/tombstone.'
  for status,response in op['responses'].items():
   if status=='412':response['description']='Current cat1 ETag does not match If-Match'
   if not '{entry_id}' in path and not (path=='/v1/catalog/entries' and method=='post'):response.pop('headers',None)
  if path.endswith('/publish'):
   op['parameters']=[x for x in op['parameters'] if x['name']!='If-Match']
   op['description']='Atomically publish a validated immutable registry only. Compare expected_active_registry_id under BEGIN IMMEDIATE; mark overlay collisions/rebases/conflicts, retire old registry, switch active pointer, increment registry generation and append history/audit/outbox. Same principal/operation/idempotency-key+request replays original result. Failed transaction leaves all state unchanged. No registry If-Match is required because immutable registry ID, validated state and expected active pointer form the CAS precondition.'
   op['responses']['404']=original['paths'][path][method]['responses']['404']
  if path=='/v1/catalog/entries' and method=='get':op['description']+=' include_suppressed=true includes suppressed, deleted and conflict states; default excludes all three. Any registry/overlay generation change expires the cursor with 410.'
p.write_text(json.dumps(d,indent=2)+'\n')
p=C/'README.md';s=p.read_text();s=s.replace('catalog storage version is `1.1.0`, SQLite `user_version=2`','corrected catalog storage baseline is `1.2.0`, SQLite `user_version=3`')
s=s.replace('`migrations/001-initial.sql` is retained unchanged from the input foundation.','This task corrects the isolated baseline migrations; originals are preserved under `../evidence`. Existing databases use checked offline copy migration `tooling/migrate_v3.py` to a new file; never rerun baseline migrations over a release.')
s=s.replace('Aggregate ETags use the complete entry representation hash.','Current entry ETags use the single cat1 formula in API-CONTRACT.md, including current registry generation, base hash, identity, overlay revision and state.')
s=s.replace('The unavailable core permission manifest means many protection levels and attributes are unknown.','Release-002 imported only 10 manifest definitions; the newer recovered core manifest contains 1,160 definitions and is not yet imported into that released database.')
s=s.replace('current SDK37.2 not acquired','current SDK37.2 not imported')
s='''# Isolated contract remediation candidate

This directory is a design/schema/extractor candidate, not a new production catalog release. Historical release-002 facts below describe the separately owned source task. The current source task has retrieved SDK 37.2 source archive revision 1, verified its published SHA-1, and retained 16,910 files; it has not imported that Java archive into release-002. Source completeness remains incomplete. Current progress and exact hashes are frozen separately for each review. See API-CONTRACT.md for corrected authority, identity, state, ETag, CAS and cursor rules. No semantic namespace re-sharding is authorized or performed.

'''+s
p.write_text(s)
# Keep an executable standalone schema identical to ordered baseline migrations.
(C/'schema.sql').write_text('\n'.join(p.read_text() for p in sorted((C/'migrations').glob('*.sql'))))
