"""Re-split the existing compressed runtime archive; no reimport/recompression."""
import pathlib,json,hashlib,zipfile
R=pathlib.Path(__file__).resolve().parent;D=R/'deliverables';old=D/'components-release002';out=D/'runtime-safe-parts-release002';out.mkdir(exist_ok=True)
archive=old/'android-catalog-runtime-release002.zip';manifest=json.loads((old/'runtime-parts-manifest.json').read_text());parts=[];CHUNK=15_000_000;CAP=16_000_000
with archive.open('rb') as f:
 while b:=f.read(CHUNK):
  name='catalog-runtime.zip.part'+str(len(parts)+1).zfill(3);p=out/name;p.write_bytes(b);parts.append(dict(name=name,size_bytes=len(b),sha256=hashlib.sha256(b).hexdigest()))
manifest['parts']=parts;manifest['delivery_variant']='compressed ZIP parts, each wrapper <=16,000,000 bytes';(out/'runtime-parts-manifest.json').write_text(json.dumps(manifest,indent=2))
readme='''PARTIAL Android17/SDK37.0 runtime database, release002. This is a smaller delivery variant of the exact validated135,508-entry database; no records were dropped.\n\nDownload ALL11 runtime-part ZIPs. Extract them into ONE NEW directory. Repeated manifest/script/README files are identical. Run python3 reassemble_runtime.py (Windows: py reassemble_runtime.py). The script verifies every part and the complete archive, extracts catalog.sqlite, verifies the exact1,490,669,568-byte SQLite SHA-256, checks135,508 entries and foreign keys, and refuses to overwrite an existing catalog.sqlite.\n\nPlan about1.7GB free for the reconstructed database plus archive, in addition to downloaded/extracted parts. No network, installs, source imports or device operations are used. Schema/API/extractor and full validation evidence are separate downloads. SDK37.2/current coverage and runtime OS/client authority are not claimed.\n'''
assert len(parts)==11
files=[]
for i,part in enumerate(parts,1):
 p=out/('android-catalog-runtime-PARTIAL-release002-safe-part-'+str(i).zfill(2)+'-of-'+str(len(parts)).zfill(2)+'.zip')
 with zipfile.ZipFile(p,'w',zipfile.ZIP_STORED) as z:z.write(out/part['name'],part['name']);z.write(out/'runtime-parts-manifest.json','runtime-parts-manifest.json');z.write(D/'reassemble_runtime.py','reassemble_runtime.py');z.writestr('README-runtime.txt',readme)
 assert p.stat().st_size<=CAP
 files.append(dict(path=str(p.resolve()),size_bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
(out/'delivery-manifest.json').write_text(json.dumps(dict(all_records_preserved=True,source_archive_sha256=manifest['archive_sha256'],files=files),indent=2));print(json.dumps(dict(parts=len(files),max_bytes=max(p['size_bytes'] for p in files),last_bytes=files[-1]['size_bytes']),indent=2))
