import pathlib,json
R=pathlib.Path(__file__).resolve().parent;C=R/'catalog'
p=C/'tests/test_remediation.py';s=p.read_text().replace("'now','validated','{}'))", "'now','published' if n==1 else 'validated','{}'))")
s=s.replace("        db.execute(\"UPDATE active_registry SET registry_id='2' WHERE singleton=1\")", "        db.execute(\"UPDATE registries SET state='retired' WHERE registry_id='1'\")\n        db.execute(\"UPDATE registries SET state='published' WHERE registry_id='2'\")\n        db.execute(\"UPDATE active_registry SET registry_id='2' WHERE singleton=1\")\n        db.commit()")
p.write_text(s)
p=C/'tests/test_build.py';s=p.read_text();anchor="   self.assertEqual(db.execute('SELECT count(*) FROM runtime_catalog').fetchone()[0],count-1)";s=s.replace(anchor,"   db.execute('INSERT INTO entry_history VALUES (?,?,?,?,?,?,?)',(bid,1,'suppress','{}','fixture',now))\n"+anchor)
anchor="   with self.assertRaises(sqlite3.IntegrityError):db.execute('INSERT INTO occurrences";s=s.replace(anchor,"   db.execute('INSERT INTO entry_history VALUES (?,?,?,?,?,?,?)',(bid,2,'restore',json.dumps({'fixture':'override'}),'fixture',now))\n   db.commit()\n"+anchor);p.write_text(s)
p=C/'migrations/002-evidence-and-publication.sql';s=p.read_text().replace('registry_generation=registry_generation+1; END;', 'registry_generation=max(registry_generation+1,(SELECT registry_version FROM registries WHERE registry_id=new.registry_id)); END;')
s=s.replace('CREATE VIEW effective_entries AS', '''CREATE TRIGGER registry_generation_initial AFTER INSERT ON active_registry BEGIN UPDATE catalog_view SET registry_generation=max(registry_generation,(SELECT registry_version FROM registries WHERE registry_id=new.registry_id)); END;
CREATE VIEW effective_entries AS''');p.write_text(s)
(C/'schema.sql').write_text('\n'.join(p.read_text() for p in sorted((C/'migrations').glob('*.sql'))))
p=C/'bridge-config.sql';s=p.read_text().replace('CHECK(code_min>=0)','CHECK(code_min BETWEEN 0 AND 4294967295)').replace('CHECK(code_max>=code_min)','CHECK(code_max>=code_min AND code_max<=4294967295)').replace('CHECK(flags_mask>=0)','CHECK(flags_mask BETWEEN 0 AND 4294967295)');p.write_text(s)
p=C/'tooling/dispatch_manifest.py';s=p.read_text().replace("result.append(dict(tool_name='binding_'+bid,binding_id=bid", "result.append(dict(tool_name='binding_'+bid,policy_tool_id='binding.invoke',binding_id=bid");p.write_text(s)

p=R/'design/Android MCP Bridge Design.md';s=p.read_text()
s=s.replace('Preserve unrecognized extension nodes', 'Preserve x- properties on known semantic nodes')
s+='''

## Canonical permission entry points and retained unknown metadata

Generated per-binding MCP tool names and binding.invoke share the single policy tool_id `binding.invoke`. The protocol alias name is stored separately in tool_binding_aliases.alias_name; tools/client_tool_rules represent the shared policy entry point, not135,000 independent shadow permission rows. Editing a generated alias in the UI edits its canonical client_binding_rules row. Convenience wrappers have their own restrictive entry-point rules in addition to every required binding grant. A known binder.transact call must also satisfy binding.invoke and its canonical binding rules, then the extra binder.transact restriction; unknown raw calls use their separate explicit raw grants and binder.transact rule. Equal route constraints produce equal decisions; an extra route-specific restriction can only narrow access.

PermissionExpression accepts only known op values. Future x- properties on known nodes are retained; an unrecognized semantic node is quarantined as a known unknown wrapper with reason plus exact raw JSON in x-raw-node (and required_semantics as appropriate), never interpreted as permission-free. Original imported bytes remain in versioned evidence. Unsupported major versions stay unsupported for invocation.

settings.brightness.get reports the actual observed signed32-bit rawValue without clamping, plus withinDocumentedRange and the cited documentedRange1..255. An out-of-range observation (including0 on a differing device) is returned with an anomaly flag, rather than fabricated as a valid documented setting or rejected by the output range. The write policy remains bounded to the documented range with default16..240; no automatic mode change is implied.

Native availability uses the reserved target_key platform:local; app/runtime targets must exist in target_app_profiles. Audit retention is a separate authenticated local-owner maintenance transaction: set bridge_config.audit_retention_authorized=1, perform bounded retention deletes, append maintenance audit evidence, reset the flag to0 before commit. Startup resets a stale flag before admitting calls. No remote catalog or MCP device endpoint can enable maintenance or issue SQL. Optional client-log purge remains separate from mandatory audit retention.
'''
# Replace the old read range statement wherever it appears in the table.
s=s.replace('rawValue 1..255','rawValue observed int32 plus withinDocumentedRange; documented range1..255')
p.write_text(s)
p=C/'API-CONTRACT.md';s=p.read_text()+'''

## Publication/storage enforcement and aggregation

Only one registry may be published. active_registry has immediate admission triggers and a deferred composite FK to the published registry state; retiring the old registry and selecting the new one occur in a single transaction, so a retired pointer cannot commit. The builder validates private registry_entries before publishing and installs its active pointer only afterward. Every overlay revision has a deferred FK to the same entry_history revision, forcing history presence at commit; JSON validity is checked in storage and full Entry/Mutation schemas are checked by the service. Active local/import collisions must become explicit conflicts before pointer publication; runtime discovery joins overlays by stable entry_id and never emits duplicate IDs. Failed publication rolls back entries, state, pointer, history and generations.

The permission aggregate is positive declared-evidence discovery, not a universal claim for every source version or an OS grant. A plain absence of a recognized annotation is excluded when selecting a single positive requirement; it is never explicit-none. In mixed positive/absent or differing-evidence cases, extensions.permission_variants retains each AST with x-occurrences and x-absence-of-annotation where applicable. Invocation still resolves the exact target/source/schema and live OS authority; it may not infer no permission from absence or use the aggregate to widen client grants. An unresolved/conflicting positive expression makes the aggregate unknown.

RawGrant.schema.json is the closed scope-only contract for constraints_json (allowed_targets, allowed_handles, allowed_displays, max_items). Connection/descriptor/code range/flag mask/hash/effect live only in SQL columns; duplicate keys in constraints_json are rejected. ImportRequest carries registry_version and coverage before staging validation. Generated aliases and binding.invoke share policy_tool_id binding.invoke; wrappers retain additional restrictive rules.
''';p.write_text(s)
p=C/'README.md';s=p.read_text()+'''

PermissionSummary exports use their own1.1.0 contract and are validated against PermissionSummary.schema.json. Catalog storage remains1.2.0. Positive declared permission evidence is aggregated separately from absent annotations; mixed variants and their source/locator associations remain in extensions.permission_variants. Absence never means explicit-none. See API-CONTRACT.md for publication/history constraints and policy entry-point aliases.
''';p.write_text(s)
