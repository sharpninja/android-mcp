"""Apply deterministic corrections to isolated prototype copies, never active sources."""
import pathlib,json,re,hashlib
R=pathlib.Path(__file__).resolve().parent; C=R/'catalog'

# These are a corrected NEW baseline. Original migrations are preserved as evidence.
for name in ('001-initial.sql','002-evidence-and-publication.sql'):
 p=C/'migrations'/name; s=p.read_text()
 (R/'evidence'/('original-'+name)).write_text(s)
 s=re.sub(r'\b(\w+) TEXT PRIMARY KEY',r'\1 TEXT NOT NULL PRIMARY KEY',s)
 s=s.replace('registry_id TEXT REFERENCES registries,source_id TEXT REFERENCES sources','registry_id TEXT NOT NULL REFERENCES registries,source_id TEXT NOT NULL REFERENCES sources')
 s=s.replace("status TEXT NOT NULL CHECK(status IN ('staged','published','retired'))", "state TEXT NOT NULL CHECK(state IN ('staged','validated','rejected','published','retired'))")
 s=s.replace("status TEXT NOT NULL CHECK(status IN ('staged','validated','rejected','published'))", "state TEXT NOT NULL CHECK(state IN ('staged','validated','rejected','published','retired'))")
 s=s.replace("'all','any','value','conditional'", "'all','any','permission','access','conditional'")
 if name.startswith('002'):
  s=s[:s.index('CREATE VIEW runtime_catalog AS')]+'''-- ETag composition is identical for base, overlay, tombstone and history CAS.
CREATE TABLE catalog_view(singleton INTEGER PRIMARY KEY CHECK(singleton=1),registry_generation INTEGER NOT NULL CHECK(registry_generation>0),overlay_generation INTEGER NOT NULL CHECK(overlay_generation>=0));
INSERT INTO catalog_view VALUES(1,1,0);
CREATE TRIGGER overlay_generation_insert AFTER INSERT ON overlays BEGIN UPDATE catalog_view SET overlay_generation=overlay_generation+1; END;
CREATE TRIGGER overlay_generation_update AFTER UPDATE ON overlays BEGIN UPDATE catalog_view SET overlay_generation=overlay_generation+1; END;
CREATE TRIGGER overlay_generation_delete AFTER DELETE ON overlays BEGIN UPDATE catalog_view SET overlay_generation=overlay_generation+1; END;
CREATE VIEW runtime_catalog AS
 SELECT e.entry_id,coalesce(o.document_json,e.document_json) AS document_json,
 '\"cat1:'||v.registry_generation||':'||e.record_sha256||':'||e.entry_id||':'||coalesce(o.revision,0)||':'||coalesce(o.state,'active')||'\"' AS etag
 FROM effective_entries e CROSS JOIN catalog_view v LEFT JOIN overlays o ON o.imported_binding_id=e.entry_id
 WHERE o.entry_id IS NULL OR o.state='active'
 UNION ALL
 SELECT o.entry_id,o.document_json,'\"cat1:'||v.registry_generation||':local:'||o.entry_id||':'||o.revision||':'||o.state||'\"'
 FROM overlays o CROSS JOIN catalog_view v WHERE o.imported_binding_id IS NULL AND o.state='active';
PRAGMA user_version=3;
COMMIT;
'''
 p.write_text(s)

p=C/'tooling/build_catalog.py';s=p.read_text().replace("{'permission':'value','access':'value'}.get(ast['op'],ast['op'])","ast['op']")
s=s.replace("'\"sha256:'+digest+'\"'", "'\"cat1:1:'+digest+':'+bid+':0:active\"'")
s=s.replace("'1.1.0'","'1.2.0'").replace('"1.1.0"','"1.2.0"')
p.write_text(s)

p=C/'tooling/parsers.py';s=p.read_text()
s=s.replace("for ordinal,raw in enumerate(chunks):", "offset=0\n   for ordinal,raw in enumerate(chunks):\n    locator=body.find(raw,offset);offset=locator+len(raw)")
s=s.replace("body[:body.find(raw)].count('\\n')", "body[:locator].count('\\n')")
p.write_text(s)

p=C/'contracts/openapi.json';d=json.loads(p.read_text());d['info']['version']='1.1.0'
d['info']['description']+=' Normative semantics: ../API-CONTRACT.md. One cat1 strong ETag model; no bridge application or HTTP implementation is delivered.'
for path,methods in d['paths'].items():
 for method,op in methods.items():
  if not isinstance(op,dict):continue
  for param in op.get('parameters',[]):
   if param.get('name')=='If-Match':
    param['required']=False;param['description']='Semantically required: missing returns 428 before schema validation; malformed or wildcard/list/weak tag returns 400; stale current cat1 ETag returns 412. Use current ETag from GET or history even for conflict or tombstone.'
  allowed={'400','401','403'}
  if method=='get':allowed|={'200','404'}
  if method=='get' and path=='/v1/catalog/entries':allowed|={'410'}
  if method=='get' and path.endswith('/{entry_id}'):allowed|={'410'}
  if method=='post':allowed|={'200','201','202','409','422'}
  if method in ('put','delete'):allowed|={'200','204','404','409','412','422','428'}
  op['responses']={k:v for k,v in op.get('responses',{}).items() if k in allowed}
  for status,response in op['responses'].items():
   if not (status in ('200','201','204','410','412') and ('{entry_id}' in path or method=='post')):response.pop('headers',None)
def fix(x):
 if isinstance(x,dict):
  for k,v in x.items():
   if k=='description' and isinstance(v,str) and ('overlay revision' in v.lower() or 'etag' in v.lower()) and ('ETag' in v or 'etag' in v):
    x[k]='Current strong cat1 ETag defined in API-CONTRACT.md. History returns current administrative ETag, never a historical revision as a write precondition.'
   else:fix(v)
 elif isinstance(x,list):
  for v in x:fix(v)
fix(d)
p.write_text(json.dumps(d,indent=2)+'\n')

p=R/'design/Android MCP Bridge Design.md';s=p.read_text()
s=s.replace('Keep v1 deliberately simple: one rule per client and exact tool version, with Deny, Ask or Allow plus a typed scope.','Use one policy evaluator over canonical binding identities and exact schema hashes, with Deny, Ask or Allow plus typed constraints. A required entry-point tool rule can only restrict binding authority.')
start=s.index('evaluate(principal, plan, snapshot):');end=s.index('```',start)
s=s[:start]+'''evaluate(principal, plan, snapshot):
  require active(principal) and currentCredential(principal)
  resolve generated, generic and known raw calls to identical canonical bindings
  reject known raw calls that cannot decode and validate the canonical arguments
  require a schema-bound binding rule for EVERY binding; absence or stale hash denies
  unknown raw calls require an explicit broad raw backend/service grant and tool rule
  collect binding rules, required entry-point rule and global/adapter guardrails
  if any deny or unknown semantics or arguments outside ANY constraint: DENY
  if not capabilityAvailableNow(plan): OS_UNAVAILABLE
  if any ask or globalAskFloor(plan): require validOneUseApproval(principal, plan, snapshot)
  recheck credential, registry/schema/target/policy versions at admission
  return ADMIT_CANDIDATE  // commit admission before dispatch
'''+s[end:]
s=s.replace('IDs are opaque random TEXT values;', 'Client, credential, execution and audit IDs are opaque random TEXT values; binding_id and entry_id are the same SHA-256 canonical source-signature identity defined in catalog/README.md, independent of source occurrence;')
s=s.replace('| 4 | Exact client rule exists and is not Deny | Deny by default |','| 4 | Every canonical binding has an exact schema-bound client rule and the entry-point rule exists; no rule denies | Deny by default |')
s=s.replace('| binding_definitions PK binding_id | source_id FK binding_sources RESTRICT; qualified_name; signature; schema_hash; type_schema_id FK binding_type_schemas; dispatch_kind; interface_descriptor nullable; transaction_code nullable; mapping_state; reason_code nullable; api_min/api_max nullable; declared_requirements_json; requirement_provenance; registry_revision. UNIQUE(source_id, qualified_name, signature). |','| binding_definitions PK binding_id | Same identity as catalog.bindings.binding_id/entry_id; canonical source signature UNIQUE. Source presence is occurrences(binding_id,source_id,line), with immutable per-source declarations and permission ASTs. Runtime mappings are separately keyed by binding_id plus schema_hash; unknown codecs or dispatch remain visible and noninvocable. |')
start=s.index('| API shape | Behavior |');end=s.index('Catalog administration uses',start)
s=s[:start]+'''`catalog/contracts/openapi.json` and `catalog/API-CONTRACT.md` are authoritative. There is one route family; no PATCH alias.

| API shape | Behavior |
| --- | --- |
| GET /v1/catalog/entries | Search pinned registry/overlay view; expired cursor 410. |
| GET /v1/catalog/entries/{entry_id} | Effective entry and current cat1 ETag; conflicts visible, tombstone 410 with current ETag. |
| POST /v1/catalog/entries | Create local entry; canonical ID collision 409; Idempotency-Key required. |
| PUT /v1/catalog/entries/{entry_id} | If-Match current cat1 ETag, including conflicts/tombstones. Overlay mutation, restore or explicit rebase; no imported mutation. |
| DELETE /v1/catalog/entries/{entry_id} | If-Match; suppression/deletion tombstone; conflict requires explicit resolution first (409). |
| GET /v1/catalog/entries/{entry_id}/history | Immutable history and current administrative ETag for CAS. |
| POST /v1/catalog/imports | Idempotent immutable staging and validation, no active view change. |
| POST /v1/catalog/registries/{registry_id}/publish | Idempotent atomic publication of validated registry, overlay rebase/conflicts, generation increment and approval invalidation. |

Executable catalog DDL, configuration DDL, offline dispatch manifest generator and contract fixtures accompany this design. They specify and validate contracts; Android hosting and dispatch remain release gates requiring actual implementation/runtime evidence.

'''+s[end:]
s+='''

## Remediation contract precedence

This revision resolves recovered findings F-01–F-08 against current artifacts. Stable source-signature IDs are shared across imported occurrences and local overrides; registry schema/permission changes are revisions, not new random identities. No universal OEM/private introspection is promised: unsupported declarations and schemas remain discoverable with evidence and reasons. Catalog metadata is distinct from OS authority and MCP grants. `catalog/API-CONTRACT.md` defines all CRUD states, CAS and cursor rules; `catalog/bridge-config.sql` makes configuration invariants executable; `catalog/tooling/policy_reference.py` and `dispatch_manifest.py` provide offline contract fixtures, not a running bridge. Existing section diagrams remain editable Mermaid; Pele 1.10.0 rendering is a separate packaging check.
'''
p.write_text(s)
