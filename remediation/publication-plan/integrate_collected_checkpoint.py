"""Add completed source-owner normal artifacts and a compressed full remainder ledger."""
from pathlib import Path
import json,hashlib,gzip,shutil,datetime,subprocess,re
R=Path(__file__).resolve().parents[1];BASE=R.parent;STAGE=BASE/'publication/android-mcp';D=BASE/'publication/android-mcp-git';SOURCE=Path('/home/sharpninja/github/2026-10-26/task/android-catalog-20261026')
raw_manifest=STAGE/'publication-manifest.json';manifest=json.loads(raw_manifest.read_text());added=[]
if not (R/'publication-plan/checkpoint-remote-sync.json').exists():raise ValueError('Initial checkpoint not remotely verified')
status=subprocess.run(['git','-C',str(D),'status','--porcelain'],capture_output=True,text=True,check=True).stdout
if status.strip():raise ValueError('Preserve existing worktree edits before integration')
for row in manifest['files']:
 if row['storage']!='git' or not row['path'].startswith('source-project/'):continue
 p=STAGE/row['path'];raw=p.read_bytes()
 if hashlib.sha256(raw).hexdigest()!=row['published_sha256']:raise ValueError('Staged source changed '+row['path'])
 q=D/row['path'];q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(raw)
 if p.stat().st_mode&0o111:q.chmod(0o755)
 added.append(dict(path=row['path'],size_bytes=len(raw),sha256=row['published_sha256']))
corrections=[]
for row in manifest['exclusions']:
 rel=Path(row['path'])
 if 'credential' not in rel.name.lower() or rel.suffix not in ('.svg','.py'):continue
 base=R if rel.parts[0]=='remediation' else SOURCE;p=base/Path(*rel.parts[1:])
 if not p.is_file():continue
 raw=p.read_bytes()
 if re.search(rb'(?:ghp_|github_pat_|sk-ant-)[A-Za-z0-9_-]{25,}|-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----',raw):raise ValueError('Potential secret in code/diagram '+str(rel))
 q=D/rel;q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(raw);corrections.append(dict(path=str(rel),sha256=hashlib.sha256(raw).hexdigest(),reason='Credential-named diagram/test code is a project artifact, not a credential store; correct overbroad collection name filter'))
inventory=D/'all-project-file-inventory.json.gz'
with raw_manifest.open('rb') as src,inventory.open('wb') as out:
 with gzip.GzipFile(fileobj=out,mode='wb',filename='',mtime=0,compresslevel=6) as gz:shutil.copyfileobj(src,gz,1024*1024)
summary=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),status='NOT_ACCEPTED; source normal checkpoint added',complete_inclusion_remainder_inventory={'file':inventory.name,'compressed_bytes':inventory.stat().st_size,'sha256':hashlib.sha256(inventory.read_bytes()).hexdigest(),'encoding':'gzip-compressed complete JSON inventory; Python gzip.open+json.load'},collected_totals=manifest['totals'],source_normal_files=added,post_collection_corrections=corrections,all_artifacts_synced=False,large_asset_bytes_pending=manifest['totals']['deferred_bytes'],large_asset_files_pending=manifest['totals']['deferred_files'],privacy_rules='No credentials, private notes, auth/dependency caches or hidden reasoning. Receipt metadata redactions retain full visible project content and immutable original hashes in complete inventory.',source_originals_modified=False)
(D/'publication-manifest.json').write_text(json.dumps(summary,indent=2)+'\n')
p=D/'README.md';s=p.read_text();s=s.replace('Source-owner normal files and the refreshed all-file inclusion/remainder manifest are being collected for the next commit. The earlier complete inventory is retained in initial-full-inventory.json.gz.','Source-owner normal files are now included. The refreshed complete all-file inclusion/remainder inventory is in [publication-manifest.json](publication-manifest.json), with its compressed detailed ledger. The earlier stat inventory is retained in initial-full-inventory.json.gz.');p.write_text(s)
print(json.dumps(dict(source_normal_files=len(added),source_normal_bytes=sum(x['size_bytes'] for x in added),full_inventory_bytes=inventory.stat().st_size,corrections=len(corrections),large_asset_bytes_pending=summary['large_asset_bytes_pending'],all_artifacts_synced=False)))
