import pathlib,json,collections
R=pathlib.Path(__file__).resolve().parent;sources=json.loads((R/'sdk37.2/source-inventory.json').read_text())['sources'];groups=collections.defaultdict(list)
for s in sources:
 if s['kind']=='java-source':groups[s['git_blob']].append(s)
out=[dict(blob=oid,records=ss[0]['metadata_cache']['records'],paths=[s['path'] for s in ss]) for oid,ss in groups.items() if len(ss)>1]
(R/'sdk37.2/duplicate-java-content.json').write_text(json.dumps(out,indent=2));print(json.dumps(out,indent=2))
