"""Small read-only schema throughput probe; does not touch the active process."""
import pathlib,json,time,statistics
import jsonschema
R=pathlib.Path(__file__).resolve().parent;schema=json.loads((R/'build-snapshots/release005/catalog/contracts/Entry.schema.json').read_text());v=jsonschema.Draft202012Validator(schema)
rows=[]
with (R/'catalog/output/release-002/entries.jsonl').open() as f:
 for _ in range(10):rows.append(json.loads(next(f)))
samples=[]
for row in rows:
 start=time.perf_counter()
 for _ in range(30):list(v.iter_errors(row))
 samples.append(dict(bytes=len(json.dumps(row)),seconds_per_entry=(time.perf_counter()-start)/30))
report=dict(samples=samples,median_seconds=statistics.median(s['seconds_per_entry'] for s in samples),purpose='Read-only reference throughput only; not a measurement of current build progress or its larger records')
(R/'release005-schema-throughput-probe.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
