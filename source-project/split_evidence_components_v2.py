"""Losslessly separate local evidence from bulky reproducibility caches.
No uploads and no runtime record changes. Original full ZIP remains immutable.
"""
import pathlib,json,sys,zipfile,hashlib,datetime,time,re
R=pathlib.Path(__file__).resolve().parent;v=sys.argv[1]
if not re.fullmatch(r'\d{3}',v):raise ValueError('Three-digit release required')
D=R/'deliverables';source=D/('android-catalog-PARTIAL-release'+v+'-schema-permissions-evidence.zip');receipt=D/('release'+v+'-evidence-components.json')
if receipt.exists():raise SystemExit('Preserve existing components receipt')
if '--wait' in sys.argv:
 deadline=time.monotonic()+6*3600
 while True:
  try:follow=json.loads((R/('local-followups-release'+v+'.json')).read_text())
  except (OSError,json.JSONDecodeError):follow={}
  if source.exists() and (follow.get('status','').startswith('blocked') or follow.get('status','').startswith('complete')):break
  if follow.get('status','').startswith('blocked'):raise SystemExit('Non-packaging follow-up failed; inspect before proceeding')
  if time.monotonic()>deadline:raise SystemExit('Waiting timeout; existing build untouched')
  time.sleep(5)
if not (R/('release'+v+'-evidence/release-manifest.json')).exists():raise ValueError('Validated release manifest required')
if not source.exists():raise ValueError('No closed source bundle')
if source.stat().st_size<=16_000_000:raise SystemExit('Original bundle already below cap; no split needed')
with zipfile.ZipFile(source) as original:
 if original.testzip():raise ValueError('Original ZIP CRC check failed')
 names=original.namelist();groups={'contracts-permissions-evidence':[],'reproduction-caches':[]}
 for name in names:
  group='reproduction-caches' if '/sdk37.2/' in name and name.endswith(('/api-index-current.jsonl.gz','/api-index-all-declared.jsonl.gz','/java-extraction-report.json')) else 'contracts-permissions-evidence'
  groups[group].append(name)
 text=f'''Release {v}: validated PARTIAL catalog evidence delivery set.
No database or records were changed to form these logical ZIP components.
Inspect the contracts/permissions/evidence component independently. To reproduce,
extract BOTH components into the same NEW directory, preserving all paths, then
follow README.md. The complete original bundle-files.json describes the union.
The reproduction-caches component contains the exact SDK API-index cache and
the complete Java extraction report. No source inventory records are dropped.
Original full ZIP remains saved locally. Nothing was uploaded by this script.
'''
 outputs=[];seen=set()
 for group,members in groups.items():
  if not members:raise ValueError('Empty logical group')
  out=D/('android-catalog-PARTIAL-release'+v+'-'+group+'.zip')
  with zipfile.ZipFile(out,'x',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
   for name in members:z.writestr(name,original.read(name));seen.add(name)
   z.writestr('DELIVERY-SET-README.txt',text)
  if out.stat().st_size>16_000_000:raise ValueError('Component remains oversized; preserved for further logical subdivision')
  with zipfile.ZipFile(out) as z:
   if z.testzip():raise ValueError('Component CRC check failed')
   for name in members:
    if hashlib.sha256(z.read(name)).digest()!=hashlib.sha256(original.read(name)).digest():raise ValueError('Component differs from original '+name)
  outputs.append(dict(path=str(out.relative_to(R)),bytes=out.stat().st_size,sha256=hashlib.sha256(out.read_bytes()).hexdigest(),original_members=len(members)))
 if seen!=set(names) or sum(len(x) for x in groups.values())!=len(names):raise ValueError('Original ZIP member loss or duplication')
result=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),status='complete local delivery variants; no upload',source=dict(path=str(source.relative_to(R)),bytes=source.stat().st_size,sha256=hashlib.sha256(source.read_bytes()).hexdigest()),components=outputs,all_original_members_preserved_byte_exact=True,uploaded=False)
receipt.write_text(json.dumps(result,indent=2));print(json.dumps(result,indent=2))
