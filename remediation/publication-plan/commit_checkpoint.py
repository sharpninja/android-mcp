"""Commit the sealed normal checkpoint to the explicitly requested new repository."""
from pathlib import Path
import subprocess,json,datetime
R=Path(__file__).resolve().parents[1];D=R.parent/'publication/android-mcp-git';E=R/'publication-plan'
def git(*args):
 p=subprocess.run(['git','-C',str(D),*args],capture_output=True,text=True)
 if p.returncode:raise RuntimeError('git '+args[0]+' failed: '+p.stderr.strip())
 return p.stdout.strip()
if not (D/'publication-status.json').exists():raise ValueError('Checkpoint not sealed')
if git('branch','--show-current')!='main':raise ValueError('Unexpected branch')
if git('ls-files','--unmerged'):raise ValueError('Unmerged paths')
for n in ('MERGE_HEAD','REBASE_HEAD','CHERRY_PICK_HEAD','REVERT_HEAD','rebase-merge','rebase-apply'):
 if (D/'.git'/n).exists():raise ValueError('Unsafe Git state '+n)
remote=subprocess.run(['git','-C',str(D),'remote','get-url','origin'],capture_output=True,text=True)
url='https://github.com/sharpninja/android-mcp.git'
if remote.returncode:git('remote','add','origin',url)
elif remote.stdout.strip()!=url:raise ValueError('Unexpected origin; do not overwrite')
before=git('status','--short','--untracked-files=all');(E/'checkpoint-git-status-before.txt').write_text(before+'\n')
git('add','-A','--','.')
names=git('diff','--cached','--name-status');(E/'checkpoint-git-staged-names.txt').write_text(names+'\n');stat=git('diff','--cached','--shortstat')
if any(line.split('\t')[-1].startswith('.github/workflows/') for line in names.splitlines()):raise ValueError('No root CI workflows authorized')
message=E/'checkpoint-commit-message.txt';message.write_text('chore: checkpoint Android MCP design pending HV acceptance\n\nLatest complete independent design HV022:92accuracy/89completeness, NOT_ACCEPTED. Corrected HV023 remains active and its frozen inputs are preserved.\n\nIncludes sealed normal design, contracts, tooling, tests and privacy-filtered visible receipts. Source-project refresh and hash-verified compressed large release assets remain explicitly pending in publication-status.json. No deployment or paid storage.\n')
git('commit','--quiet','-F',str(message))
result=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),checkout=str(D),branch='main',origin=url,commit=git('rev-parse','HEAD'),staged_summary=stat,worktree_clean=not git('status','--porcelain'),pushed=False,all_artifacts_synced=False)
with (E/'checkpoint-local-commit.json').open('x') as f:f.write(json.dumps(result,indent=2)+'\n')
print(json.dumps(result))
