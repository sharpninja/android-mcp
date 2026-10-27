"""Conservative source-declaration parsers. No runtime access or wire-codec claims."""
import re,json,hashlib
from foundation_extract import split_top,annotations as old_annotations,permission_ast as old_ast

def sha(x):return hashlib.sha256(x.encode()).hexdigest()
def annotations(text):
 # Equal-length masking preserves source offsets. Quoted @ signs are ordinary data.
 chars=list(text);anns=[];i=0;quote=None;esc=False
 while i<len(text):
  c=text[i]
  if quote:
   if esc:esc=False
   elif c=='\\':esc=True
   elif c==quote:quote=None
   i+=1;continue
  if c in ('"',"'"):quote=c;i+=1;continue
  if c!='@':i+=1;continue
  m=re.match(r'@([\w.$]+)',text[i:])
  if not m:i+=1;continue
  name=m.group(1)
  if name=='interface':i+=len(m.group(0));continue
  a=i;b=i+len(m.group(0));end=b;args='';j=b
  while j<len(text) and text[j].isspace():j+=1
  if j<len(text) and text[j]=='(':
   depth=0;q=None;escape=False;end=None
   for k in range(j,len(text)):
    cc=text[k]
    if q:
     if escape:escape=False
     elif cc=='\\':escape=True
     elif cc==q:q=None
    elif cc in ('"',"'"):q=cc
    elif cc=='(':depth+=1
    elif cc==')':
     depth-=1
     if depth==0:end=k+1;args=text[j+1:k];break
   if end is None:raise ValueError('Unclosed annotation')
  anns.append(dict(name=name,arguments=args,raw=text[a:end],start=a,end=end))
  chars[a:end]=['\n' if x=='\n' else ' ' for x in text[a:end]];i=end
 return ''.join(chars),anns
def unknown(reason,raw=None):
 d=dict(op='unknown',reason=reason)
 if raw is not None:d['raw']=raw
 return d
def permission(anns,symbols=None,aidl=False):
 symbols=symbols or {};clauses=[]
 def resolve(a):
  if a.get('op')=='unknown' and a.get('reason')=='Unresolved symbolic permission expression' and a.get('raw') in symbols:
   return dict(op='permission',name=symbols[a['raw']],**{'x-symbol':a['raw']})
  if a.get('op')=='permission':
   name=a['name']
   if name in symbols:return dict(op='permission',name=symbols[name],**{'x-symbol':name})
   if name.startswith('Manifest.permission.') or '.Manifest.permission.' in name or '.Permission.' in name or re.search(r'\s',name):return unknown('Source annotation contains a symbolic reference or non-permission predicate, not a proven literal permission name',name)
   if aidl and '.' not in name:return unknown('AIDL short permission namespace not resolved by this extractor',name)
  return {k:resolve(v) if isinstance(v,dict) else [resolve(t) if isinstance(t,dict) else t for t in v] if isinstance(v,list) else v for k,v in a.items()}
 for a in anns:
  n=a['name'];short=n.split('.')[-1]
  if short not in ('RequiresPermission','EnforcePermission','PermissionManuallyEnforced','RequiresNoPermission','Read','Write'):continue
  if short in ('Read','Write') and 'RequiresPermission' not in n:continue
  args=a['arguments']
  # AIDL accepts array syntax in addition to Java's brace syntax.
  if aidl:args=re.sub(r'(allOf|anyOf)\s*=\s*\[([^\]]*)\]',r'\1={\2}',args)
  clauses.append(resolve(old_ast(n,args)))
 return clauses[0] if len(clauses)==1 else dict(op='all',children=clauses) if clauses else unknown('No declared recognized permission metadata; absence is not permission-free')

def typ(text):
 clean,_=annotations(text);clean=re.sub(r'\b(final|optional)\s+','',clean).strip()
 clean=re.sub(r'\s+',' ',clean);clean=re.sub(r'\s*([<>,\[\]])\s*',r'\1',clean)
 return clean
def param(text,aidl=False):
 clean,anns=annotations(text);clean=clean.strip();direction=None;name=None
 if aidl:
  m=re.match(r'(inout|in|out)\s+',clean)
  if m:direction=m.group(1);clean=clean[m.end():]
  m=re.fullmatch(r'(.+?)\s+([\w$]+)',clean,re.S)
  if not m:raise ValueError('AIDL parameter type/name unresolved')
  clean,name=m.groups()
 else:
  clean=re.sub(r'\b(final|optional)\s+','',clean)
  m=re.fullmatch(r'(.+?)\s+([A-Za-z_$][\w$]*)(?:\s*=.*)?',clean,re.S)
  if m and not m.group(1).endswith(('extends','super')):clean,name=m.groups()
 return dict(type=typ(clean),name=name,direction=direction,annotations=anns)
def head_return(head):
 h=re.sub(r'^(?:(?:public|protected|private|static|final|abstract|default|synchronized|native|strictfp|deprecated|inline|operator|suspend|oneway)\s+)+','',head).strip()
 if h.startswith('<'):
  depth=0
  for i,c in enumerate(h):
   if c=='<':depth+=1
   elif c=='>':
    depth-=1
    if depth==0:h=h[i+1:].strip();break
 return typ(h)

def member(raw,owner,package,aidl=False):
 clean_full,allanns=annotations(raw);clean=clean_full.strip().rstrip(';').strip()
 kind='aidl-member' if aidl else clean.split()[0]
 body=clean if aidl else clean[len(kind):].strip()
 if '(' in body and (aidl or kind in ('ctor','method')):
  a=body.index('(');b=body.rfind(')')
  if b<a:raise ValueError('Unclosed parameter list')
  h=body[:a].strip();m=re.search(r'([\w$]+)$',h)
  if not m:raise ValueError('Unresolved callable name')
  name=m.group(1);ret=None if kind=='ctor' else head_return(h[:m.start()].strip())
  original_a=clean_full.index('(');original_b=clean_full.rfind(')')
  params=[param(x,aidl) for x in split_top(raw[original_a+1:original_b]) if x.strip()]
  # Capture declaration annotations separately; parameter annotations cannot grant the callable authority.
  anns=[a for a in allanns if a['end']<=original_a]
  canonical=owner+'#'+('<init>' if kind=='ctor' else name)+'('+','.join(p['type'] for p in params)+')'
  extra={}
  if aidl:
   suffix=body[b+1:].strip();explicit=re.fullmatch(r'=\s*(\d+)',suffix)
   extra=dict(oneway=bool(re.search(r'\boneway\b',body[:a])),declared_method_id=int(explicit.group(1)) if explicit else None,wire_transaction_code=None,wire_status='unknown; generated Binder transaction constants and codecs not imported')
   if suffix and not explicit:extra['unresolved_suffix']=suffix
  return dict(kind='constructor' if kind=='ctor' else 'method' if kind=='method' else kind,canonical=canonical,owner=owner,package=package,name=name,return_type=ret,parameters=params,annotations=anns,all_annotations=allanns,raw=raw,extras=extra)
 if aidl:raise ValueError('AIDL non-method statement preserved but not interpreted')
 if kind not in ('field','enum_constant','property'):raise ValueError('Unsupported signature member')
 h=body.split('=',1)[0].strip();m=re.search(r'([\w$]+)$',h)
 if not m:raise ValueError('Unresolved field name')
 name=m.group(1);value=body.split('=',1)[1].strip() if '=' in body else None
 return dict(kind='field' if kind=='enum_constant' else kind,canonical=owner+'#'+name,owner=owner,package=package,name=name,return_type=head_return(h[:m.start()].strip()),parameters=[],annotations=allanns,all_annotations=allanns,raw=raw,extras=dict(constant_raw=value,enum_constant=kind=='enum_constant'))

def signature(text):
 package=None;owner=None
 for line,raw in enumerate(text.splitlines(),1):
  s=raw.strip()
  if not s or s.startswith('//'):continue
  if s=='}':
   if owner is not None:owner=None
   else:package=None
   continue
  try:
   clean,anns=annotations(s)
   if clean.startswith('package '):
    package=clean.split()[1];owner=None;r=dict(kind='package',canonical=package,package=package,owner=None,name=package,parameters=[],return_type=None,annotations=anns,all_annotations=anns,raw=s,extras={})
   else:
    m=re.search(r'(?<![\w.])(@interface|class|interface|enum)\s+([\w.$]+)',clean)
    if m and clean.endswith('{'):
     if not package:raise ValueError('Type without package')
     kind='annotation' if m.group(1)=='@interface' else m.group(1);name=m.group(2);owner=package+'.'+name;r=dict(kind=kind,canonical=owner,package=package,owner=owner,name=name,parameters=[],return_type=None,annotations=anns,all_annotations=anns,raw=s,extras=dict(type_header=clean))
    else:
     if not owner:raise ValueError('Member without owner')
     r=member(s,owner,package)
   r['line']=line;yield r
  except Exception as e:
   if s.endswith('{'):owner=None
   yield dict(kind='unsupported',canonical='unparsed:'+sha(s),package=package or '',owner=owner,name='unparsed',parameters=[],return_type=None,annotations=[],all_annotations=[],raw=s,line=line,extras=dict(parse_error=str(e)))

def without_comments(text):
 # Keep string/char literals intact, including // and /* inside quoted strings.
 return re.sub(r'"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'|/\*.*?\*/|//[^\n]*',lambda m:m.group(0) if m.group(0).startswith(('"',"'")) else '\n'*m.group(0).count('\n')+' ',text,flags=re.S)

def aidl(text):
 code=without_comments(text);p=re.search(r'\bpackage\s+([\w.]+)\s*;',code);pkg=p.group(1) if p else ''
 imports=re.findall(r'\bimport\s+([\w.]+)\s*;',code)
 clean,anns=annotations(code)
 d=re.search(r'\b(interface|parcelable|union|enum)\s+([\w]+)',clean)
 if not d:return dict(package=pkg,descriptor=None,declaration_kind=None,imports=imports,annotations=anns,members=[],parse_status='unsupported',reason='No supported top-level AIDL declaration')
 kind,name=d.groups();descriptor=(pkg+'.' if pkg else '')+name
 # Locate the actual declaration before stripping annotations, preserving raw member annotation arguments.
 dm=re.search(r'\b'+kind+r'\s+'+re.escape(name)+r'\b',code)
 prefix=code[:dm.start()];_,topanns=annotations(prefix[prefix.rfind(';')+1:])
 start=code.find('{',dm.end());members=[];status='declaration-only'
 if start>=0:
  depth=0;quote=None;esc=False;end=None
  for i,c in enumerate(code[start:],start):
   if quote:
    if esc:esc=False
    elif c=='\\':esc=True
    elif c==quote:quote=None
   elif c in ('"',"'"):quote=c
   elif c=='{':depth+=1
   elif c=='}':
    depth-=1
    if depth==0:end=i;break
  if end is None:return dict(package=pkg,descriptor=descriptor,declaration_kind=kind,imports=imports,annotations=topanns,members=[],parse_status='unsupported',reason='Unbalanced declaration body')
  body=code[start+1:end]
  if kind=='interface':
   chunks=[c.strip() for c in split_top(body,';') if c.strip()];status='methods-parsed'
   offset=0
   for ordinal,raw in enumerate(chunks):
    locator=body.find(raw,offset);offset=locator+len(raw)
    try:r=member(raw,descriptor,pkg,True);r['extras']['declaration_ordinal']=ordinal
    except Exception as e:r=dict(kind='unsupported',canonical=descriptor+'#unparsed:'+sha(raw),owner=descriptor,package=pkg,name='unparsed',return_type=None,parameters=[],annotations=annotations(raw)[1],all_annotations=annotations(raw)[1],raw=raw,extras=dict(parse_error=str(e)));status='partial'
    r['line']=code[:start+1].count('\n')+1+body[:locator].count('\n');members.append(r)
  else:status='structured-body-retained; fields/enumerants/union tags unresolved'
 return dict(package=pkg,descriptor=descriptor,declaration_kind=kind,imports=imports,annotations=topanns,members=members,parse_status=status,reason='All source bytes retained; generated transaction IDs, resolved imports and wire codecs unknown',oneway=bool(re.search(r'\boneway\s+interface\s+'+re.escape(name),code)))
