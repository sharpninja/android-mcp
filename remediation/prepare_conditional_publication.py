"""Local inventory and scan PLAN only; never creates a repository or uploads."""
from pathlib import Path
import os,json,hashlib,datetime,re,collections
R=Path(__file__).resolve().parent;OUT=R/'publication-plan';OUT.mkdir(exist_ok=True)
output=OUT/'inventory-001.json'
if output.exists():raise SystemExit('Existing immutable publication inventory; use new numbered receipt')
source=Path('/home/sharpninja/github/2026-10-26/task/android-catalog-20261026')
excluded={'.git','.venv','venv','node_modules','.tools','.gradle-home','.gradle','__pycache__','.aws','.codex','.agents','.claude'}
rows=[];totals=collections.defaultdict(lambda:dict(files=0,bytes=0));skipped=[]
for scope,root in [('design-remediation',R),('source-owner-project',source)]:
    for parent,dirs,files in os.walk(root,followlinks=False):
        base=Path(parent)
        for name in list(dirs):
            q=base/name
            if name in excluded or q.is_symlink() or q==OUT:
                dirs.remove(name);skipped.append(dict(scope=scope,path=str(q.relative_to(root)),reason='dependency/auth/internal cache, symlink, or this plan output; not a distributable project artifact'))
        for name in files:
            p=base/name
            if p.is_symlink():continue
            stat=p.stat();rel=str(p.relative_to(root))
            category='git-candidate'
            if stat.st_size>=100*1024*1024:category='release-asset-required'
            elif stat.st_size>=50*1024*1024:category='prefer-release-asset'
            if any(x in p.name.lower() for x in ('credential','secret','token')) or p.name.startswith('.env'):
                category='quarantine-review-filename'
            if p.suffix in ('.sqlite','.db','.wal','.shm') or p.name.endswith(('-wal','-shm')):
                category='sealed-database-only-release-asset' if not p.name.endswith(('-wal','-shm')) else 'volatile-not-release-artifact'
            if p.suffix=='.jsonl' and ('response' in name or 'request' in name) and 'visible' not in name and 'request' not in name:
                category='receipt-sanitization-required'
            if scope=='source-owner-project':category='source-owner-confirmation:'+category
            row=dict(scope=scope,path=rel,bytes=stat.st_size,mtime_ns=stat.st_mtime_ns,disposition=category,hash_status='pending exact sealed publication freeze')
            rows.append(row);totals[scope]['files']+=1;totals[scope]['bytes']+=stat.st_size
scan=[]
patterns={'github_token':re.compile(r'\b(?:ghp_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{50,})\b'),'anthropic_token':re.compile(r'\bsk-ant-[A-Za-z0-9_-]{30,}\b'),'private_key':re.compile(r'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----')}
text_suffixes={'.py','.json','.jsonl','.md','.txt','.sql','.kts','.dsl','.svg','.cjs','.cs','.proto','.properties','.sh','.ps1'}
for row in rows:
    if row['scope']!='design-remediation' or row['bytes']>20_000_000:continue
    p=R/row['path']
    if p.suffix not in text_suffixes:continue
    try:text=p.read_text()
    except UnicodeDecodeError:continue
    for category,pattern in patterns.items():
        for match in pattern.finditer(text):
            scan.append(dict(file=row['path'],category=category,line=text.count('\n',0,match.start())+1,matched_value_omitted=True,disposition='quarantine until fixture/real-secret distinction reviewed; never upload raw finding value'))
result=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),condition='Only after independent design accuracy>=98 AND completeness>=98',condition_met=False,target='sharpninja/android-mcp',visibility='private default pending parent/user answer',repository_created=False,committed=False,pushed=False,source_active_modified=False,totals=dict(totals),excluded_directories=skipped,files=rows,local_token_signature_scan=scan,scan_limits='Signature scan of own task text<=20MB only, not a claim of exhaustive secret or personal-data absence. Source owner must supply approved sealed inventory and license/provenance. Receipts need structured hidden-block/personal-data scan before publication; never read auth stores.',large_asset_strategy='After acceptance, sealed DB/export/raw-source archives split into <2GiB release assets, whole-file and per-part SHA256 + deterministic reconstruction manifest in Git. Prefer1GiB chunks, no Git LFS or paid commitment. No active staging/WAL is uploaded. Coordinate all historical/current artifacts with source owner; no silent omission.',github_capability={'git':'2.55.0 available','gh':'mise shim attempted auto-install on --version; failed read-only filesystem, no software installed; do not repeat installation','connector_repo_search':'422 resource absent OR inaccessible; existence/visibility uncertain, no creation attempted','next_step':'After acceptance resolve existing authenticated repository-create/upload capability and exact repo state without credential reads or overwriting existing work'},primary_limits={'regular_git':'GitHub blocks files>100MiB, warns>50MiB','release':'up to1000assets/release; each<2GiB; official docs state no total release size/bandwidth limit','sources':['https://docs.github.com/en/repositories/working-with-files/managing-large-files/about-large-files-on-github','https://docs.github.com/en/repositories/releasing-projects-on-github/about-releases']},license_gate='ARC pinned package metadata says MIT, exact copyright/notices not yet verified. No copied/translated upstream code/schema distribution until applicable notice determined. Record public-source licenses per owner release. Do not fabricate license text.')
output.write_text(json.dumps(result,indent=2)+'\n')
(OUT/'README.md').write_text('''# Conditional GitHub publication preparation

Nothing has been created, committed or pushed. Publication is conditional on independent design>=98/98, and visibility defaults private unless the user chooses otherwise. inventory-001.json records stat-only artifact coverage and exclusions; it is not a sealed release manifest. Source owner confirmation, exact hashes, licensed source notices and scoped secret/personal-data/hidden-reasoning review precede publication. The plan does not authorize deployment, secret CI, paid LFS or an installation.

Git retains documentation/tooling/tests and full sanitized visible review receipts. Large sealed data is preserved through versioned private GitHub release parts with whole-file and per-part SHA-256 and reconstruction tooling; no silent omissions. Keep active staging/WAL and dependency/auth caches out. Exact ownership and current/historical source outputs must be reconciled with the source task through the parent.

The GitHub connector's repository search returned422 (absent or inaccessible), so target existence/visibility remains uncertain. The installed gh path is a mise shim; a version check attempted an unavailable auto-install and failed on a read-only filesystem. No software was installed. Find a supported existing authenticated create/upload route after acceptance; do not repeat that installation attempt or expose credentials.
''')
print(json.dumps(dict(inventory=str(output),totals=dict(totals),signature_hits=len(scan),large_files=sum(r['bytes']>=100*1024*1024 for r in rows),condition_met=False,repository_created=False)))
