"""Seal completed normal remediation files independently of ongoing data inventory."""
from pathlib import Path
import json,hashlib,datetime,shutil,os,re,subprocess
R=Path(__file__).resolve().parents[1];BASE=R.parent;STAGE=BASE/'publication/android-mcp';DEST=BASE/'publication/android-mcp-git'
if DEST.exists():raise SystemExit('Existing Git checkpoint path; inspect before retry')
DEST.mkdir(parents=True);rows=[]
patterns=[re.compile(rb'(?:ghp_|github_pat_)[A-Za-z0-9_]{25,}'),re.compile(rb'sk-ant-[A-Za-z0-9_-]{25,}'),re.compile(rb'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----')]
for p in sorted((STAGE/'remediation').rglob('*')):
 if not p.is_file():continue
 rel=p.relative_to(STAGE);raw=p.read_bytes()
 # Frozen normative JSON is project input, not runtime/plugin metadata. Restore
 # exact original bytes when the broad receipt classifier reformatted it.
 original=R/p.relative_to(STAGE/'remediation')
 if 'input' in rel.parts and p.suffix=='.json' and p.name not in ('completion.json','process.json','request.json') and original.exists():raw=original.read_bytes()
 if any(x.search(raw) for x in patterns):raise ValueError('Potential secret in '+str(rel)+'; no value exported')
 q=DEST/rel;q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(raw)
 if p.stat().st_mode&0o111:q.chmod(0o755)
 rows.append(dict(path=str(rel),size_bytes=len(raw),sha256=hashlib.sha256(raw).hexdigest()))
# Credential-named SVGs and code are project design/test artifacts, not credential
# stores. The broad staging name filter must not omit them.
for current,dirs,names in os.walk(R):
 dirs[:]=[x for x in dirs if x not in {'.tools','.gradle-home','.gradle','.venv','__pycache__','node_modules','.git'}]
 for name in names:
  if 'credential' not in name.lower() or Path(name).suffix not in ('.svg','.py'):continue
  p=Path(current)/name;rel=Path('remediation')/p.relative_to(R);q=DEST/rel
  if q.exists():continue
  raw=p.read_bytes()
  if any(x.search(raw) for x in patterns):raise ValueError('Potential secret in '+str(rel))
  q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(raw);rows.append(dict(path=str(rel),size_bytes=len(raw),sha256=hashlib.sha256(raw).hexdigest()))
seed=R/'publication-plan/inventory-001.json.gz';shutil.copyfile(seed,DEST/'initial-full-inventory.json.gz')
status=dict(created_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),status='NOT_ACCEPTED',latest_complete_hv={'round':'022','accuracy':92,'completeness':89},active_hv='023; frozen inputs/processes untouched',normal_checkpoint_files=rows,normal_bytes=sum(x['size_bytes'] for x in rows),complete_source_inventory_seed={'file':'initial-full-inventory.json.gz','sha256':hashlib.sha256(seed.read_bytes()).hexdigest(),'scope':'Earlier complete stat inventory of both project roots; current refresh still collecting, not a sealed payload manifest'},remaining={'source_project_normal_files':'Current refresh in publication/android-mcp; next commit after seal','large_artifacts':'All databases, acquired datasets, historical archives and any privacy-reviewed remainder require compressed/chunked hash-verified release assets','active_review_receipts':'Captured normal-prefix checkpoint only; final023 receipts will be synchronized after completion'},all_artifacts_synced=False,originals_modified=False,visibility='private',deployment=False)
(DEST/'publication-status.json').write_text(json.dumps(status,indent=2)+'\n')
(DEST/'README.md').write_text('''# Android MCP bridge — checkpoint, acceptance pending

Kotlin server and Compose UI design, internal Kotlin ARC compatibility-port plan, C4/Structurizr SVG models, Gradle documentation build, SVG screen definitions and Marp storyboards, catalog contracts and offline reference tooling. No Android runtime implementation or deployment is claimed.

Latest complete independent HV: **round02292/89, NOT_ACCEPTED**. Corrected round023 is active. The user requested this checkpoint regardless of acceptance; the design98/98 loop continues.

- [Canonical Markdown design](<remediation/design-c4/docs/Android MCP Bridge Design.md>)
- [Reproducible Gradle docs project](remediation/design-c4/README.md)
- [SVG screens and storyboards](remediation/design-c4/docs/ui-screens-and-storyboards.md)
- [Catalog/reference contracts](remediation/catalog/README.md)
- [Visible unified022 review](remediation/reviews/round-022/supervisor/unified-review-report.txt)
- [Exact checkpoint inventory and sync remainder](publication-status.json)

This first commit contains the sealed normal remediation checkpoint. Source-owner normal files and the refreshed all-file inclusion/remainder manifest are being collected for the next commit. The earlier complete inventory is retained in initial-full-inventory.json.gz. Large artifacts will use compressed release chunks with whole/part hashes and reconstruction tooling; they are not yet synchronized. No paid LFS/storage commitment is made.

Visible review requests/responses are preserved in audited publication copies. Hidden reasoning, account-quota/plugin/operator metadata and credential/private-note files are excluded; local originals remain unchanged. Active review streams are captured prefixes, not completed verdicts. No root Actions workflow or deployment is enabled.
''')
(DEST/'.gitignore').write_text('**/__pycache__/\n**/.tools/\n**/.gradle-home/\n**/.gradle/\n**/node_modules/\n**/.venv/\n*.part.unverified\n')
print(json.dumps(dict(checkout=str(DEST),files=len(rows),bytes=status['normal_bytes'],source_inventory_seed_bytes=seed.stat().st_size,all_artifacts_synced=False)))
