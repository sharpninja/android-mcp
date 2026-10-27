"""Read-only validation of every entry, provenance FK, source reconciliation and AST."""
import pathlib, json, sqlite3, hashlib, collections, sys, datetime, contextlib
import jsonschema
P = pathlib.Path(__file__).resolve().parents[1]
LOADED_CODE_SHA256=hashlib.sha256(pathlib.Path(__file__).read_bytes()).hexdigest()

def run(output, inventory, report_path):
    with contextlib.ExitStack() as resources:
        return _run(output,inventory,report_path,resources)

def _run(output, inventory, report_path, resources):
    out = pathlib.Path(output).resolve()
    inv = pathlib.Path(inventory).resolve()
    sources = json.loads(inv.read_text())['sources']
    db = sqlite3.connect('file:' + str(out / 'catalog.sqlite') + '?mode=ro', uri=True)
    resources.callback(db.close)
    validator = jsonschema.Draft202012Validator(json.loads((P / 'contracts/Entry.schema.json').read_text()))
    ast_validator = jsonschema.Draft202012Validator(json.loads((P / 'contracts/PermissionExpression.schema.json').read_text()))
    for f in (P / 'contracts').glob('*.schema.json'):
        jsonschema.Draft202012Validator.check_schema(json.loads(f.read_text()))
    generation = db.execute('SELECT registry_generation FROM catalog_view WHERE singleton=1').fetchone()[0]
    from catalog_projection import validate_inventory
    device_inventory=validate_inventory(db,out)
    failures = []
    kinds = collections.Counter()
    ops = collections.Counter()
    variants = 0
    n = 0

    def walk(ast):
        ops[ast['op']] += 1
        for v in ast.values():
            if isinstance(v, dict) and 'op' in v:
                walk(v)
            elif isinstance(v, list):
                for x in v:
                    if isinstance(x, dict) and 'op' in x:
                        walk(x)
    for bid, document, digest, etag in db.execute('SELECT entry_id,document_json,record_sha256,etag FROM effective_entries'):
        doc = json.loads(document)
        errors = list(validator.iter_errors(doc))
        if errors:
            failures.extend((dict(entry_id=bid, path=list(e.path), message=e.message) for e in errors))
        if not hashlib.sha256(doc['canonical_signature'].encode()).hexdigest() == bid:
            raise ValueError("Validation failed: hashlib.sha256(doc['canonical_signature'].encode()).hexdigest() == bid")
        if not hashlib.sha256(document.encode()).hexdigest() == digest:
            raise ValueError('Validation failed: hashlib.sha256(document.encode()).hexdigest() == digest')
        if not etag == '"cat1:' + str(generation) + ':' + digest + ':' + bid + ':0:active"':
            raise ValueError('Validation failed: etag == \'"cat1:\' + str(generation) + \':\' + digest + \':\' + bid + \':0:active"\'')
        kinds[doc['kind']] += 1
        walk(doc['permission'])
        variants += 'permission_variants' in doc['extensions']
        n += 1
        for param in doc['extensions'].get('parameters', []):
            if 'permission' in param:
                ast_validator.validate(param['permission'])
        if n % 20000 == 0:
            print('validated entries', n, flush=True)
    if not n == db.execute('SELECT count(*) FROM bindings').fetchone()[0]:
        raise ValueError("Validation failed: n == db.execute('SELECT count(*) FROM bindings').fetchone()[0]")
    exports = 0
    for line in resources.enter_context((out / 'entries.jsonl').open()):
        doc = json.loads(line)
        exports += 1
        row = db.execute('SELECT record_sha256 FROM effective_entries WHERE entry_id=?', (doc['entry_id'],)).fetchone()
        if not (row and hashlib.sha256(line.rstrip('\n').encode()).hexdigest() == row[0]):
            raise ValueError("Validation failed: row and hashlib.sha256(line.rstrip('\\n').encode()).hexdigest() == row[0]")
    if not exports == n:
        raise ValueError('Validation failed: exports == n')
    downloaded = [s for s in sources if s['status'] == 'downloaded']
    if not len(downloaded) == db.execute('SELECT count(*) FROM sources').fetchone()[0]:
        raise ValueError("Validation failed: len(downloaded) == db.execute('SELECT count(*) FROM sources').fetchone()[0]")
    for s in downloaded:
        if not hashlib.sha256((inv.parent / s['local_path']).read_bytes()).hexdigest() == s['sha256']:
            raise ValueError("Validation failed: hashlib.sha256((inv.parent / s['local_path']).read_bytes()).hexdigest() == s['sha256']")
    reports = [json.loads(r[0]) for r in db.execute('SELECT document_json FROM parse_reports')]
    for r in reports:
        if 'expected_declarations' in r:
            if not r['expected_declarations'] == r['declarations']:
                raise ValueError("Validation failed: r['expected_declarations'] == r['declarations']")
    from aidl_lex import inventory_counts
    report_by_id={r['source_id']:r for r in reports}
    actual_by_id=dict(db.execute('SELECT source_id,count(*) FROM binding_declarations GROUP BY source_id'))
    aidl_reconciliation=collections.Counter()
    for sid,raw_schema in db.execute('SELECT source_id,raw_schema FROM aidl_schemas'):
        report=report_by_id[sid];lexical=inventory_counts(raw_schema)
        if report.get('aidl_reconciliation')=='matched':
            if not lexical['reconciled'] or lexical['expected_records']!=actual_by_id.get(sid,0):raise ValueError('AIDL reconciliation failed in validator '+sid)
            aidl_reconciliation['matched']+=1
        else:
            aidl_reconciliation['unreconciled']+=1
    signature_stats = {}
    for k in ('sdk-signature', 'source-signature'):
        rs = [r for r in reports if r['kind'] == k]
        signature_stats[k] = dict(files=len(rs), declarations=sum((r['declarations'] for r in rs)), unsupported=sum((r['unsupported'] for r in rs)))
    integrity = db.execute('PRAGMA integrity_check').fetchone()[0]
    fk = db.execute('PRAGMA foreign_key_check').fetchall()
    if not (integrity == 'ok' and (not fk)):
        raise ValueError("Validation failed: integrity == 'ok' and (not fk)")
    duplicates = db.execute('SELECT count(*) FROM (SELECT binding_id,source_id,line,count(*) n FROM occurrences GROUP BY binding_id,source_id,line HAVING n>1)').fetchone()[0]
    if not duplicates == 0:
        raise ValueError('Validation failed: duplicates == 0')
    unmatched = db.execute('SELECT count(*) FROM annotation_evidence WHERE binding_id IS NULL').fetchone()[0]
    scoped_annotation_ops = collections.Counter()
    for document, scope in db.execute('SELECT document_json,scope FROM annotation_evidence'):
        a = json.loads(document)['annotation']
        if 'permission' in a:
            ast_validator.validate(a['permission'])
            scoped_annotation_ops[scope + ':' + a['permission']['op']] += 1
    known_defs = {x[0] for x in db.execute('SELECT DISTINCT name FROM permission_definitions')}
    known_symbols = {x[0] for x in db.execute('SELECT DISTINCT value FROM permission_symbols')}
    leaves = collections.Counter()
    for row in db.execute('SELECT expression_json FROM permission_relations'):

        def refs(a):
            if a['op'] == 'permission':
                leaves[a['name']] += 1
            for v in a.values():
                if isinstance(v, dict) and 'op' in v:
                    refs(v)
                elif isinstance(v, list):
                    for x in v:
                        if isinstance(x, dict) and 'op' in x:
                            refs(x)
        refs(json.loads(row[0]))
    result = dict(validated_at=datetime.datetime.now(datetime.timezone.utc).isoformat(), schema_version='1.2.0', entries_validated=n, json_schema_failures=failures, integrity_check=integrity, foreign_key_check=fk, duplicate_occurrences=duplicates, counts={t: db.execute('SELECT count(*) FROM ' + t).fetchone()[0] for t in ('sources', 'bindings', 'occurrences', 'binding_declarations', 'permission_definitions', 'permission_symbols', 'permission_relations', 'aidl_schemas', 'annotation_evidence', 'issues')}, entry_kinds=dict(kinds), effective_permission_nodes=dict(ops), scoped_annotation_permission_nodes=dict(scoped_annotation_ops), source_dependent_permission_entries=variants, signature_reconciliation=signature_stats, external_annotations_unmatched=unmatched, permission_reference_names=dict(total=len(leaves), with_definition=len(set(leaves) & known_defs), constant_only=len((set(leaves) & known_symbols) - known_defs), without_definition_or_constant=len(set(leaves) - known_defs - known_symbols)), undefined_permission_names=sorted(set(leaves) - known_defs - known_symbols), complete=False)
    result['aidl_reconciliation']=dict(aidl_reconciliation)
    result['device_inventory']=device_inventory
    db.close()
    report_path=pathlib.Path(report_path).resolve()
    if report_path==out or out in report_path.parents:raise ValueError('Validation report must be outside immutable release directory')
    with report_path.open('x') as report:report.write(json.dumps(result,indent=2))
    print(json.dumps(result, indent=2), flush=True)
    if failures:
        raise ValueError('JSON Schema validation failed')
    return result
def run_database(database,report_path):
    """Validate a populated v3 database read-only, without exports or acquisition.

    Structural validity is separate from semantic rebuild/source coverage. Legacy
    declaration variants and applicability facts are reported rather than invented.
    """
    path=pathlib.Path(database).resolve();report_path=pathlib.Path(report_path).resolve()
    if path==report_path:raise ValueError('Report cannot replace database')
    db=sqlite3.connect(path.as_uri()+'?mode=ro',uri=True)
    try:
        if db.execute('PRAGMA user_version').fetchone()[0]!=3:raise ValueError('Expected storage v3; migrate legacy storage to a new copy first')
        schema_bytes=(P/'contracts/Entry.schema.json').read_bytes();ast_bytes=(P/'contracts/PermissionExpression.schema.json').read_bytes()
        validator=jsonschema.Draft202012Validator(json.loads(schema_bytes))
        ast_validator=jsonschema.Draft202012Validator(json.loads(ast_bytes))
        generation=db.execute('SELECT registry_generation FROM catalog_view').fetchone()[0]
        failures=[];active_entries=0;has_variants={}
        for bid,document,digest,etag in db.execute('SELECT entry_id,document_json,record_sha256,etag FROM effective_entries'):
            doc=json.loads(document);failures.extend(dict(entry_id=bid,path=list(e.path),message=e.message) for e in validator.iter_errors(doc))
            if hashlib.sha256(doc['canonical_signature'].encode()).hexdigest()!=bid:raise ValueError('Canonical entry identity mismatch '+bid)
            if hashlib.sha256(document.encode()).hexdigest()!=digest:raise ValueError('Entry document hash mismatch '+bid)
            if etag!='"cat1:'+str(generation)+':'+digest+':'+bid+':0:active"':raise ValueError('Current entry ETag mismatch '+bid)
            for parameter in doc.get('extensions',{}).get('parameters',[]):
                if 'permission' in parameter:ast_validator.validate(parameter['permission'])
            has_variants[bid]=bool(doc.get('extensions',{}).get('declaration_variants'));active_entries+=1
        for (raw,) in db.execute('SELECT expression_json FROM permission_relations'):ast_validator.validate(json.loads(raw))
        omissions=0;samples=[];previous=None;shapes=set()
        def record_omission(bid,shapes):
            nonlocal omissions
            if bid is not None and len(shapes)>1 and bid in has_variants and not has_variants[bid]:
                omissions+=1
                if len(samples)<20:samples.append(bid)
        for bid,raw in db.execute('SELECT binding_id,document_json FROM binding_declarations ORDER BY binding_id'):
            if previous!=bid:record_omission(previous,shapes);shapes=set();previous=bid
            declaration=json.loads(raw);shape={k:declaration.get(k) for k in ('kind','arguments','return_type')};shapes.add(json.dumps(shape,sort_keys=True,separators=(',',':')))
        record_omission(previous,shapes)
        integrity=db.execute('PRAGMA integrity_check').fetchone()[0];fk=db.execute('PRAGMA foreign_key_check').fetchall()
        rebuilt=db.execute("SELECT value FROM catalog_meta WHERE key='rebuilt_with_corrected_extractor'").fetchone()
        rebuilt=rebuilt[0] if rebuilt else 'unknown'
        result=dict(database=str(path),database_open_mode='read-only',validated_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),entries_validated=active_entries,json_schema_failures=failures,integrity_check=integrity,foreign_key_check=fk,registry_generation=generation,rebuilt_with_corrected_extractor=rebuilt,source_applicability_facts_revalidated=False,declaration_variant_omissions=omissions,declaration_variant_omission_sample_ids=samples,complete=False,limitations=['No source acquisition or semantic rebuild performed','Legacy API/channel claims are preserved, not independently re-established'],counts={t:db.execute('SELECT count(*) FROM '+t).fetchone()[0] for t in ('sources','bindings','occurrences','binding_declarations','permission_definitions','registry_entries')})
        result['validation_inputs']=dict(loaded_validator_sha256=LOADED_CODE_SHA256,entry_schema_sha256=hashlib.sha256(schema_bytes).hexdigest(),permission_schema_sha256=hashlib.sha256(ast_bytes).hexdigest())
    finally:db.close()
    with report_path.open('x') as report:json.dump(result,report,indent=2)
    if failures or integrity!='ok' or fk:raise ValueError('Database validation failed; see report')
    if rebuilt=='true' and omissions:raise ValueError('Corrected rebuild omits declaration variants')
    return result

if __name__ == '__main__':
    if sys.argv[1]=='--database':run_database(sys.argv[2],sys.argv[3])
    else:run(sys.argv[1], sys.argv[2], sys.argv[3])
