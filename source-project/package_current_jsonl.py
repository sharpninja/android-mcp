"""Lossless independently readable Entry JSONL shards; no records sampled."""
import pathlib,json,hashlib,zipfile,sys
R=pathlib.Path(__file__).resolve().parent;v=sys.argv[1];O=R/'catalog/output'/('release-'+v);E=R/('release'+v+'-evidence');D=R/'deliverables'/('jsonl-release'+v)
manifest=json.loads((E/'release-manifest.json').read_text());record=next(f for f in manifest['files'] if f['path'].endswith('/entries.jsonl'));D.mkdir()
parts=[];target=None;current=None;total=0;whole=hashlib.sha256()
with (O/'entries.jsonl').open('rb') as source:
    for line in source:
        if len(line)>96_000_000:raise ValueError('Single entry exceeds shard allocation; preserve diagnostic output')
        if current is None or current['bytes']+len(line)>96_000_000:
            if target:target.close()
            path=D/('entries-part'+str(len(parts)+1).zfill(3)+'.jsonl');target=path.open('xb');current=dict(name=path.name,bytes=0,records=0,first_entry_id=None,last_entry_id=None);parts.append(current)
        doc=json.loads(line);target.write(line);whole.update(line);current['bytes']+=len(line);current['records']+=1;current['first_entry_id']=current['first_entry_id'] or doc['entry_id'];current['last_entry_id']=doc['entry_id'];total+=1
if target:target.close()
if whole.hexdigest()!=record['sha256'] or total!=manifest['counts']['bindings']:raise ValueError('Source export/count changed')
for part in parts:part['sha256']=hashlib.sha256((D/part['name']).read_bytes()).hexdigest()
delivery=dict(release=v,complete=False,entries=total,source=record,parts=parts,concatenation_byte_equivalent=True)
(D/'jsonl-parts-manifest.json').write_text(json.dumps(delivery,indent=2));outputs=[]
readme=f'''PARTIAL Android catalog release{v}: independent JSONL shard. Each line is one complete Entry document, readable by standard JSON tools. Obtain every shard in jsonl-parts-manifest.json for all{total:,} entries. Verify listed SHA256 values and concatenate JSONL members in manifest order to reproduce entries.jsonl exactly. Coverage/schema/permission metadata/evidence are separate. No OS authority, MCP client grants, all-current completeness or98/98 verdict is inferred.\n'''
for i,part in enumerate(parts,1):
    path=D/('android-catalog-PARTIAL-release'+v+'-JSONL-'+str(i).zfill(3)+'-of-'+str(len(parts)).zfill(3)+'.zip')
    with zipfile.ZipFile(path,'x',zipfile.ZIP_DEFLATED,compresslevel=6) as z:z.write(D/part['name'],part['name']);z.write(D/'jsonl-parts-manifest.json','jsonl-parts-manifest.json');z.writestr('README.txt',readme)
    if path.stat().st_size>16_000_000:raise ValueError('Preserve oversize variant; use smaller lossless shards')
    outputs.append(dict(path=str(path),bytes=path.stat().st_size,sha256=hashlib.sha256(path.read_bytes()).hexdigest(),records=part['records']))
(D/'delivery-manifest.json').write_text(json.dumps(dict(files=outputs,all_entries_preserved=True),indent=2));print(json.dumps(dict(parts=len(parts),entries=total,first=outputs[0]),indent=2))
