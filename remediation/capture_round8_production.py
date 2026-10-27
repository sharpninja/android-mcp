"""Read-only size/hash and nested-AIDL defect evidence; never republishes data."""
import collections,datetime,hashlib,json,pathlib,sqlite3,sys
R=pathlib.Path(__file__).resolve().parent;S=pathlib.Path('/home/sharpninja/github/2026-10-26/task/android-catalog-20261026')
sys.path.insert(0,str(R/'catalog/tooling'));from aidl_lex import inventory_counts
P=S/'catalog/output/release-006/catalog.sqlite';E=R/'evidence'
now=lambda:datetime.datetime.now(datetime.timezone.utc).isoformat()
size_receipt=E/'catalog-size-release006.json'
if not size_receipt.exists():
    before=P.stat();digest=hashlib.sha256()
    with P.open('rb') as stream:
        while chunk:=stream.read(16*1024*1024):digest.update(chunk)
    after=P.stat()
    if (before.st_size,before.st_mtime_ns)!=(after.st_size,after.st_mtime_ns):raise ValueError('Published database changed during size/hash capture')
    size_receipt.write_text(json.dumps(dict(captured_at=now(),database=str(P),size_bytes=before.st_size,sha256=digest.hexdigest(),mtime_ns=before.st_mtime_ns,stable_during_capture=True,read_only=True,reviewed_directly_by_claude=False),indent=2)+'\n')
    print('Size/hash captured',before.st_size,flush=True)
db=sqlite3.connect(P.as_uri()+'?mode=ro',uri=True)
try:
    malformed=db.execute("SELECT count(*) FROM bindings WHERE kind='aidl-member' AND (instr(return_type,'{')>0 OR instr(return_type,'parcelable ')>0)").fetchone()[0]
    samples=[dict(zip(('binding_id','signature','return_type'),row)) for row in db.execute("SELECT binding_id,signature,return_type FROM bindings WHERE kind='aidl-member' AND (instr(return_type,'{')>0 OR instr(return_type,'parcelable ')>0) LIMIT 20")]
    counts=collections.Counter();affected=[]
    for sid,raw in db.execute('SELECT source_id,raw_schema FROM aidl_schemas'):
        lexical=inventory_counts(raw);counts['files']+=1;counts['declarations']+=lexical['declarations'];counts['statements']+=lexical['statements'];counts['unreconciled_files']+=not lexical['reconciled']
        from aidl_lex import declaration_spans
        nested=lexical['declarations']-len(declaration_spans(raw))
        if nested:
            counts['nested_type_files']+=1;counts['nested_declarations']+=nested;affected.append(dict(source_id=sid,nested_declarations=nested))
finally:db.close()
report=dict(captured_at=now(),database=str(P),mode='read-only',source_files_modified=False,malformed_unique_aidl_member_return_types=malformed,examples=samples,lexical_counts=dict(counts),nested_affected_sources=affected,scope='Independent lexical inventory and SQL defect query only. Corrected production regeneration and validation remain required; no wire layouts inferred.')
target=E/'round008-release006-aidl-audit.json'
if target.exists():raise ValueError('Preserve existing audit')
target.write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(dict(completed=True,counts=dict(counts),malformed_unique_bindings=malformed)),flush=True)
