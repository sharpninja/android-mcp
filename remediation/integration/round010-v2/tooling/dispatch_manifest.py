"""Offline generated alias manifest; never executes imported data or enables tools."""
import hashlib,json,sys

def contract_digest(entry):
    """Version2 semantic contract; source locators/permissions do not select codecs."""
    def type_ref(value):
        if not isinstance(value,dict):return value
        return {k:value[k] for k in ('text','status','resolved_binding_id','codec_status','codec_id') if k in value}
    contract=dict(contract_format_version=2,canonical_signature=entry.get('canonical_signature'),kind=entry.get('kind'),arguments=[type_ref(v) for v in entry.get('arguments',[])],return_type=type_ref(entry.get('return_type')),codec_status=entry.get('codec_status'),codec_identity=entry.get('extensions',{}).get('codec_identity'))
    shape=entry.get('extensions',{}).get('declared_type_shape')
    if shape is not None:contract['declared_type_shape']=shape
    return hashlib.sha256(json.dumps(contract,sort_keys=True,separators=(',',':'),ensure_ascii=False).encode()).hexdigest()

def generate(entries,resolved_status=None):
    """resolved_status comes from a trusted configured store, never request JSON.
    Even resolved aliases remain offline/deny: metadata is not runtime authority.
    """
    selected={};view=None
    if resolved_status is not None:
        import device_resolution as dr
        view=resolved_status.get('snapshot')
        if not view or resolved_status.get('stale') or resolved_status.get('capability_epoch')!=view['capability_epoch'] or resolved_status.get('generation')!=view['view_generation']:raise ValueError('Fresh active device view required')
        dr.validate(view,'Snapshot')
        if dr.digest({k:v for k,v in view.items() if k!='view_id'})!=view['view_id']:raise ValueError('Device view hash mismatch')
        selected={v['entry_id']:v for v in view['entries']}
    seen={}; result=[]
    for entry in sorted(entries,key=lambda e:e['entry_id']):
        canonical=entry['canonical_signature']; bid=hashlib.sha256(canonical.encode()).hexdigest()
        if bid!=entry['entry_id']: raise ValueError('Canonical identity mismatch')
        if bid in seen: raise ValueError('Duplicate effective entry')
        seen[bid]=True
        document_hash=hashlib.sha256(json.dumps(entry,sort_keys=True,separators=(',',':'),ensure_ascii=False).encode()).hexdigest()
        digest=contract_digest(entry);resolved=selected.get(bid)
        if view is not None:
            if resolved is None:raise ValueError('Device view omits inventory entry')
            digest=resolved['schema_hash']
        permission_hash=hashlib.sha256(json.dumps(entry.get('permission'),sort_keys=True,separators=(',',':'),ensure_ascii=False).encode()).hexdigest()
        # Names use the full hash, avoiding collision through human-friendly truncation.
        result.append(dict(tool_name='binding_'+bid,policy_tool_id='binding.invoke',binding_id=bid,schema_hash=digest,document_hash=document_hash,permission_hash=permission_hash,
                           generic_binding_id=bid,default_grant='deny',
                           device_view_id=view['view_id'] if view else None,device_state=resolved['state'] if resolved else 'unknown',type_graph_digest=resolved['graph_digest'] if resolved else None,
                           dispatch_state='unavailable',
                           reason='Offline catalog only; runtime authority, target resolver and validated codec required'))
    if view is not None and set(selected)!=set(seen):raise ValueError('Device view and supplied inventory differ')
    return dict(format_version='1.1.0',contract_format_version=2,aliases=result)

if __name__=='__main__':
    with open(sys.argv[1]) as f: entries=[json.loads(x) for x in f if x.strip()]
    with open(sys.argv[2],'x') as f:json.dump(generate(entries),f,indent=2)
