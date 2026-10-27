"""Acquire commit/tree metadata only, in NEW task-local bare repositories."""
import subprocess,pathlib,json,time
R=pathlib.Path(__file__).resolve().parent
def tree(repo,rev):
 d=R/'tree-cache'/repo.replace('/','__');d.mkdir(parents=True,exist_ok=True)
 if not (d/'HEAD').exists():subprocess.run(['git','init','--bare',str(d)],check=True,capture_output=True)
 p=subprocess.run(['git','-C',str(d),'fetch','--filter=blob:none','--depth=1','https://android.googlesource.com/'+repo,rev],text=True,capture_output=True,timeout=180)
 if p.returncode:raise RuntimeError(p.stderr)
 if 'filtering not recognized' in p.stderr:raise RuntimeError('Server does not support metadata-only filter; stop')
 commit=subprocess.run(['git','-C',str(d),'rev-parse','FETCH_HEAD^{commit}'],capture_output=True,text=True,check=True).stdout.strip()
 p=subprocess.run(['git','-C',str(d),'ls-tree','-r','-z',commit],capture_output=True,check=True)
 entries=[]
 for raw in p.stdout.split(b'\0'):
  if not raw:continue
  meta,path=raw.decode().split('\t',1);mode,kind,oid=meta.split();entries.append(dict(path=path,kind=kind,oid=oid,mode=mode))
 out=R/'trees';out.mkdir(exist_ok=True);(out/(repo.replace('/','__')+'.json')).write_text(json.dumps(dict(repo=repo,requested_revision=rev,revision=commit,entries=entries),indent=2))
 print(repo,commit,len(entries),'entries',sum(x['path'].endswith('.aidl') for x in entries),'AIDL',flush=True)
 return entries
if __name__=='__main__':tree('platform/frameworks/base','94b4c163b7dfe5ce3607f7bb8456f9573f7de57d')
