"""Read-only source/import and legacy declaration-shape acceptance evidence."""
import collections,datetime,hashlib,itertools,json,pathlib,sqlite3,sys,xml.etree.ElementTree as ET

def source_key(s):return tuple(s.get(k) for k in ('repository','revision','path','sha256'))
def shape_key(d):return json.dumps({k:d.get(k) for k in ('kind','arguments','return_type')},sort_keys=True,separators=(',',':'))
def legacy_variant_candidates(rows):
    shapes=collections.defaultdict(set)
    for bid,doc in rows:shapes[bid].add(shape_key(doc))
    return {bid for bid,s in shapes.items() if len(s)>1}
def manifest_elements(raw):
    return collections.Counter(e.get('{http://schemas.android.com/apk/res/android}name') for e in ET.fromstring(raw).iter('permission'))
def readonly(path):return sqlite3.connect(path.resolve().as_uri()+'?mode=ro',uri=True)

def verify_json_streams(entries,catalog):
    prefix=b'{"schema_version":"1.2.0","complete":false,"entries":['
    if catalog.read(len(prefix))!=prefix:raise ValueError('Catalog JSON prefix mismatch')
    count=0
    for line in entries:
        expected=(b',' if count else b'')+line.rstrip(b'\n')
        if catalog.read(len(expected))!=expected:raise ValueError('Catalog JSON differs from Entry JSONL at '+str(count))
        count+=1
    if catalog.read()!=b']}':raise ValueError('Catalog JSON has missing or extra records/trailing bytes')
    return count

def verify_table_rows(rows,lines):
    count=0;sentinel=object()
    for row,line in itertools.zip_longest(rows,lines,fillvalue=sentinel):
        if row is sentinel or line is sentinel or dict(row)!=json.loads(line):raise ValueError('Table JSONL differs from SQLite at '+str(count))
        count+=1
    return count

def export_evidence(root,version):
    root=pathlib.Path(root).resolve();out=root/'catalog/output'/('release-'+version);report=root/('export-reconciliation-release'+version+'.json')
    if report.exists():raise ValueError('Preserve prior export evidence')
    db=readonly(out/'catalog.sqlite');db.row_factory=sqlite3.Row;tables={}
    try:
        for table in ('sources','source_details','bindings','occurrences','binding_declarations','permission_definitions','permission_symbols','permission_relations','aidl_schemas','annotation_evidence','issues','parse_reports'):
            with (out/(table+'.jsonl')).open() as lines:tables[table]=verify_table_rows(db.execute('SELECT * FROM '+table),lines)
            print('reconciled export',table,tables[table],flush=True)
        with (out/'entries.jsonl').open('rb') as entries,(out/'catalog.json').open('rb') as catalog:count=verify_json_streams(entries,catalog)
        expected=db.execute('SELECT count(*) FROM effective_entries').fetchone()[0]
        if count!=expected:raise ValueError('Entry export count mismatch')
    finally:db.close()
    result=dict(created_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),release=version,database_open_mode='read-only',table_rows=tables,entries=count,json_and_jsonl_byte_equivalent=True,complete=False)
    report.write_text(json.dumps(result,indent=2));print(json.dumps(result,indent=2));return result

def run(root,version):
    root=pathlib.Path(root).resolve();report=root/('production-reconciliation-release'+version+'.json')
    if report.exists():raise ValueError('Preserve prior acceptance evidence')
    inventory=root/('source-inventory-release'+version+'.json');raw=inventory.read_bytes();inv=json.loads(raw)
    expected=collections.Counter(source_key(s) for s in inv['sources'] if s['status']=='downloaded')
    db=readonly(root/'catalog/output'/('release-'+version)/'catalog.sqlite')
    actual=collections.Counter();kinds=collections.Counter()
    for (document,) in db.execute('SELECT document_json FROM source_details'):
        source=json.loads(document);actual[source_key(source)]+=1;kinds[source['kind']]+=1
    missing=list((expected-actual).elements());extra=list((actual-expected).elements())
    if missing or extra:raise ValueError('Imported source identity mismatch')
    core=db.execute('SELECT source_id,sha256 FROM sources WHERE path=?',('core/res/AndroidManifest.xml',)).fetchall()
    if len(core)!=1:raise ValueError('Core manifest identity ambiguous')
    sid,sha=core[0];manifest_source=next(s for s in inv['sources'] if s['path']=='core/res/AndroidManifest.xml')
    core_raw=(root/manifest_source['local_path']).read_bytes()
    if hashlib.sha256(core_raw).hexdigest()!=sha:raise ValueError('Core manifest hash mismatch')
    names=manifest_elements(core_raw);imported={name for name, in db.execute('SELECT name FROM permission_definitions WHERE source_id=?',(sid,))}
    if imported!=set(names):raise ValueError('Core permission names mismatch')
    duplicates=list(db.execute("SELECT raw FROM issues WHERE source_id=? AND category='duplicate' AND reason='Duplicate permission name in one manifest; raw element retained'",(sid,)))
    retained=collections.Counter()
    for (element,) in duplicates:retained.update(manifest_elements(('<manifest>'+element+'</manifest>').encode()))
    if retained!=collections.Counter({n:c-1 for n,c in names.items() if c>1}):raise ValueError('Duplicate permission evidence mismatch')
    annotation=collections.Counter()
    for bid,document in db.execute('SELECT binding_id,document_json FROM annotation_evidence'):
        d=json.loads(document);annotation[(('matched' if bid else 'unmatched')+':'+str(d.get('target_match_kind'))+':'+str(d.get('target_resolution_reason')))]+=1
    legacy=readonly(root/'catalog/output/release-002/catalog.sqlite')
    candidates=legacy_variant_candidates((bid,json.loads(doc)) for bid,doc in legacy.execute('SELECT binding_id,document_json FROM binding_declarations'))
    legacy_rows={bid:signature for bid,signature in legacy.execute('SELECT binding_id,signature FROM bindings') if bid in candidates}
    dispositions=[]
    for bid,signature in sorted(legacy_rows.items()):
        row=db.execute('SELECT document_json FROM effective_entries WHERE entry_id=?',(bid,)).fetchone()
        if not row:
            dispositions.append(dict(legacy_entry_id=bid,canonical_signature=signature,status='missing-current-identity'));continue
        doc=json.loads(row[0]);variants=doc.get('extensions',{}).get('declaration_variants',[])
        dispositions.append(dict(legacy_entry_id=bid,status='shape-variants-retained' if len(variants)>1 else 'variant-not-demonstrated',variant_count=len(variants)))
    parse=collections.defaultdict(lambda:collections.Counter())
    for (document,) in db.execute('SELECT document_json FROM parse_reports'):
        d=json.loads(document);c=parse[d['kind']];c['files']+=1;c['declarations']+=d['declarations'];c['unsupported']+=d['unsupported']
    result=dict(created_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),release=version,database_open_mode='read-only',complete=False,
        inventory_sha256=hashlib.sha256(raw).hexdigest(),source_reconciliation=dict(expected=sum(expected.values()),imported=sum(actual.values()),missing=missing,extra=extra,by_kind=dict(kinds)),
        core_manifest=dict(sha256=sha,elements=sum(names.values()),unique_names=len(names),imported_unique_names=len(imported),duplicate_elements_retained=sum(retained.values()),duplicates={n:c for n,c in names.items() if c>1}),
        annotation_target_resolution=dict(annotation),parse_reports={k:dict(v) for k,v in parse.items()},
        legacy_release002=dict(shape_variant_candidates=len(candidates),dispositions=dict(collections.Counter(d['status'] for d in dispositions)),entries=dispositions),
        limitations=['This sidecar supplements exhaustive validation; it does not claim all-current coverage','Legacy comparison checks retained shape alternatives, not API applicability or current public membership','AIDL enrichment and expanded outside-corpus sources belong to the next immutable candidate'])
    db.close();legacy.close();report.write_text(json.dumps(result,indent=2));print(json.dumps({k:v for k,v in result.items() if k not in ('legacy_release002','parse_reports')},indent=2));print('legacy dispositions',result['legacy_release002']['dispositions'],flush=True)
    return result

if __name__=='__main__':
    (export_evidence if '--exports' in sys.argv else run)(pathlib.Path(__file__).resolve().parent,sys.argv[1])
