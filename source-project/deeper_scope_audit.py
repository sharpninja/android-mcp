"""Versioned metadata correction and conservative Binder/HIDL discovery hints.
Filename signals do not prove semantic irrelevance, runtime availability or a
complete binding surface. All prior audit snapshots remain unchanged.
"""
import pathlib,json,hashlib,collections,datetime

def classify_tree(entries):
    extensions=collections.Counter();hidl=[];binder=[];source_files=0
    source_extensions={'.c','.cc','.cpp','.cxx','.h','.hpp','.java','.kt','.rs','.hal','.proto','.aidl','.py','.bp','.go'}
    for entry in entries:
        if entry['kind']!='blob':continue
        path=entry['path'];suffix=pathlib.PurePosixPath(path).suffix.lower();extensions[suffix]+=1
        source_files+=suffix in source_extensions
        item=dict(path=path,git_blob=entry['oid'])
        if suffix=='.hal':hidl.append(dict(item,signal='HIDL filename; source bytes/semantics not inspected'))
        elif 'binder' in path.lower() or 'aidl' in path.lower():binder.append(dict(item,signal='Binder/AIDL filename token; source semantics not inspected'))
    bucket='hidl-filename-candidates' if hidl else 'binder-filename-candidates' if binder else 'source-content-uninspected' if source_files else 'prebuilt-or-other-uninspected'
    return dict(scope_bucket=bucket,source_filename_count=source_files,extensions=dict(extensions),candidate_paths=hidl+binder,semantic_irrelevance_proven=False,limitations='Filename evidence only; source contents, generators, native Binder and prebuilt interfaces remain uninspected')

def run(root):
    root=pathlib.Path(root).resolve();original=root/'outside-corpus-audit/classification-batchfull985.json';out=root/'outside-corpus-audit/classification-full985-v2.json'
    if out.exists():raise ValueError('Preserve versioned audit output')
    raw=original.read_bytes();doc=json.loads(raw);buckets=collections.Counter();candidates=0
    for project in doc['projects']:
        tree_path=root/project['tree_evidence'];tree_raw=tree_path.read_bytes();tree=json.loads(tree_raw)
        if tree['revision']!=project['revision']:raise ValueError('Pinned tree revision mismatch')
        project['selection_scope']='historical original102-project acquisition corpus'
        project['selection_reason']='Outside original acquisition corpus; complete pinned tree now enumerated. '+project['reason']
        project['supplemental_source_selection_eligible']=project['classification']=='relevant'
        project['tree_evidence_sha256']=hashlib.sha256(tree_raw).hexdigest()
        if project['classification']=='unknown':
            evidence=classify_tree(tree['entries']);project['deeper_scope_evidence']=evidence;buckets[evidence['scope_bucket']]+=1;candidates+=len(evidence['candidate_paths'])
        if project['classification']=='irrelevant' and tree['entries']:raise ValueError('Irrelevance requires verified empty tree')
    doc.update(metadata_version=2,updated_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),previous_snapshot=dict(path=str(original.relative_to(root)),sha256=hashlib.sha256(raw).hexdigest()),unknown_scope_buckets=dict(buckets),unknown_filename_candidate_paths=candidates,global_complete=False)
    doc['classification_rules']['unknown']+='; version2 adds filename-only discovery dimensions without treating absence of signals as irrelevance'
    out.write_text(json.dumps(doc,indent=2));print(json.dumps(dict(path=str(out),classification_counts=doc['counts'],unknown_scope_buckets=dict(buckets),filename_candidate_paths=candidates,sha256=hashlib.sha256(out.read_bytes()).hexdigest()),indent=2));return doc

if __name__=='__main__':run(pathlib.Path(__file__).resolve().parent)
