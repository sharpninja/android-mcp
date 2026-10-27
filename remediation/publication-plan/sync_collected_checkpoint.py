"""Commit/push the completed normal source collection, preserving prior history."""
from pathlib import Path
import subprocess,json,hashlib,re,datetime,os
R=Path(__file__).resolve().parents[1];D=R.parent/'publication/android-mcp-git';E=R/'publication-plan';gh='/home/sharpninja/.local/share/mise/installs/gh/latest/gh_2.102.0_linux_amd64/bin/gh';helper='!'+gh+' auth git-credential'
def git(*a):
 p=subprocess.run(['git','-C',str(D),'-c','credential.helper=','-c','credential.helper='+helper,*a],capture_output=True,text=True)
 if p.returncode:raise RuntimeError('git '+a[0]+': '+p.stderr.strip())
 return p.stdout.strip()
summary=json.loads((D/'publication-manifest.json').read_text())
git('fetch','origin','main')
if git('rev-parse','HEAD')!=git('rev-parse','origin/main'):raise ValueError('Remote changed; preserve and reconcile before commit')
for p in E.glob('*'):
 if p.is_file() and p.suffix in ('.py','.md','.txt','.json') and p.stat().st_size<10*1024*1024:
  q=D/'remediation/publication-plan'/p.name;q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(p.read_bytes())
old=json.loads((D/'publication-status.json').read_text());old.update(extended_normal_collection=True,complete_current_manifest='publication-manifest.json');old['remaining']['source_project_normal_files']='Included in this completed normal checkpoint; remote verification is recorded by the publishing task';(D/'publication-status.json').write_text(json.dumps(old,indent=2)+'\n')
rx=re.compile(rb'"type"\s*:\s*"(?:thinking|redacted_thinking)"');signatures=re.compile(rb'(?:ghp_|github_pat_|sk-ant-)[A-Za-z0-9_-]{25,}|-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----');hits=[];files=0;size=0
for current,dirs,names in os.walk(D):
 dirs[:]=[x for x in dirs if x!='.git']
 for name in names:
  p=Path(current)/name;raw=p.read_bytes();files+=1;size+=len(raw)
  if len(raw)>=100*1024*1024:raise ValueError('Oversize Git file '+str(p.relative_to(D)))
  if signatures.search(raw) or rx.search(raw):hits.append(str(p.relative_to(D)))
if hits:raise ValueError('Potential private content, no values printed: '+json.dumps(hits))
(E/'collected-normal-privacy-scan.json').write_text(json.dumps(dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),files=files,bytes=size,signature_or_hidden_blocks=[],pass_=True),indent=2)+'\n')
git('add','-A','--','.');stat=git('diff','--cached','--shortstat')
message=E/'collected-commit-message.txt';message.write_text('chore: sync source contracts and complete artifact remainder ledger\n\nAdds completed normal source-project artifacts and compressed full inclusion/exclusion/remainder inventory. Large data remains pending release assets; this is not all-artifact sync or design acceptance. Active HV023 and immutable inputs remain unchanged.\n');git('commit','--quiet','-F',str(message));commit=git('rev-parse','HEAD');git('push','origin','HEAD:main')
remote=subprocess.run([gh,'api','repos/sharpninja/android-mcp/git/ref/heads/main','--jq','.object.sha'],capture_output=True,text=True,check=True).stdout.strip()
if remote!=commit:raise ValueError('Remote mismatch; inspect before retry')
receipt=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),repository='https://github.com/sharpninja/android-mcp',branch='main',commit=commit,remote_commit=remote,remote_verified=True,worktree_clean=not git('status','--porcelain'),summary=stat,normal_files_scanned=files,large_asset_bytes_pending=summary['large_asset_bytes_pending'],large_asset_files_pending=summary['large_asset_files_pending'],all_artifacts_synced=False,private=True)
with (E/'collected-normal-remote-sync.json').open('x') as f:f.write(json.dumps(receipt,indent=2)+'\n')
live=R/'LIVE-STATUS.json';v=json.loads(live.read_text());v.update(updated_at=receipt['at'],github_publication=receipt,publication_pause='Completed normal checkpoints remotely verified; large-asset sealing/upload remains, uncapped designHV023 continues');live.write_text(json.dumps(v,indent=2)+'\n');print(json.dumps(receipt))
