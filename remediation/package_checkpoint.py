"""Package scoped candidate and visible review receipts, never databases or secrets."""
import pathlib,json,zipfile,hashlib,datetime,sys
R=pathlib.Path(__file__).resolve().parent;round_number=int(sys.argv[1]);D=R/'reviews'/f'round-{round_number:03d}'
if not (D/'completion.json').exists():raise SystemExit('Review still running: no immutable checkpoint yet')
stamp=datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%SZ');out=R/'deliverables'/f'Android-MCP-Remediation-Round{round_number:03d}-{stamp}.zip';out.parent.mkdir(exist_ok=True)
files=[]
for folder in ('catalog','design','integration'):
 for p in (R/folder).rglob('*'):
  if p.is_file() and p.suffix in ('.py','.json','.sql','.md','.txt','.svg','.mmd','.png','.patch','.yml','.yaml') and '__pycache__' not in p.parts and 'output' not in p.parts:files.append(p)
for p in (R/'evidence').iterdir():
 if p.is_file() and p.suffix in ('.json','.txt','.log','.sql'):files.append(p)
for p in (R/'reviews').rglob('*'):
 if not p.is_file() or '__pycache__' in p.parts:continue
 if int(p.relative_to(R/'reviews').parts[0].split('-')[-1])>round_number:continue
 if p.suffix not in ('.json','.jsonl','.txt','.md','.py','.sql','.mmd','.svg'):continue
 if p.name=='response.visible.jsonl':
  for line in p.read_text().splitlines():
   event=json.loads(line)
   assert not any(b.get('type') in ('thinking','redacted_thinking') for b in event.get('message',{}).get('content',[]))
 files.append(p)
for p in (R/'reviews').glob('round-*/input-manifest.json'):
 if int(p.parent.name.split('-')[-1])>round_number:continue
 for item in json.loads(p.read_text())['files']:
  target=p.parent/'input'/item['file'];assert hashlib.sha256(target.read_bytes()).hexdigest()==item['sha256'],str(target)
files+=list(R.glob('*.py'))+[R/'LIVE-STATUS.json'];files=sorted(set(files))
manifest=[dict(file=str(p.relative_to(R)),size_bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in files]
readme='''Android MCP bridge remediation checkpoint. Design/catalog/schema/extractor/API specification only; no Android/HTTP application implementation, deployment, credentials, device operations, commits or installs.

Review request.jsonl is the complete scoped visible request and exact CLI invocation. response.visible.jsonl preserves visible provider events and responses while excluding hidden reasoning blocks. Reassembled reports retain originals and extraction receipts. Each round input manifest pins immutable bytes.

See LIVE-STATUS.json, latest finding ledger/progress, frozen regression evidence, source integration handoffs and populated validation. Passing storage tests does not establish semantic rebuild, all-current-binding coverage or device authority. The production source task is separate; completed releases are identified by their exact code/input/validation receipts, and global coverage remains incomplete. No giant databases or unrelated files are included. Current working candidate may include changes after the latest completed review; those changes are not independently accepted until a fresh review says so. Each review's input manifest, rather than the mutable working candidate, identifies its assessed bytes. Receipts include only completed rounds up to the round number in the filename; a newer running review is intentionally excluded, not presented as complete.
'''
with zipfile.ZipFile(out,'x',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
 z.writestr('README.txt',readme);z.writestr('bundle-manifest.json',json.dumps(dict(created_at=stamp,files=manifest),indent=2))
 for p in files:z.write(p,str(p.relative_to(R)))
assert out.stat().st_size<16000000,'Split into scoped bundles before Library delivery'
receipt=dict(path=str(out),size_bytes=out.stat().st_size,sha256=hashlib.sha256(out.read_bytes()).hexdigest(),files=len(files),all_round_inputs_verified=True,hidden_reasoning_excluded=True)
(out.with_suffix('.manifest.json')).write_text(json.dumps(receipt,indent=2));print(json.dumps(receipt))
