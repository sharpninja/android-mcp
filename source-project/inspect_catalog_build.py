"""Read-only CPU/I/O observations for this task's exact catalog builder only."""
import pathlib,sys,re,json,datetime,subprocess
R=pathlib.Path(__file__).resolve().parent;release=sys.argv[1];phase=sys.argv[2] if len(sys.argv)>2 else 'build_catalog.py'
if not re.fullmatch(r'\d{3}',release):raise ValueError('Three-digit release required')
if phase not in ('build_catalog.py','validate_catalog.py','export_permissions.py'):raise ValueError('Exact task phase required')
needle=str(R/'build-snapshots'/('release'+release)/'catalog/tooling'/phase).encode();rows=[]
for file in pathlib.Path('/proc').glob('[0-9]*/cmdline'):
 try:
  if needle not in file.read_bytes().split(b'\0'):continue
  pid=file.parent.name;item=dict(pid=int(pid),ps=subprocess.run(['ps','-p',pid,'-o','pid,ppid,stat,%cpu,etime,time,rss'],capture_output=True,text=True).stdout,wchan=(file.parent/'wchan').read_text())
  try:item['io']={line.split(':')[0]:int(line.split(':')[1]) for line in (file.parent/'io').read_text().splitlines()}
  except PermissionError:item['io']='not readable'
  rows.append(item)
 except (FileNotFoundError,ProcessLookupError,PermissionError):pass
now=datetime.datetime.now(datetime.timezone.utc);result=dict(at=now.isoformat(),release=release,phase=phase,exact_task_builder_matches=rows,scope='Exact task phase command argument only; no environment, credentials, unrelated commandlines or process modification')
destination=R/('build'+release+'-process-observation-'+now.strftime('%Y%m%dT%H%M%S')+'.json');destination.write_text(json.dumps(result,indent=2));print(json.dumps(result,indent=2))
