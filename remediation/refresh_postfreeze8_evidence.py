"""Prepare new evidence; never change existing frozen review files."""
import datetime,hashlib,json,pathlib,shutil
R=pathlib.Path(__file__).resolve().parent;E=R/'evidence'
S=pathlib.Path('/home/sharpninja/github/2026-10-26/task/android-catalog-20261026')
now=lambda:datetime.datetime.now(datetime.timezone.utc).isoformat()
records=[]
for source,name in [('cache-build-round007/cache-byte-recomputation-proof.json','source007-cache-recomputation.json'),('cache-build-round007/preflight.json','source007-cache-preflight.json')]:
    p=S/source;out=E/name
    if out.exists():raise SystemExit('Refuse to replace captured evidence: '+str(out))
    shutil.copyfile(p,out);records.append(dict(source=str(p),copy=str(out.relative_to(R)),sha256=hashlib.sha256(out.read_bytes()).hexdigest()))
(E/'source007-cache-evidence-capture.json').write_text(json.dumps(dict(captured_at=now(),source_files_modified=False,records=records,scope='Producer cache proof only; not a completed007production import or independent validation'),indent=2)+'\n')
p=E/'requirements-traceability.json';doc=json.loads(p.read_text());doc['updated_at']=now();doc['production_baseline']='Completed006 independently validates794195entries,39194sources,1399definition records; full selected corpus, global completefalse.'
for row in doc['requirements']:
    if row['requirement'].startswith('ALL available'):
        row['remaining_blockers']=['006predates post007candidate and cache-contract2 fixes; corrected release required','Outside985 audit is in progress, not wholly unenumerated; relevant extra paths not fully imported, unknown/native/generated sources unresolved','Missing runtime/wire schemas remain explicit unknowns; no universal private/OEM introspection claim']
    if row['requirement'].startswith('Comprehensive Google'):
        row['remaining_blockers']=['Global public binding/source coverage remains materially incomplete','006producer/DDL differs from corrected candidate; new matching production evidence required']
        row['historical_validation']='Legacy002copy passes structural validation but has552variant omissions; current candidate read-only005crosscheck passes772733entries with0variant omissions. Historical copy limitations are not counted as omissions in newer releases.'
p.write_text(json.dumps(doc,indent=2)+'\n')
print(json.dumps(dict(captured=records,traceability_updated=True,frozen008_unchanged=True)))
