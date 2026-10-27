"""AIDL declaration inventory only. Never infer wire tags/layouts or execute values."""
import re
from parsers import annotations,typ,without_comments,sha
from aidl_lex import chunks

def field(raw,descriptor,package,kind,ordinal,line):
    clean,anns=annotations(raw);clean=clean.strip().rstrip(';').strip()
    constant=bool(re.match(r'^const\b',clean));clean=re.sub(r'^const\s+','',clean)
    if kind=='enum':
        m=re.fullmatch(r'([A-Za-z_]\w*)\s*(?:=\s*(.*))?',clean,re.S)
        if not m:raise ValueError('Unresolved AIDL enumerant')
        name,value=m.groups();type_name=descriptor
    else:
        m=re.fullmatch(r'(.+?)\s+([A-Za-z_]\w*)\s*(?:=\s*(.*))?',clean,re.S)
        if not m:raise ValueError('Unresolved AIDL field declaration')
        type_name,name,value=m.groups()
        if any(c in type_name for c in ';={}()'):raise ValueError('Unsupported nested AIDL field syntax')
        type_name=typ(type_name)
    return dict(kind='field',canonical=descriptor+'#'+name,owner=descriptor,package=package,name=name,return_type=type_name,parameters=[],annotations=anns,all_annotations=anns,raw=raw,line=line,extras=dict(aidl_declaration_kind=kind,enum_constant=kind=='enum',is_constant=constant,constant_raw=value,declared_ordinal=ordinal,wire_tag=None,wire_layout_status='unknown; declaration order/value is not verified wire metadata'))

def enrich(text,declaration):
    """Preserve parser result; enrich structured metadata and interface constants."""
    result=dict(declaration);result['members']=list(declaration['members'])
    kind=result['declaration_kind'];descriptor=result.get('descriptor')
    if not descriptor:return result
    if kind=='interface':
        for i,member in enumerate(result['members']):
            if member['kind']=='unsupported' and re.match(r'^\s*const\b',annotations(member['raw'])[0]):
                try:result['members'][i]=field(member['raw'],descriptor,result['package'],'interface',i,member['line'])
                except ValueError:pass
        result['parse_status']='partial' if any(m['kind']=='unsupported' for m in result['members']) else 'methods-parsed'
        return result
    if kind not in ('parcelable','union','enum'):return result
    code=without_comments(text);clean,_=annotations(code)
    header=re.search(r'\b'+kind+r'\s+'+re.escape(descriptor.rsplit('.',1)[-1])+r'\b',clean)
    if not header:return result
    pos=header.end()
    while pos<len(clean) and clean[pos].isspace():pos+=1
    if pos<len(clean) and clean[pos]=='<':
        depth=0
        for i in range(pos,len(clean)):
            if clean[i]=='<':depth+=1
            elif clean[i]=='>':
                depth-=1
                if depth==0:pos=i+1;break
        else:return result
        while pos<len(clean) and clean[pos].isspace():pos+=1
    if pos>=len(clean) or clean[pos]!='{':return result # external declaration has no body
    start=pos;depth=0;quote=None;escape=False;end=None
    for i,c in enumerate(code[start:],start):
        if quote:
            if escape:escape=False
            elif c=='\\':escape=True
            elif c==quote:quote=None
        elif c in ('"',"'"):quote=c
        elif c=='{':depth+=1
        elif c=='}':
            depth-=1
            if depth==0:end=i;break
    if end is None:return result
    body=code[start+1:end];members=[];unsupported=0
    for ordinal,(raw,offset) in enumerate(chunks(body,',' if kind=='enum' else ';')):
        locator=body.find(raw,offset);line=code[:start+1+locator].count('\n')+1
        try:member=field(raw,descriptor,result['package'],kind,ordinal,line)
        except ValueError as error:
            unsupported+=1;member=dict(kind='unsupported',canonical=descriptor+'#unparsed:'+sha(raw),owner=descriptor,package=result['package'],name='unparsed',return_type=None,parameters=[],annotations=annotations(raw)[1],all_annotations=annotations(raw)[1],raw=raw,line=line,extras=dict(parse_error=str(error),declared_ordinal=ordinal))
        members.append(member)
    result.update(members=members,parse_status='field-metadata-partial' if unsupported else 'field-metadata-parsed',reason='Source fields/enumerants inventoried; imports, runtime type resolution, wire tags/layouts and codecs remain unverified')
    return result
