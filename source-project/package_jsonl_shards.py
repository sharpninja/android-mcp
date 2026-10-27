"""Independent lossless JSONL shards, each raw shard <=12,000,000 bytes.
Upload only shard001 until the consumer confirms its attachment is accepted.
"""
import pathlib,json,hashlib,zipfile
R=pathlib.Path(__file__).resolve().parent;O=R/'catalog/output/release-002';D=R/'deliverables/jsonl-shards-release002';D.mkdir(exist_ok=True)
TARGET=12_000_000;LIMIT=16_000_000
def digest(p):
 h=hashlib.sha256()
 with p.open('rb') as f:
  while b:=f.read(1024*1024):h.update(b)
 return h.hexdigest()
parts=[];f=None;current=None;total=0
with (O/'entries.jsonl').open('rb') as source:
 for line in source:
  if len(line)>TARGET:raise ValueError('One entry exceeds shard bound; retain it using a separate lossless representation')
  if current is None or current['size_bytes']+len(line)>TARGET:
   if f:f.close()
   path=D/('entries-shard-'+str(len(parts)+1).zfill(3)+'.jsonl');f=path.open('wb');current=dict(name=path.name,size_bytes=0,records=0,first_entry_id=None,last_entry_id=None);parts.append(current)
  entry=json.loads(line);f.write(line);current['size_bytes']+=len(line);current['records']+=1;current['first_entry_id']=current['first_entry_id'] or entry['entry_id'];current['last_entry_id']=entry['entry_id'];total+=1
if f:f.close()
assert total==135508
for part in parts:part['sha256']=digest(D/part['name'])
manifest=dict(release='PARTIAL release002',schema_version='1.1.0',entries=total,shards=len(parts),source_jsonl_size_bytes=(O/'entries.jsonl').stat().st_size,source_jsonl_sha256=digest(O/'entries.jsonl'),source_json_size_bytes=(O/'catalog.json').stat().st_size,source_json_sha256=digest(O/'catalog.json'),all_records_preserved=True,complete_android_coverage=False,parts=parts)
(D/'entries-shards-manifest.json').write_text(json.dumps(manifest,indent=2))
readme='''Independent JSONL entry shard from the135,508-entry PARTIAL Android17/SDK37.0 catalog, release002.\n\nEach JSONL line is one complete entry document. This file is independently readable. To reconstruct the complete entry export, obtain every shard in entries-shards-manifest.json, extract into one new directory, verify the listed SHA-256 values, and concatenate the JSONL files in manifest order. Their concatenation is byte-for-byte identical to the validated full entries.jsonl; no records were removed or sampled. The separate schema/evidence Library ZIP contains provenance inventory, JSON Schemas, API contract and executed validation evidence.\n\nCurrent SDK37.2 coverage is not claimed. Permission metadata does not grant OS or MCP-client authority. This frozen release has only10 acquired manifest permission-definition records; later core-manifest recovery is not incorporated here.\n'''
outputs=[]
for i,part in enumerate(parts,1):
 p=D/('android-catalog-JSONL-PARTIAL-release002-shard-'+str(i).zfill(3)+'-of-'+str(len(parts)).zfill(3)+'.zip')
 with zipfile.ZipFile(p,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:z.write(D/part['name'],part['name']);z.write(D/'entries-shards-manifest.json','entries-shards-manifest.json');z.writestr('README-shard.txt',readme)
 assert p.stat().st_size<=LIMIT
 outputs.append(dict(path=str(p.resolve()),size_bytes=p.stat().st_size,sha256=digest(p),records=part['records']))
(D/'delivery-manifest.json').write_text(json.dumps(dict(first_only_before_attachment_confirmation=True,outputs=outputs),indent=2));print(json.dumps(dict(shards=len(parts),entries=total,first=outputs[0]),indent=2),flush=True)
