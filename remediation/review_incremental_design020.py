"""Incremental holistic acceptance from complete018 and exact current text delta.

All current bytes remain frozen. No Google review and no hidden-thought recovery.
"""
from pathlib import Path
import json,hashlib,difflib,datetime,subprocess,sys
R=Path(__file__).resolve().parent;D=R/'reviews/round-020';BASE=R/'reviews/round-018/input';CUR=R/'reviews/round-019/input'
if D.exists():raise SystemExit('020 exists; inspect outcome before any retry')
prior=json.loads((R/'reviews/round-019/completion.json').read_text())
if prior.get('report_complete'):raise SystemExit('019 complete: inspect verdict first')
root=json.loads((R/'reviews/round-019/input-manifest.json').read_text());now=lambda:datetime.datetime.now(datetime.timezone.utc).isoformat();sha=lambda b:hashlib.sha256(b).hexdigest()
D.mkdir();S=D/'input';S.mkdir();rows=[]
# Reuse the exact frozen019 candidate, not later working edits or source production.
for row in root['files']:
    p=CUR/row['file'];raw=p.read_bytes()
    if sha(raw)!=row['sha256']:raise ValueError('019 frozen bytes changed')
    q=S/row['file'];q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(raw);q.chmod(0o444);rows.append(row)
main='design-c4/docs/Android MCP Bridge Design.md';a=(BASE/main).read_text().splitlines(True);b=(S/main).read_text().splitlines(True);ops=[];reconstructed=a[:]
for tag,i,j,k,l in difflib.SequenceMatcher(a=a,b=b,autojunk=False).get_opcodes():
    if tag=='equal':continue
    ops.append(dict(operation=tag,old_lines_1based=[i+1,j],old_text_sha256=sha(''.join(a[i:j]).encode()),old_anchor=''.join(a[i:j])[:120],new_lines_1based=[k+1,l],replacement=''.join(b[k:l])))
for op in reversed(ops):
    start,end=op['old_lines_1based'];reconstructed[start-1:end]=op['replacement'].splitlines(True)
if ''.join(reconstructed).encode()!=(S/main).read_bytes():raise ValueError('Exact delta reconstruction failed')
(D/'exact-main-delta.json').write_text(json.dumps(dict(baseline_sha256=sha((BASE/main).read_bytes()),current_sha256=sha((S/main).read_bytes()),reconstruction_verified=True,operations=ops),indent=2)+'\n')
old=json.loads((R/'reviews/round-018/recovered-synthesis/review-report.json').read_text());ledger=json.loads((R/'evidence/finding-ledger-design018-reconciled.json').read_text())
prior_compact=dict(review_scope=old['review_scope'],verdict=old['verdict'],scores=old['scores'],findings=[{k:f[k] for k in ('id','severity','area','artifact','issue')} for f in old['findings']],verified_resolutions=old['verified_resolutions'],implementation_validation_gates=old['implementation_validation_gates'],coverage=old['coverage'],limitations=old['limitations'])
compact_ledger=[dict(id=f['id'],resolution=f['working_resolution'],evidence=[dict(anchor=e['anchor'],locations=[dict(file=x['file'],line=x['line']) for x in e['locations'][:1]]) for e in f['evidence']],**({'disputed_subclaims':f['disputed_subclaims']} if 'disputed_subclaims' in f else {})) for f in ledger['findings']]
(D/'prior-review-projection.json').write_text(json.dumps(prior_compact,indent=2)+'\n');(D/'candidate-resolution-projection.json').write_text(json.dumps(compact_ledger,indent=2)+'\n')
manifest=dict(created_at=now(),scope=root['scope'],files=rows,candidate_manifest_sha256=sha((R/'reviews/round-019/input-manifest.json').read_bytes()),baseline_manifest_sha256=sha((R/'reviews/round-018/input-manifest.json').read_bytes()),complete_baseline_synthesis_sha256=sha((R/'reviews/round-018/recovered-synthesis/review-report.json').read_bytes()),review_method='Incremental holistic design acceptance: complete018 assessment + exact all current main changes + full current C4/class/UI/build/contract sources. No independent re-reading of unchanged prose or raw generated SVG markup is claimed.',main_delta_sha256=sha((D/'exact-main-delta.json').read_bytes()),main_delta_reconstruction_verified=True,prior019='Interrupted after two output-limit continuations, zero visible report; no findings or score used')
(D/'input-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
instruction=(R/'reviews/round-018/requirements.txt').read_text()
instruction+='''

CURRENT020 INCREMENTAL HOLISTIC DESIGN ACCEPTANCE. No Google API data review.
019 did not emit a visible report after two token-limit continuations and was stopped. Its hidden thoughts are NOT evidence, are NOT supplied, and contribute no score. All current candidate bytes are the unchanged exact019 immutable snapshot. This is one smaller review of the current design through the standard complete-baseline-plus-exact-delta method; it is not a claim that you freshly re-read every unchanged byte. The COMPLETE VALID018 holistic assessment (design86/85,25 findings) is supplied as a lossless projection of its issues/verified resolutions/coverage, and every edit to the main design is supplied as exact replacement text with old/new line ranges and hashes. The delta was mechanically applied and reconstructs the exact full current main SHA. Unchanged main prose retains the complete018 review evidence. Full current model, class definitions, UI manifest, build, pins and supporting hash/CRUD contract are included for cross-boundary auditing. Generated SVGs/HTML/ZIP are pinned and locally validated; full editable current sources are supplied, no duplicate raw XML/base64 bulk. Do not pretend to execute or visually inspect them. Review all changed mechanisms and adjacent contracts, reconcile all25 findings, and judge WHOLE CURRENT DESIGN independently, not an average or inherited old score. Preserve ALL12 requirements above. Missing app/data/runtime tests are implementation gates, not design deductions. An unspecified or contradictory mechanism IS a design defect.

The author's resolution projection is a proposal, not authority. Verify each. Two018 corrections deserve primary evidence: actual upstream RemoteControlProtocol.cs defines arc-protobuf-v1, and BridgeFrameCodec permits length0 at framing layer; the default envelope then fails validation. Do not enforce the reviewer's incorrect <=0 frame rule. Metadata conservative global ETag/retry tradeoffs are explicit, not inherently an HTTP violation; itemPUT creation is the actual correction. Exact source identity namespaces are not erased JVM descriptors. A planned SSLServerSocket-based TLS host is a named implementation candidate, not a claimed proven Android app. Source acquisition/extractor/catalog population are excluded. Historical round013 notes are NON-NORMATIVE; current prose must agree without blanket precedence.

Return a COMPLETE CONCISE MARKDOWN report, not giant JSON. At most2500words visible; do not repeat lengthy quoted evidence. Start with DESIGN ACCURACY: N/100, DESIGN COMPLETENESS: N/100 and ACCEPTED/NOT_ACCEPTED at98/98, with honest rationale. Include a compact25-row disposition table for F01..F25 and all material remaining/new root causes with exact path/line evidence and minimal fix. Include requirements coverage and design-vs-implementation gates. Do not invent findings from missing packet context; unchanged baseline mechanisms are covered by the supplied complete review. If a SPECIFIC unseen unchanged paragraph is truly needed, report NEEDS_CONTEXT with exact heading/line and no fabricated acceptance; we will supply only that requested text. Do not expand into dozens of packets. No tools/network/shell or hidden reasoning. End with the literal marker END_DESIGN_REVIEW_020 only after a complete assessment. This is a concise independent design review, not a request to rewrite or implement the bridge.
'''
pieces=[instruction,'\nPRIOR COMPLETE BASELINE REVIEW\n'+json.dumps(prior_compact,separators=(',',':')),'\nCANDIDATE DISPOSITIONS TO VERIFY\n'+json.dumps(compact_ledger,separators=(',',':')),'\nEXACT ALL MAIN REPLACEMENTS\n'+(D/'exact-main-delta.json').read_text()]
full=['design-c4/architecture/workspace.dsl','design-c4/architecture/code-diagrams.md','design-c4/docs/ui-asset-manifest.json','design-c4/docs/ui-screens-and-storyboards.md','design-c4/build.gradle.kts','design-c4/renderer-lock.json','design-c4/toolchain-lock.json','catalog/SOURCE-METADATA-CONTRACT.md']
for rel in full:pieces.append('\nFULL CURRENT '+rel+'\n'+(S/rel).read_text())
for name in ('RemoteControlProtocol.cs','BridgeFrameCodec.cs','Protos/remote_control.proto'):
    rel='arc-upstream/19792a52a13a3df342f90bab6b246b547e673919/src/Avalonia.RemoteControl.Protocol/'+name;pieces.append('\nPINNED PRIMARY SOURCE '+rel+'\n'+(S/rel).read_text())
pieces.append('\nBUILD COMPLETION\n'+(S/'design-c4/build/receipts/gradle-package-015.completion.json').read_text())
pieces.append('\nBROWSER NAVIGATION/NO EXTERNAL REQUESTS\n'+(S/'design-c4/build/receipts/storyboard-browser-validation-v4.json').read_text())
pieces.append('\nSCOPE / PROVENANCE\n'+json.dumps({k:v for k,v in manifest.items() if k!='files'})+'\nAll full frozen artifacts are locally retained; no extra implicit reviewer tools.')
prompt='\n'.join(pieces);(D/'prompt.txt').write_text(prompt)
if len(prompt.encode())>260000:raise SystemExit('Prepared prompt exceeded smaller recovery budget; no process started')
for p in sorted(S.rglob('*'),reverse=True):
    if p.is_dir():p.chmod(0o555)
S.chmod(0o555)
number='020-incremental-design-acceptance';source=(R/'review_design_round.py').read_text();tail=source[source.index("cli='/home/sharpninja/.local/share/mise/installs/claude/2.1.300/claude'"):]
print(json.dumps(dict(prepared_at=now(),manifest_sha256=sha((D/'input-manifest.json').read_bytes()),prompt_bytes=len(prompt.encode()),delta_reconstruction=True,tools=[])),flush=True)
exec(compile(tail,str(D/'capture.py'),'exec'))
