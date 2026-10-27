"""Published SDK API-version index: exact JVM member keys, conservative semantics.
The index is not a Java signature file: type kind, field types, modifiers,
constants, generics, throws and permission annotations are not encoded here.
Historical/removed and future declarations are enumerated separately.
"""
import xml.etree.ElementTree as ET,xml.parsers.expat,collections
PRIMITIVES={'B':'byte','C':'char','D':'double','F':'float','I':'int','J':'long','S':'short','Z':'boolean','V':'void'}
def version(v):
 try:return tuple(map(int,v.split('.')))+(0,)*(2-len(v.split('.')))
 except (AttributeError,ValueError):return None
def descriptor_type(text,i=0,allow_void=False):
 start=i;dims=0
 while i<len(text) and text[i]=='[':dims+=1;i+=1
 if i>=len(text):raise ValueError('Incomplete JVM descriptor')
 c=text[i]
 if c in PRIMITIVES:
  if c=='V' and (dims or not allow_void):raise ValueError('Illegal void JVM descriptor')
  name=PRIMITIVES[c];i+=1
 elif c=='L':
  end=text.find(';',i)
  if end<0:raise ValueError('Unterminated object JVM descriptor')
  name=text[i+1:end].replace('/','.').replace('$','.');i=end+1
 else:raise ValueError('Unsupported JVM descriptor '+text[start:])
 return name+'[]'*dims,i
def method_descriptor(value):
 p=value.find('(')
 if p<1:raise ValueError('Missing method JVM descriptor')
 name=value[:p];args=[];i=p+1
 while i<len(value) and value[i]!=')':
  t,i=descriptor_type(value,i);args.append(t)
 if i>=len(value):raise ValueError('Unclosed method JVM descriptor')
 result,i=descriptor_type(value,i+1,True)
 if i!=len(value):raise ValueError('Trailing method JVM descriptor')
 return name,args,result
def parse(data,target=None,preserve_history=False):
 if target is None:raise ValueError('Explicit target platform API required')
 root=ET.fromstring(data);records=[];excluded=[];issues=[];limit=version(target)
 if limit is None:raise ValueError('Numeric platform API major.minor required')
 if root.tag!='api' or root.get('version')!='4':raise ValueError('Unsupported SDK API index format')
 positions=[];locator=xml.parsers.expat.ParserCreate()
 def locate(tag,attrs):
  if tag in ('class','method','field'):positions.append((tag,locator.CurrentLineNumber))
 locator.StartElementHandler=locate;locator.Parse(data,True)
 nodes=[n for n in root.iter() if n.tag in ('class','method','field')]
 if [n.tag for n in nodes]!=[tag for tag,line in positions]:raise ValueError('API index locator alignment failed')
 lines={id(n):line for n,(tag,line) in zip(nodes,positions)}
 def active(node,inherited):
  since=node.get('since',inherited.get('since','1'));removed=node.get('removed',inherited.get('removed'));v=version(since);rv=version(removed) if removed else None
  attrs=dict(inherited);attrs.update(node.attrib)
  known=v is not None and (not removed or rv is not None)
  return known and v<=limit and (rv is None or rv>limit),attrs,known
 for ordinal,c in enumerate(root.findall('class'),1):
  owner_binary=c.get('name','');owner=owner_binary.replace('/','.').replace('$','.');package=owner_binary.rsplit('/',1)[0].replace('/','.') if '/' in owner_binary else ''
  current,life,known=active(c,{})
  for child_index,node in enumerate([c]+[x for x in c if x.tag in ('method','field')]):
   enabled,lifecycle,understood=(current,life,known) if node is c else active(node,life)
   activity=False if (known and not current) or (understood and not enabled) else True if known and understood else None
   if not preserve_history and (not current or not enabled):
    excluded.append(dict(owner=owner_binary,tag=node.tag,attributes=node.attrib,reason='not applicable at target platform API' if understood else 'unknown API lifecycle'));continue
   raw=ET.Element(node.tag,node.attrib);raw=ET.tostring(raw,encoding='unicode')
   name=owner.split('.')[-1] if node is c else node.get('name','');kind='type' if node is c else 'field';parameters=[];ret=None
   membership='declared-current-in-official-SDK-API-index' if activity is True else 'declared-in-official-index; inactive-at-target' if activity is False else 'declared-in-official-index; target-applicability-unknown'
   extra=dict(jvm_owner=owner_binary,api_index_attributes=node.attrib,api_owner_attributes=c.attrib,api_lifecycle=lifecycle,active_at_target=activity,index_inventory_view='all-declared' if preserve_history else 'target-current',public_sdk_membership=membership,target_platform_api=target,source_kind_semantics='published API lifecycle index; not complete Java signature',index_ordinal=ordinal,member_ordinal=child_index)
   canonical=owner_binary if node is c else owner_binary+'#'+name
   if node is c:extra.update(type_kind_status='unknown',type_kind_reason='API index omits interface/class/annotation distinction',supertypes=[dict(x.attrib,relation=x.tag) for x in node if x.tag in ('extends','implements')])
   elif node.tag=='method':
    try:
     name,args,ret=method_descriptor(name);kind='constructor' if name=='<init>' else 'method';parameters=[dict(type=t,name=None,direction=None,annotations=[]) for t in args]
    except ValueError as e:kind='unsupported';extra['parse_error']=str(e);issues.append(dict(owner=owner_binary,name=name,error=str(e)))
   else:extra['field_type_status']='unknown'
   records.append(dict(kind=kind,canonical=canonical,owner=owner,package=package,name=name,return_type=ret,parameters=parameters,annotations=[],all_annotations=[],raw=raw,line=lines[id(node)],extras=extra))
 return dict(records=records,excluded_history_or_future=len(excluded),excluded=excluded,retained_inactive_or_unknown=sum(r['extras']['active_at_target'] is not True for r in records),inventory_view='all-declared' if preserve_history else 'target-current',sdk_definitions=[dict(n.attrib) for n in root if n.tag=='sdk'],issues=issues,counts_by_kind=dict(collections.Counter(r['kind'] for r in records)),target_platform_api=target,index_format_version=root.get('version'),line_locator_semantics='physical XML line from Expat parser')
