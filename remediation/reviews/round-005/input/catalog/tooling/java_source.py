"""Java source declaration metadata using the pinned Tree-sitter Java grammar.
Method bodies/local/anonymous implementation classes are not API declarations.
All parse errors retain byte/line locators into the immutable complete source.
No imported source is executed, compiled, or loaded as a runtime type.
"""
import re,hashlib,importlib.metadata
from tree_sitter import Language,Parser
import tree_sitter_java
from parsers import annotations,typ,sha
LANGUAGE=Language(tree_sitter_java.language())
TYPE_KINDS={'class_declaration':'class','interface_declaration':'interface','enum_declaration':'enum','annotation_type_declaration':'annotation','record_declaration':'class'}
JAVA_LANG={'String','Object','Class','Throwable','Exception','RuntimeException','Error','Enum','Annotation','Deprecated','Override','SuppressWarnings','SafeVarargs','FunctionalInterface','Boolean','Byte','Character','Short','Integer','Long','Float','Double','Void','Number','CharSequence','StringBuilder','StringBuffer','Iterable','Comparable','Cloneable','AutoCloseable'}
def parse(text,source_path='source.java'):
 data=text.encode('utf-8') if isinstance(text,str) else text;root=Parser(LANGUAGE).parse(data).root_node
 records=[];issues=[];package='';imports={};wildcards=[];excluded={};binary_by_owner={}
 def txt(n):return data[n.start_byte:n.end_byte].decode('utf-8',errors='replace') if n else ''
 for n in root.named_children:
  if n.type=='package_declaration':
   ids=[x for x in n.named_children if x.type in ('identifier','scoped_identifier')];package=txt(ids[-1]) if ids else ''
  elif n.type=='import_declaration':
   value=re.sub(r'^import\s+(?:static\s+)?|;\s*$','',txt(n)).strip()
   if value.endswith('.*'):wildcards.append(value[:-2])
   else:imports[value.split('.')[-1]]=value
 def mask_comments(value):
  return re.sub(r'"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'|/\*.*?\*/|//[^\n]*',lambda m:m.group(0) if m.group(0).startswith(('"',"'")) else ''.join('\n' if c=='\n' else ' ' for c in m.group(0)),value,flags=re.S)
 def qualify_anns(value):
  cleaned=mask_comments(value)
  _,items=annotations(cleaned)
  for a in items:
   raw=a['name'];first,sep,tail=raw.partition('.')
   if first in imports:a['name']=imports[first]+(sep+tail if sep else '');a['name_resolution']='explicit-import'
   elif '.' in raw:a['name_resolution']='qualified-source-spelling'
   elif raw in JAVA_LANG:a['name']='java.lang.'+raw;a['name_resolution']='java-lang'
   elif 'android.annotation' in wildcards and raw in ('RequiresPermission','EnforcePermission','RequiresNoPermission','SystemApi','TestApi','FlaggedApi','Nullable','NonNull'):
    a['name']='android.annotation.'+raw;a['name_resolution']='known-annotation-wildcard'
   else:a['name']=(package+'.' if package else '')+raw;a['name_resolution']='package-candidate; unresolved wildcard imports retained'
   a['source_spelling']=raw
   a['source_raw']=value[a['start']:a['end']]
   args=a['arguments']
   for simple,full in imports.items():args=re.sub(r'(?<![\w.$])'+re.escape(simple)+r'(?=\.)',full,args)
   a['source_arguments']=a['arguments'];a['arguments']=args
  return items
 def modifiers(n):
  m=next((c for c in n.named_children if c.type=='modifiers'),None);value=txt(m);clean,_=annotations(mask_comments(value))
  return re.findall(r'\b(public|protected|private|static|final|abstract|native|synchronized|strictfp|default|transient|volatile|sealed|non-sealed)\b',clean),qualify_anns(value)
 def type_text(n,extra=''):
  raw=txt(n)+extra;clean=typ(raw)
  # Resolve explicit imports without guessing wildcard imports/type variables.
  for simple,full in imports.items():
   if full.startswith('java.lang.'):continue
   clean=re.sub(r'(?<![\w.$])'+re.escape(simple)+r'(?![\w$])',full,clean)
  return clean
 def parameter(n):
  t=n.child_by_field_name('type');name=n.child_by_field_name('name');mod,anns=modifiers(n)
  if n.type=='spread_parameter':
   t=t or next((c for c in n.named_children if c.type not in ('modifiers','variable_declarator','annotation','marker_annotation')),None)
   decl=next((c for c in n.named_children if c.type=='variable_declarator'),None);name=name or (decl.child_by_field_name('name') if decl else None)
  dims=''.join(txt(c) for c in n.named_children if c.type=='dimensions')
  return dict(type=type_text(t,('...' if n.type=='spread_parameter' else '')+dims),name=txt(name) or None,direction=None,annotations=qualify_anns(txt(n)),source_type=txt(t),source_byte_range=[n.start_byte,n.end_byte])
 def row(n,kind,owner,name,return_type=None,parameters=None,canonical=None,anns=None,extra=None,raw=None):
  mods,ma=modifiers(n);parameters=parameters or [];body=n.child_by_field_name('body')
  header=data[n.start_byte:body.start_byte].decode('utf8',errors='replace').strip() if body else txt(n).strip()
  if raw is not None:header=raw
  visibility=next((x for x in mods if x in ('public','protected','private')),None)
  defaults=extra or {}
  if visibility is None:visibility='public' if defaults.get('owner_kind') in ('interface','annotation') and kind in ('method','field') else 'package'
  e=dict(source_path=source_path,source_byte_range=[n.start_byte,n.end_byte],modifiers=mods,effective_visibility=visibility,owner_binary_name=binary_by_owner.get(owner),public_sdk_membership='unknown until checked against published SDK metadata');e.update(defaults)
  return dict(kind=kind,canonical=canonical or owner+'#'+name,owner=owner,package=package,name=name,return_type=return_type,parameters=parameters,annotations=ma if anns is None else anns,all_annotations=qualify_anns(header),raw=header,line=n.start_point[0]+1,extras=e)
 def excluded_node(kind):excluded[kind]=excluded.get(kind,0)+1
 def type_decl(n,outer=None,depth=0):
  if depth>128:issues.append(dict(reason='Nested type depth exceeds128',line=n.start_point[0]+1,byte_range=[n.start_byte,n.end_byte]));return
  name=txt(n.child_by_field_name('name'));owner=outer+'.'+name if outer else (package+'.' if package else '')+name;kind=TYPE_KINDS[n.type]
  binary_by_owner[owner]=binary_by_owner[outer]+'$'+name if outer else (package.replace('.','/')+'/' if package else '')+name
  r=row(n,kind,owner,name,canonical=owner,extra=dict(declaration_kind=n.type,type_parameters=txt(n.child_by_field_name('type_parameters')),superclass=txt(n.child_by_field_name('superclass')),interfaces=txt(n.child_by_field_name('interfaces'))));records.append(r)
  body=n.child_by_field_name('body')
  if body is None:return
  def child(c):
   if c.type in TYPE_KINDS:type_decl(c,owner,depth+1)
   elif c.type=='enum_body_declarations':
    for x in c.named_children:child(x)
   elif c.type in ('method_declaration','constructor_declaration','compact_constructor_declaration','annotation_type_element_declaration'):
    ctor=c.type in ('constructor_declaration','compact_constructor_declaration');name=txt(c.child_by_field_name('name'));ps=c.child_by_field_name('parameters');params=[];receivers=[]
    if ps:
     for p in ps.named_children:
      if p.type in ('formal_parameter','spread_parameter'):params.append(parameter(p))
      elif p.type=='receiver_parameter':receivers.append(txt(p))
    canonical=owner+'#'+('<init>' if ctor else name)+'('+','.join(p['type'] for p in params)+')'
    mods,anns=modifiers(c);r=row(c,'constructor' if ctor else 'method',owner,name,None if ctor else type_text(c.child_by_field_name('type')),params,canonical,anns,dict(owner_kind=kind,type_parameters=txt(c.child_by_field_name('type_parameters')),receiver_parameters=receivers,default_value_raw=txt(c.child_by_field_name('value')),source_declared_constructor=ctor))
    records.append(r)
   elif c.type in ('field_declaration','constant_declaration'):
    t=c.child_by_field_name('type');mods,anns=modifiers(c)
    for v in c.named_children:
     if v.type!='variable_declarator':continue
     name=txt(v.child_by_field_name('name'));value=v.child_by_field_name('value');dims=''.join(txt(x) for x in v.named_children if x.type=='dimensions');value_raw=txt(value)
     raw=txt(c);extra=dict(owner_kind=kind,constant_raw=value_raw if len(value_raw)<=4096 else None,initializer_source_range=[value.start_byte,value.end_byte] if value else None,enum_constant=False)
     if len(raw)>16384:raw=type_text(t)+' '+name+' [initializer retained at source byte range]';extra['raw_signature_is_excerpt']=True
     records.append(row(c,'field',owner,name,type_text(t,dims),anns=anns,extra=extra,raw=raw))
   elif c.type=='enum_constant':
    name=txt(c.child_by_field_name('name'));records.append(row(c,'field',owner,name,owner,extra=dict(owner_kind='enum',enum_constant=True,effective_visibility='public')))
   elif c.type not in ('line_comment','block_comment'):excluded_node(c.type)
  for c in body.named_children:child(c)
 if package:
  n=next(n for n in root.named_children if n.type=='package_declaration');records.append(row(n,'package',None,package,canonical=package,extra=dict(declaration_kind='package')))
 for n in root.named_children:
  if n.type in TYPE_KINDS:type_decl(n)
  elif n.type not in ('package_declaration','import_declaration','line_comment','block_comment'):excluded_node(n.type)
 def errors(n):
  if n.type=='ERROR' or n.is_missing:
   issues.append(dict(reason='Java AST '+('missing token' if n.is_missing else 'ERROR'),line=n.start_point[0]+1,byte_range=[n.start_byte,n.end_byte],raw_excerpt=txt(n)[:4096],source_bytes_retained=True))
  elif n.has_error:
   for c in n.children:errors(c)
 errors(root)
 for issue in issues:
  locator=str(issue['byte_range']);raw=issue.get('raw_excerpt','')
  records.append(dict(kind='unsupported',canonical='unparsed-java:'+source_path+':'+locator+':'+sha(raw),owner=None,package=package,name='unparsed',return_type=None,parameters=[],annotations=[],all_annotations=[],raw=raw,line=issue['line'],extras=dict(parse_error=issue['reason'],source_byte_range=issue['byte_range'])))
 return dict(records=records,issues=issues,parse_errors=len(issues),package=package,imports=imports,wildcard_imports=wildcards,excluded_implementation_nodes=excluded,parser='tree-sitter-java'+importlib.metadata.version('tree-sitter-java')+' / tree-sitter'+importlib.metadata.version('tree-sitter'))
