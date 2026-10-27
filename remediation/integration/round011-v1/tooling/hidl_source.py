"""Conservative HIDL declared-source structure, not compiler/ABI/wire validation.
Every unrecognized statement remains an unsupported record with exact source text.
Package @major.minor denotes HIDL package version, never Android applicability.
"""
import re
from aidl_lex import code_mask,comment_mask
IDENT=r'[A-Za-z_]\w*'
def spans(text,start,end,separator=';',angles=False,masked=None):
 masked=code_mask(text) if masked is None else masked;stack=[];begin=start;angle=0
 for i in range(start,end):
  c=masked[i]
  if c in '([{':stack.append(c)
  elif c in ')]}':
   if stack:stack.pop()
  elif angles and not stack and c=='<':angle+=1
  elif angles and not stack and c=='>':angle=max(0,angle-1)
  elif c==separator and not stack and angle==0:
   if comment_mask(text[begin:i]).strip():yield begin,i
   begin=i+1
 if comment_mask(text[begin:end]).strip():yield begin,end
def annotations(text,start,end,masked=None):
 masked=code_mask(text) if masked is None else masked;result=[];position=start
 while True:
  while position<end and masked[position].isspace():position+=1
  match=re.match(r'@('+IDENT+r'(?:\.'+IDENT+r')*)',masked[position:end])
  if not match:return position,result
  initial=position;position+=match.end();arguments=''
  while position<end and masked[position].isspace():position+=1
  if position<end and masked[position]=='(':
   begin=position+1;depth=1;position+=1
   while position<end and depth:
    if masked[position]=='(':depth+=1
    elif masked[position]==')':depth-=1
    position+=1
   if depth:return initial,result
   arguments=text[begin:position-1]
  result.append(dict(name=match.group(1),arguments=arguments,raw=text[initial:position],semantics='unknown HIDL source annotation'))
def typed(text,start,end):
 result=[]
 for a,b in spans(text,start,end,',',True):
  initial,anns=annotations(text,a,b);clean=comment_mask(text[initial:b]).strip();match=re.fullmatch(r'(.+?)(?:\s+|(?<=[>\]]))('+IDENT+r')',clean,re.S)
  if not match:raise ValueError('Unrecognized typed variable '+clean)
  result.append(dict(name=match.group(2),type=re.sub(r'\s+',' ',match.group(1)).strip(),annotations=[],all_annotations=anns,raw=text[a:b]))
 if len({p['name'] for p in result})!=len(result):raise ValueError('Duplicate typed variable names')
 return result
def parse(text):
 mask=comment_mask(text);code=code_mask(text);package_match=re.match(r'\s*package\s+([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)@(\d+\.\d+)\s*;',mask);records=[];unsupported=[];imports=[];seen=set()
 package=package_match.group(1)+'@'+package_match.group(2) if package_match else None
 version=package_match.group(2) if package_match else None
 def add(kind,name,owner,start,end,params=None,return_type=None,anns=None,extras=None):
  canonical=package if kind=='package' else (owner if kind in ('type','interface','enum') and owner else (owner+'::'+name if owner else (package or 'unresolved-package')+'::'+name))
  if kind=='method':canonical+='('+','.join(p['type'] for p in (params or []))+')'
  visible=comment_mask(text[start:end]);leading=len(visible)-len(visible.lstrip());line=text.count('\n',0,start+leading)+1;key=(canonical,line)
  if key in seen:
   kind='unsupported';canonical+='::duplicate-locator:'+str(start);extras=dict(extras or {},parse_error='Duplicate declaration at one physical locator; raw syntax retained')
  seen.add(key);extra=dict(hidl_package_version=version,android_availability='unknown',wire_transaction_id=None,wire_codec_status='unknown',source_character_span=[start,end],semantic_status='Declared-source structure only; compiler validity, imports and runtime registration unverified');extra.update(extras or {})
  record=dict(kind=kind,name=name,owner=owner,package=package or '',canonical=canonical or 'unresolved-package:'+str(start),line=line,raw=text[start:end],parameters=params or [],return_type=return_type,annotations=[],all_annotations=anns or [],extras=extra)
  records.append(record)
  if kind=='unsupported':unsupported.append(dict(line=line,raw=record['raw'],reason=extra.get('parse_error','Unknown HIDL structure')))
  return record
 if not package_match:
  add('unsupported','unresolved-hidl-source',None,0,len(text),extras=dict(parse_error='Required HIDL package/version declaration absent or malformed'));return dict(records=records,unsupported=unsupported,imports=[],source_complete=False)
 add('package',package,None,package_match.start(),package_match.end())
 def scope(start,end,owner,context):
  for a,b in spans(text,start,end,masked=code):
   initial,anns=annotations(text,a,b,code)
   while initial<b and mask[initial].isspace():initial+=1
   raw=mask[initial:b].strip()
   if not raw:continue
   if b>=end or code[b]!=';':
    add('unsupported','unterminated-hidl-statement',owner,a,b,anns=anns,extras=dict(parse_error='Required statement semicolon absent; raw syntax retained'));continue
   imp=re.fullmatch(r'import\s+([^;{}]+)',raw)
   if imp and context=='package':imports.append(dict(raw=text[a:b],target=imp.group(1).strip(),line=text.count('\n',0,initial)+1));continue
   declaration=re.match(r'(interface|struct|union|safe_union|enum)\s+('+IDENT+r')\b',raw)
   if declaration and declaration.group(1)=='interface' and context=='compound' and code.find('{',initial,b)<0:declaration=None
   if declaration:
    local_start=initial;head=declaration.group(1);name=declaration.group(2);masked=code;brace=masked.find('{',local_start,b);close=masked.rfind('}',local_start,b)
    if head=='interface' and (context!='package' or not name.startswith('I')):
     add('unsupported',name,owner,a,b,anns=anns,extras=dict(parse_error='HIDL interfaces require global scope and I-prefixed name'));continue
    if brace<0 or close<brace:
     add('unsupported',name,owner,a,b,anns=anns,extras=dict(parse_error='Missing HIDL declaration body'));continue
    suffix=mask[close+1:b].strip();full=(owner+'.'+name) if owner else package+'::'+name;header=mask[initial:brace];extra=dict(hidl_declaration_kind=head,enclosing_type=owner,keyword_character_offset=initial,hidl_qualified_name=full)
    if head=='interface':
     extra['declared_extends']=header[declaration.end():].strip();extra['descriptor_source_name']=full
    if head=='enum':extra['storage_type_raw']=header[declaration.end():].strip()
    add('interface' if head=='interface' else 'enum' if head=='enum' else 'type',name,full,a,b,anns=anns,extras=extra)
    if head=='enum':
     for x,y in spans(text,brace+1,close,',',masked=code):
      value_start,value_anns=annotations(text,x,y,code);value=comment_mask(text[value_start:y]).strip();m=re.fullmatch(r'('+IDENT+r')(?:\s*=\s*(.+))?',value,re.S)
      if m:add('field',m.group(1),full,x,y,return_type=full,anns=value_anns,extras=dict(hidl_declaration_kind='enum-value',constant_expression_raw=m.group(2)))
      else:add('unsupported','unresolved-enum-value',full,x,y,extras=dict(parse_error='Unknown HIDL enum value'))
    else:scope(brace+1,close,full,'interface' if head=='interface' else 'compound')
    if suffix:
     if context=='compound' and re.fullmatch(IDENT,suffix):add('field',suffix,owner,close+1,b,return_type=full,extras=dict(hidl_declaration_kind='inline-compound-field'))
     else:add('unsupported','trailing-declaration-syntax',owner,close+1,b,extras=dict(parse_error='Unrecognized suffix after HIDL type body'))
    continue
   alias=re.fullmatch(r'typedef\s+(.+?)\s+('+IDENT+r')',raw,re.S)
   if alias:add('type',alias.group(2),(owner+'.'+alias.group(2)) if owner else package+'::'+alias.group(2),a,b,anns=anns,extras=dict(hidl_declaration_kind='typedef',target_type_raw=alias.group(1)));continue
   method=re.fullmatch(r'(oneway\s+)?('+IDENT+r')\s*\((.*?)\)\s*(?:generates\s*\((.*?)\))?',raw,re.S)
   if method and context=='interface':
    try:
     if method.group(1) and method.group(4) is not None:raise ValueError('HIDL oneway method cannot declare generated results')
     parameters=typed(raw,method.start(3),method.end(3));results=typed(raw,method.start(4),method.end(4)) if method.group(4) is not None else []
     if method.group(4) is not None and not results:raise ValueError('Empty HIDL generates clause')
     add('method',method.group(2),owner,a,b,params=parameters,return_type=None,anns=anns,extras=dict(hidl_declaration_kind='method',results=results,oneway=bool(method.group(1))))
    except ValueError as error:add('unsupported',method.group(2),owner,a,b,anns=anns,extras=dict(parse_error=str(error)))
    continue
   if context=='compound':
    try:
     fields=typed(raw,0,len(raw))
     if len(fields)!=1:raise ValueError('Expected exactly one HIDL field')
     field=fields[0];add('field',field['name'],owner,a,b,return_type=field['type'],anns=anns,extras=dict(hidl_declaration_kind='field'));continue
    except ValueError:pass
   add('unsupported','unresolved-hidl-statement',owner,a,b,anns=anns,extras=dict(parse_error='Unsupported HIDL declaration retained verbatim'))
 scope(package_match.end(),len(text),None,'package')
 return dict(records=records,unsupported=unsupported,imports=imports,source_complete=False,structural_statement_parse_complete=not unsupported,limitation='No compiler validation, complete type resolution, implicit inherited method expansion, Android/module availability or wire transaction inference')
