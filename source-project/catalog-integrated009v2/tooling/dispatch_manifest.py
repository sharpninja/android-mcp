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

def generate(entries):
    seen={}; result=[]
    for entry in sorted(entries,key=lambda e:e['entry_id']):
        canonical=entry['canonical_signature']; bid=hashlib.sha256(canonical.encode()).hexdigest()
        if bid!=entry['entry_id']: raise ValueError('Canonical identity mismatch')
        if bid in seen: raise ValueError('Duplicate effective entry')
        seen[bid]=True
        document_hash=hashlib.sha256(json.dumps(entry,sort_keys=True,separators=(',',':'),ensure_ascii=False).encode()).hexdigest()
        digest=contract_digest(entry)
        permission_hash=hashlib.sha256(json.dumps(entry.get('permission'),sort_keys=True,separators=(',',':'),ensure_ascii=False).encode()).hexdigest()
        # Names use the full hash, avoiding collision through human-friendly truncation.
        result.append(dict(tool_name='binding_'+bid,policy_tool_id='binding.invoke',binding_id=bid,schema_hash=digest,document_hash=document_hash,permission_hash=permission_hash,
                           generic_binding_id=bid,default_grant='deny',
                           dispatch_state='unavailable',
                           reason='Offline catalog only; runtime authority, target resolver and validated codec required'))
    return dict(format_version='1.1.0',contract_format_version=2,aliases=result)

if __name__=='__main__':
    with open(sys.argv[1]) as f: entries=[json.loads(x) for x in f if x.strip()]
    with open(sys.argv[2],'x') as f:json.dump(generate(entries),f,indent=2)
