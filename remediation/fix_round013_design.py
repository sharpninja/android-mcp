import pathlib,re,json,datetime
R=pathlib.Path(__file__).resolve().parent;P=R/'design-c4';p=P/'docs/Android MCP Bridge Design.md';s=p.read_text();before=s
replacements={
'Generated per-binding MCP tools are paginated;':'Generated per-binding aliases appear in tools/list only in the opt-in dynamic alias mode defined in the functional-group section;',
'The arbitrary-app convenience surface has two primary tools: screen.state and input.tap, alongside the complete generated binding inventory.':'The arbitrary-app convenience group provides screen.state and input.tap through a bounded portable manifest or opt-in dynamic aliases; these are not additional default bootstrap tools. The complete inventory remains separately searchable.',
'android.groups.set_active, binding.invoke, callback.status and callback.cancel.':'android.groups.set_active, binding.invoke, callback.status, callback.cancel, bindings.search, bindings.list and bindings.get. This nine-tool set is the authoritative default tools/list surface; convenience operations are group-delivered.',
'SDK/AIDL metadata ingestion, versioned schemas, code generation and generic marshalling':'Offline-generated SDK/AIDL metadata ingestion, versioned schemas and generic marshalling; no runtime bytecode generation',
'Use an Android single view application with MVVM and compiled bindings. Keep navigation state in view models and platform activity concerns behind services.':'Use one ComponentActivity hosting Jetpack Compose with ViewModel-held UI state and state hoisting. Navigation/saved state lives in ViewModels; platform concerns stay behind service-owned interfaces reached through the bounded local facade.',
'Generate invocation wrappers where required by trimming/AOT; reflection or JNI may be used where supported, not as a hidden-API bypass.':'Resolve public members through java.lang.reflect or supported MethodHandles from pinned JVM signatures; offline tooling alone generates metadata and reviewed adapters. R8 keep rules preserve required bridge entry points. Non-SDK blocks are explicit, never bypassed.',
'PUT/PATCH/DELETE':'PUT/DELETE',
'The no-query URI reads the earliest retained page.':'The no-query URI returns bounded subscription status, retention cursors and a next-read URI; cursor-expanded URIs page history.',
'| tool_binding_aliases PK tool_id plus binding_id |':'| tool_binding_aliases PK alias_name plus binding_id |',
'Default v1 has no durable event replay;':'Default v1 has no durable transport SSE replay; separately retained callback journals are read through resources/read;',
'Mcp-Session-Id: <bound-session-if-negotiated>':'Mcp-Session-Id: <required-initialized-owned-session>',
'Extend executions.state with dispatched':'executions.state already includes dispatched; extend its diagnostic metadata',
'Android35+':'API level35 (Android15) or later',
'Stable tool names include canonical binding identity; names cannot be reused for different contracts after refresh.':'Dynamic alias names use b.<first32hex(binding_id)>.<first24hex(schema_hash)> (59 ASCII characters, [A-Za-z0-9_.-]); full IDs/hashes remain in metadata and the canonical invocation mapping. Detect any prefix collision against full identities and refuse alias activation with generic-invoke fallback. Never reuse an alias for a different full contract.',
'Pending approvals, pairing attempts, sessions and raw arguments/results stay in memory and die on restart.':'Pending ordinary approvals, pairing attempts, MCP sessions and ordinary call arguments/results stay in memory and die on restart. The explicitly authorized bounded callback event journal and consumed registration-consent pins are functional storage exceptions defined below; they do not restore a pending owner approval or live OS registration.',
'verifier BLOB length 32; key_alias; state CHECK':'verifier BLOB length 32; kind CHECK mcp/admin; key_alias; state CHECK',
}
for a,b in replacements.items():
 if a not in s:raise ValueError('Expected text missing: '+a)
 s=s.replace(a,b)
s=s.replace('canonicalization_version; argument_mapping_hash. Generated tools','canonicalization_version; argument_mapping_hash; index(tool_id,binding_id); trigger enforcing one policy tool_id for every alias_name. Generated tools')
s=s.replace('Round011 independently assessed design95/95, contracts93/93, extractor94/92 and overall93/84 (NOT_READY).','Historical full-project review012 was NOT_READY overall93/80; the separate013 design review was91/88 and predates the latest ARC/C4/documentation changes.')
s=s.replace('Latest completed independent overall agreement remains93% accuracy/80% completeness, NOT_READY; its design category is94/95 and contracts95/94.','Latest full-project review012 remains overall93/80 NOT_READY. Separate design-only013 was91/88; it predates the internal ARC/C4/build/documentation revision and is not a current acceptance score.')
s=s.replace('The official source documents StreamableHTTP/CIO integration; that is not Android or exact2025-11-25 conformance proof.','The official SDK source documents StreamableHTTP integration; Ktor separately documents CIO. Neither is Android or exact2025-11-25 conformance proof.')
s=s.replace('## Verified design mechanisms after round013','## Verified design mechanisms after round013')
corrections=(P/'architecture/round013-design-corrections.md').read_text().replace('# Verified design mechanisms after round013','## Verified design mechanisms after round013',1)
corrections=corrections.replace('The official source documents StreamableHTTP/CIO integration; that is not Android or exact2025-11-25 conformance proof.','The official SDK source documents StreamableHTTP integration; Ktor separately documents CIO. Neither is Android or exact2025-11-25 conformance proof.')
s=s.rstrip()+'\n\n'+corrections
s+='''\n\nThe pinned official Settings.System reference states brightness is “between 1 (minimum) and 255 (maximum)” ([S15](https://developer.android.com/reference/android/provider/Settings.System#SCREEN_BRIGHTNESS)). This exact primary-source wording was rechecked for F-13; a reviewer's recollection of0..255 does not override it. Actual raw reads still return any observed signed32-bit value with the documented-range flag; the16..240 default write policy is a separate design restriction.\n\nThe opt-in dynamic group view uses initialization experimental capability android.bridge.toolGroups.v1 with requested mode dynamic_aliases and a server-confirmed matching capability; absent/mismatched agreement stays portable. The server additionally advertises standard tools.listChanged only when it will emit notifications/tools/list_changed. This application extension does not promise host context eviction.\n'''
# Move tests/decisions/references behind normative design extensions, preserving reference identities.
a=s.index('# 19 Acceptance tests and delivery sequence');b=s.index('# 20 Decisions and source references',a);c=s.index('## Architecture diagram',b)
tests=s[a:b];refs=s[b:c];s=s[:a]+s[c:]
refs=refs.replace('## Decisions to settle before implementation','## Decisions and remaining implementation gates')
refs+='''\n\nCurrent decisions: server and UI are Kotlin/Compose; internal ARC retains arc-protobuf-v1/wire1.0; callback materialization is interface proxy/generic Binder stub/reviewed prebuilt adapter with unsupported abstract classes retained; portable nine-tool bootstrap is default, aliases require explicit group-view negotiation; specialUse is the selected foreground-service candidate with eligibility/distribution gates. Brightness source range remains1..255. C4/Structurizr and Gradle/Markdown/SVG/Marp supersede earlier renderer/document-stack decisions. [S25] supports the documented code-view workflow.\n'''
s=s.rstrip()+'\n\n'+tests+'\n'+refs
p.write_text(s)
report=json.loads((R/'reviews/round-013/review-report.json').read_text());rows=[]
for finding in report['findings']:
 row=dict(finding);row['status']='corrected-in-current-design-awaiting-fresh-review';row['current_artifact']='design-c4/docs/Android MCP Bridge Design.md'
 if finding['id']=='F-04':row['status']='superseded-and-addressed-by-C4-Compose-model; original frozen source retained'
 if finding['id']=='F-13':row['status']='refuted-by-current-primary-reference';row['verification_evidence']={'url':'https://developer.android.com/reference/android/provider/Settings.System#SCREEN_BRIGHTNESS','exact_quote':'between 1 (minimum) and 255 (maximum)','prior_complete_snapshot':'evidence/brightness-reference-page.json','rechecked_at':datetime.datetime.now(datetime.timezone.utc).isoformat()}
 rows.append(row)
ledger=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),review='reviews/round-013/review-report.json',original_report_complete=True,original_scores=report['scores'],findings=rows,current_design_accepted=False,current_implementation_claim=False)
(R/'evidence/finding-ledger-design014.json').write_text(json.dumps(ledger,indent=2)+'\n')
status=json.loads((R/'LIVE-STATUS.json').read_text());status.update(updated_at=ledger['at'],current_phase='All28 round013 findings dispositioned against current C4 design; rebuild and deliver current Markdown/SVG package then fresh014',review_pid=None,review_phase='013complete91/88NOT_READY; currentfinalrequirementsnotyetreviewed',latest_design_review_scores=report['scores'],design_finding_ledger='evidence/finding-ledger-design014.json',current_design_review_accepted=False,review_round='013',review_completion='2026-10-27T06:00:45.689651+00:00');(R/'LIVE-STATUS.json').write_text(json.dumps(status,indent=2)+'\n')
print(json.dumps(dict(findings=len(rows),primary_refutation='F-13 brightness1..255',canonical_markdown_bytes=p.stat().st_size,previous_bytes=len(before.encode()))))
