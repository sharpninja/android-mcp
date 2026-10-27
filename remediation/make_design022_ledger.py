from pathlib import Path
import json,re,hashlib,datetime
R=Path(__file__).resolve().parent;P=R/'design-c4';report=R/'reviews/round-022/supervisor/unified-review-report.txt'
groups=[
('B1 P3-01 F-04','Post-render SVG embedding, actual-image regressions and dynamic manifest slide counts',['build.gradle.kts','docs/storyboards.html','docs/Android MCP Bridge Design.md']),
('B2 P3-02 P3-15 SYN-3 P1-05 P2-10','Named closed-domain HealthSnapshot fixtures with Error chip, desired_state, catalog and ARC listener',['docs/ui-asset-manifest.json','ui-svg/SCR10.svg','architecture/code-diagrams.md']),
('B3 P1-01 P1-03','ArcTargetBackend, shared canonical entry-point/binding policy and reserved local controller principal',['architecture/workspace.dsl','architecture/code-diagrams.md','docs/Android MCP Bridge Design.md']),
('B4 P3-03','Inbound ARC listener enable/bind/root/credential rotation/session surface separate from outbound controller',['ui-svg/SCR08.svg','docs/ui-asset-manifest.json']),
('B5 P3-04 P3-06 P3-08 P3-09','Explicit affordances, external event actors, proof/final-commit panels, viewport/tree/pointer controls and separate branches',['docs/ui-asset-manifest.json','storyboards/storyboard.md','ui-svg/STB04.svg','ui-svg/SCR07.svg']),
('N-07 P1-02 N-01 P1-04 N-02','Live domains, separate identity methods, Reserved state and phase-specific uncertainty transition exits',['docs/Android MCP Bridge Design.md']),
('SYN-8 P2-04 P1-12','Pinned upstream singleton input state; principal/root lease across unary sockets; current-root coordinate limits',['architecture/code-diagrams.md','docs/Android MCP Bridge Design.md']),
('P2-05 P2-06 P2-07 P1-16 P2-08 P2-09','SDK adapter interface, session-view commit, service input quiescer, proxy factory, explicit direct-stream note and precise describe page contract',['architecture/code-diagrams.md','docs/Android MCP Bridge Design.md']),
('P2-03 F-11 P2-01 P2-02 P1-08 P1-09','Class ownership and scope mapping, visible component edges, host/pair/admin/outbox class view',['architecture/code-diagrams.md','architecture/workspace.dsl']),
('P3-07 P3-10 P3-11 P3-14 P3-05','Phone drawer/detail navigation, explicit protected review and import/export/discard controls',['docs/ui-asset-manifest.json','docs/ui-screens-and-storyboards.md']),
('P1-06 P1-07 P1-11 P1-13 P1-15 P1-17 F-03 F-06 F-14 F-18 N-04 SYN-1 SYN-2 SYN-4 SYN-5 SYN-6 SYN-7','Removed contradictory labels/codes/domains and added explicit remaining lifecycle, trust, mapping and route contracts',['docs/Android MCP Bridge Design.md','docs/ui-asset-manifest.json','architecture/code-diagrams.md']),
('P1-10','Bootstrap compares published Gradle digest with committed lock and does not rewrite committed wrapper properties',['tooling/bootstrap.py']),
('P1-14 P1-18 F-17 P2-11 P2-12 P2-13 P2-14 P2-15 P2-16 P2-17 P2-18 P2-19 P2-20 P2-21 P2-22 P2-23 P2-24','Call ordering, auth, invalidation, deployment, socket/lease semantics, shutdown/revoke and editing caveats reconciled',['architecture/workspace.dsl','architecture/code-diagrams.md','README.md']),
('P3-12 P3-13 P3-16 P3-17 P3-18 P3-19 P3-20','Notification/Ask queue, retention causes, exact labels, role-specific scopes and grant vocabulary',['docs/Android MCP Bridge Design.md','docs/ui-asset-manifest.json']),
('P3-21','Rendering uncertainty addressed by actual browser captures and geometry checks, not inferred from text-only tests',['build/receipts/storyboard-browser-validation-v7.json'])]
items=[]
for ids,description,files in groups:
    items.append(dict(ids=ids.split(),status='candidate_corrected_pending_independent_review',change=description,evidence=[dict(file='design-c4/'+f,sha256=hashlib.sha256((P/f).read_bytes()).hexdigest()) for f in files]))
covered={x for i in items for x in i['ids']};allids=set(re.findall(r'\b(?:P[123]-\d\d|SYN-\d|B[1-5]|N-\d\d|F-\d\d)\b',report.read_text()))
source=R/'catalog/SOURCE-METADATA-CONTRACT.md';items.append(dict(ids=['P1-19'],status='candidate_corrected_pending_independent_review',change='Status428 cites RFC6585 section3 separately from RFC9110 semantics',evidence=[dict(file='catalog/SOURCE-METADATA-CONTRACT.md',sha256=hashlib.sha256(source.read_bytes()).hexdigest())]));covered.add('P1-19')
remaining=sorted(allids-covered)
out=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),baseline=dict(round='022',report_sha256=hashlib.sha256(report.read_bytes()).hexdigest(),accuracy=92,completeness=89,accepted=False),items=items,other_prior_ids=remaining,other_prior_id_status='Explicitly previously resolved or acknowledged gates in the complete022 baseline; retained for reviewer regression assessment, no new self-score',acceptance_claim=False,scope='Whole design only; no Google API surface/population scoring',validation='19 normal+19 optimized design contract checks; full pinned Gradle; complete SVG image decoding and every-slide captures; fresh independent acceptance still required')
(R/'evidence/design022-closure-ledger.json').write_text(json.dumps(out,indent=2)+'\n');print(json.dumps({'groups':len(items),'ids_covered':len(covered),'retained_previous_ids':remaining,'acceptance_claim':False}))
