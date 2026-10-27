from pathlib import Path
import subprocess,json,datetime
R=Path(__file__).resolve().parents[1];D=R.parent/'publication/android-mcp-git';E=R/'publication-plan';gh='/home/sharpninja/.local/share/mise/installs/gh/latest/gh_2.102.0_linux_amd64/bin/gh'
audit=json.loads((E/'checkpoint-receipt-privacy-scan.json').read_text())
if not audit['pass_']:raise ValueError('Privacy scan not clear')
local=json.loads((E/'checkpoint-local-commit.json').read_text());helper='!'+gh+' auth git-credential'
def run(args):return subprocess.run(args,capture_output=True,text=True)
head=run(['git','-C',str(D),'rev-parse','HEAD'])
if head.returncode or head.stdout.strip()!=local['commit']:raise ValueError('Checkpoint HEAD changed')
args=['git','-C',str(D),'-c','credential.helper=','-c','credential.helper='+helper,'push','origin','HEAD:main']
result=run(args);(E/'checkpoint-push.stdout.txt').write_text(result.stdout);(E/'checkpoint-push.stderr.txt').write_text(result.stderr)
if result.returncode:raise RuntimeError('Git push failed: '+result.stderr.strip())
remote=run([gh,'api','repos/sharpninja/android-mcp/git/ref/heads/main','--jq','.object.sha'])
if remote.returncode:raise RuntimeError('Push succeeded; remote verification unavailable: '+remote.stderr.strip())
if remote.stdout.strip()!=local['commit']:raise ValueError('Remote commit differs; inspect before another push')
private=run([gh,'repo','view','sharpninja/android-mcp','--json','isPrivate'])
if private.returncode:raise RuntimeError('Commit verified; visibility read unavailable')
clean=run(['git','-C',str(D),'status','--porcelain'])
receipt=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),repository='https://github.com/sharpninja/android-mcp',private=json.loads(private.stdout)['isPrivate'],branch='main',commit=local['commit'],remote_commit=remote.stdout.strip(),remote_verified=True,normal_checkpoint_synced=True,all_artifacts_synced=False,remaining='Source-owner normal refresh, finalactive023receipts and compressed/hash-verified large release assets remain pending',checkout=str(D),worktree_clean=not clean.stdout.strip(),no_force_push=True,no_global_credential_configuration_change=True)
with (E/'checkpoint-remote-sync.json').open('x') as f:f.write(json.dumps(receipt,indent=2)+'\n')
p=R/'LIVE-STATUS.json';live=json.loads(p.read_text());live.update(updated_at=receipt['at'],github_publication=receipt,publication_pause='Normal checkpoint pushed and remotely verified; resume design-only uncapped HV023 and retain large-asset sync remainder');p.write_text(json.dumps(live,indent=2)+'\n')
print(json.dumps(receipt))
