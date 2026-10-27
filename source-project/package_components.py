"""Lossless attachment variants, each <=25 MiB. No source records dropped."""
import pathlib,zipfile,json,hashlib,concurrent.futures,datetime
R=pathlib.Path(__file__).resolve().parent;O=R/'catalog/output/release-002';D=R/'deliverables';V=D/'components-release002';V.mkdir(exist_ok=True)
LIMIT=25*1024*1024;CHUNK=24*1024*1024
def digest(p):
 h=hashlib.sha256()
 with p.open('rb') as f:
  while b:=f.read(1024*1024):h.update(b)
 return h.hexdigest()
def make(name,files):
 p=V/name
 with zipfile.ZipFile(p,'w',zipfile.ZIP_DEFLATED,compresslevel=6,allowZip64=True) as z:
  for src,target in files:z.write(src,target)
 return p
jobs=[('android-catalog-runtime-release002.zip',[(O/'catalog.sqlite','catalog.sqlite'),(O/'validation-report.json','validation-report.json')]),('android-catalog-entries-JSONL-PARTIAL-release002.zip',[(O/'entries.jsonl','entries.jsonl')]),('android-catalog-entries-JSON-PARTIAL-release002.zip',[(O/'catalog.json','catalog.json')])]
with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:paths=list(pool.map(lambda args:make(*args),jobs))
runtime=paths[0];parts=[]
with runtime.open('rb') as f:
 while b:=f.read(CHUNK):
  name='catalog-runtime.zip.part'+str(len(parts)+1).zfill(3);p=V/name;p.write_bytes(b);parts.append(dict(name=name,size_bytes=len(b),sha256=hashlib.sha256(b).hexdigest()))
manifest=dict(schema_version='1.0.0',release='PARTIAL release002',created_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),archive_name=runtime.name,archive_size_bytes=runtime.stat().st_size,archive_sha256=digest(runtime),sqlite_size_bytes=(O/'catalog.sqlite').stat().st_size,sqlite_sha256=digest(O/'catalog.sqlite'),binding_entries=135508,parts=parts,complete_android_coverage=False)
(V/'runtime-parts-manifest.json').write_text(json.dumps(manifest,indent=2))
instructions='''PARTIAL Android17 / SDK37.0 catalog, release002. Current SDK37.2 is not covered.\n\nDownload ALL runtime-part ZIPs. Extract their contents into ONE NEW directory. The repeated manifest/script/README files are identical. Run:\n  python3 reassemble_runtime.py\n(or on Windows: py reassemble_runtime.py)\n\nThe script checks every part, reconstructs the compressed runtime ZIP, extracts catalog.sqlite, verifies its exact original SHA-256, and checks the135,508-entry count and foreign keys. It refuses to overwrite an existing catalog.sqlite. No network, installs or device access are used. About1.7GB free space is needed for the reconstructed database plus compressed archive, in addition to the downloaded parts.\n\nNo database records were removed. The JSON and JSONL downloads are optional alternative entry representations. Schema/API/extractor/coverage/test evidence is a separate small ZIP. The CRUD API is a specification, not an implemented service. No OS authority or MCP client grant is inferred. This frozen release still has limited manifest permission coverage; source recovery continues separately.\n'''
downloads=[]
for i,part in enumerate(parts,1):
 p=V/('android-catalog-runtime-PARTIAL-release002-part-'+str(i).zfill(2)+'-of-'+str(len(parts)).zfill(2)+'.zip')
 with zipfile.ZipFile(p,'w',compression=zipfile.ZIP_STORED) as z:
  z.write(V/part['name'],part['name']);z.write(V/'runtime-parts-manifest.json','runtime-parts-manifest.json');z.write(D/'reassemble_runtime.py','reassemble_runtime.py');z.writestr('README-runtime.txt',instructions)
 downloads.append(dict(path=str(p),contents='Lossless compressed SQLite runtime part '+str(i)+' of '+str(len(parts))))
for p in paths[1:]:downloads.append(dict(path=str(p),contents='Complete135,508-entry '+('JSONL' if 'JSONL' in p.name else 'JSON')+' export for PARTIAL release002'))
docs=V/'android-catalog-schema-API-extractor-evidence-PARTIAL-release002.zip'
with zipfile.ZipFile(D/'android17-sdk37.0-catalog-PARTIAL-milestone-20261026.zip') as source,zipfile.ZipFile(docs,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
 for info in source.infolist():
  if info.filename.endswith(('/catalog.sqlite','/entries.jsonl','/catalog.json','SHA256-MANIFEST.json')):continue
  z.writestr(info.filename,source.read(info.filename))
 z.writestr('DELIVERY-PARTS.txt',instructions);z.write(V/'runtime-parts-manifest.json','runtime-parts-manifest.json');z.write(D/'reassemble_runtime.py','reassemble_runtime.py')
downloads.append(dict(path=str(docs),contents='DDL/migrations, JSON Schemas, OpenAPI3.1 CRUD spec, extractor/tests, coverage/provenance, permission summaries and validation reports'))
for d in downloads:
 p=pathlib.Path(d['path']);d.update(size_bytes=p.stat().st_size,sha256=digest(p));assert p.stat().st_size<=LIMIT,(p,p.stat().st_size)
(V/'delivery-manifest.json').write_text(json.dumps(dict(original_library_item='libfile_0ce4a0f8948881919d83a7af949e6c3b',original_item_kept=True,original_user_attachment_delivery='failed: exceeds ChatGPT upload limit',all_records_preserved=True,maximum_attachment_bytes=LIMIT,downloads=downloads),indent=2));print(json.dumps(downloads,indent=2),flush=True)
