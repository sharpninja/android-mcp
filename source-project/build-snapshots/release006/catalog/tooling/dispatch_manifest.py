"""Offline generated alias manifest; never executes imported data or enables tools."""
import hashlib,json,sys

def generate(entries):
    seen={}; result=[]
    for entry in sorted(entries,key=lambda e:e['entry_id']):
        canonical=entry['canonical_signature']; bid=hashlib.sha256(canonical.encode()).hexdigest()
        if bid!=entry['entry_id']: raise ValueError('Canonical identity mismatch')
        if bid in seen: raise ValueError('Duplicate effective entry')
        seen[bid]=True
        document_hash=hashlib.sha256(json.dumps(entry,sort_keys=True,separators=(',',':'),ensure_ascii=False).encode()).hexdigest()
        contract={key:entry.get(key) for key in ('canonical_signature','kind','arguments','return_type','codec_status')}
        contract['codec_identity']=entry.get('extensions',{}).get('codec_identity')
        digest=hashlib.sha256(json.dumps(contract,sort_keys=True,separators=(',',':'),ensure_ascii=False).encode()).hexdigest()
        permission_hash=hashlib.sha256(json.dumps(entry.get('permission'),sort_keys=True,separators=(',',':'),ensure_ascii=False).encode()).hexdigest()
        # Names use the full hash, avoiding collision through human-friendly truncation.
        result.append(dict(tool_name='binding_'+bid,policy_tool_id='binding.invoke',binding_id=bid,schema_hash=digest,document_hash=document_hash,permission_hash=permission_hash,
                           generic_binding_id=bid,default_grant='deny',
                           dispatch_state='unavailable',
                           reason='Offline catalog only; runtime authority, target resolver and validated codec required'))
    return dict(format_version='1.0.0',aliases=result)

if __name__=='__main__':
    with open(sys.argv[1]) as f: entries=[json.loads(x) for x in f if x.strip()]
    with open(sys.argv[2],'x') as f:json.dump(generate(entries),f,indent=2)
