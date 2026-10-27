"""Lossless runtime delivery parts, each safely below16,000,000 bytes."""
import pathlib,json,hashlib,lzma,zipfile,sys
R=pathlib.Path(__file__).resolve().parent;v=sys.argv[1];O=R/'catalog/output'/('release-'+v);E=R/('release'+v+'-evidence');D=R/'deliverables'/('runtime-release'+v);D.mkdir(exist_ok=True)
manifest=json.loads((E/'release-manifest.json').read_text());record=next(f for f in manifest['files'] if f['path'].endswith('/catalog.sqlite'));archive=D/'catalog.sqlite.xz'
if archive.exists():raise ValueError('Preserve existing compressed deliverable')
h=hashlib.sha256()
with (O/'catalog.sqlite').open('rb') as source,lzma.open(archive,'wb',preset=1) as target:
 while data:=source.read(4*1024*1024):h.update(data);target.write(data)
if h.hexdigest()!=record['sha256']:raise ValueError('Runtime source changed after validation')
check=hashlib.sha256();size=0
with lzma.open(archive,'rb') as source:
 while data:=source.read(4*1024*1024):check.update(data);size+=len(data)
if check.hexdigest()!=record['sha256'] or size!=record['bytes']:raise ValueError('Lossless compression verification failed')
parts=[];compressed_hash=hashlib.sha256()
with archive.open('rb') as source:
 while data:=source.read(14_000_000):
  name='catalog.sqlite.xz.part'+str(len(parts)+1).zfill(3);(D/name).write_bytes(data);compressed_hash.update(data);parts.append(dict(name=name,bytes=len(data),sha256=hashlib.sha256(data).hexdigest()))
delivery=dict(release=v,complete=False,sqlite=record,compressed=dict(bytes=archive.stat().st_size,sha256=compressed_hash.hexdigest()),parts=parts,entries=manifest['counts']['bindings'])
(D/'runtime-parts-manifest.json').write_text(json.dumps(delivery,indent=2))
script='''import pathlib,json,hashlib,lzma,sqlite3,os
R=pathlib.Path(__file__).resolve().parent;m=json.loads((R/'runtime-parts-manifest.json').read_text());target=R/'catalog.sqlite';temp=R/'catalog.sqlite.reassembling'
if target.exists() or temp.exists():raise SystemExit('Refusing to overwrite existing database or incomplete reassembly')
h=hashlib.sha256();raw=hashlib.sha256();n=0;decoder=lzma.LZMADecompressor()
try:
 with temp.open('xb') as out:
  for part in m['parts']:
   data=(R/part['name']).read_bytes()
   if len(data)!=part['bytes'] or hashlib.sha256(data).hexdigest()!=part['sha256']:raise ValueError('Part mismatch: '+part['name'])
   h.update(data);decoded=decoder.decompress(data);out.write(decoded);raw.update(decoded);n+=len(decoded)
 if not decoder.eof or h.hexdigest()!=m['compressed']['sha256'] or n!=m['sqlite']['bytes'] or raw.hexdigest()!=m['sqlite']['sha256']:raise ValueError('Complete stream/database mismatch')
 db=sqlite3.connect(temp.as_uri()+'?mode=ro',uri=True)
 if db.execute('PRAGMA integrity_check').fetchone()[0]!='ok' or db.execute('PRAGMA foreign_key_check').fetchall() or db.execute('SELECT count(*) FROM bindings').fetchone()[0]!=m['entries']:raise ValueError('SQLite integrity/count/FK check failed')
 db.close();os.replace(temp,target);print(str(target))
except Exception:
 print('Incomplete diagnostic file retained:',temp);raise
'''
(D/'reassemble_runtime.py').write_text(script)
readme=f'''PARTIAL Android catalog release{v}. Download ALL{len(parts)} part ZIPs and extract into one NEW directory. Run python3 reassemble_runtime.py (Windows:py reassemble_runtime.py). The standard-library script verifies each part, the complete compressed stream, exact database bytes/hash, entry count, integrity and foreign keys before publishing catalog.sqlite. No network/install/device operation. It refuses existing target/incomplete files. Keep enough storage for the {record['bytes']:,}-byte database plus downloaded/extracted parts. Coverage and schema/evidence are separate; no global completeness or OS/client authority is claimed.\n'''
files=[]
for ordinal,part in enumerate(parts,1):
 p=D/('android-catalog-PARTIAL-release'+v+'-runtime-part-'+str(ordinal).zfill(3)+'-of-'+str(len(parts)).zfill(3)+'.zip')
 with zipfile.ZipFile(p,'w',zipfile.ZIP_STORED) as z:
  z.write(D/part['name'],part['name']);z.write(D/'runtime-parts-manifest.json','runtime-parts-manifest.json');z.writestr('reassemble_runtime.py',script);z.writestr('README.txt',readme)
 if p.stat().st_size>16_000_000:raise ValueError('Attachment wrapper exceeds safe cap')
 files.append(dict(path=str(p),bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
(D/'delivery-manifest.json').write_text(json.dumps(dict(lossless_verified=True,files=files),indent=2));print(json.dumps(dict(sqlite_bytes=size,compressed_bytes=archive.stat().st_size,parts=len(parts),largest_wrapper=max(f['bytes'] for f in files)),indent=2))
