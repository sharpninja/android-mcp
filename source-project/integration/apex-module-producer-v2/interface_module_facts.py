"""Static Soong interface-module facts. Never runs build files or resolves products.
Expressions/filegroups/defaults remain unknown; build-module names are not OS module IDs.
"""
import pathlib,re,json,hashlib,datetime,collections
def mask_comments(text):
 return re.sub(r'"(?:\\.|[^"\\])*"|/\*.*?\*/|//[^\n]*',lambda m:m.group() if m.group().startswith('"') else ''.join('\n' if c=='\n' else ' ' for c in m.group()),text,flags=re.S)
def tokens(text):return list(re.finditer(r'"(?:\\.|[^"\\])*"|[A-Za-z_]\w*|\d+|[^\s]',mask_comments(text)))
def value(ts,i,text,expression=True):
 if i>=len(ts):return dict(kind='missing-value',raw=''),i
 start=i;token=ts[i].group()
 if token in ('[','{'):
  closing=']' if token=='[' else '}';i+=1;children=[];complete=True
  while i<len(ts) and ts[i].group()!=closing:
   if token=='{':
    if i+1>=len(ts) or ts[i+1].group()!=':':complete=False;break
    key=ts[i].group();i+=2;child,i=value(ts,i,text);children.append(dict(name=key,value=child))
   else:child,i=value(ts,i,text);children.append(child)
   if i<len(ts) and ts[i].group()==',':i+=1
   elif i<len(ts) and ts[i].group()!=closing:complete=False;break
  if i>=len(ts) or ts[i].group()!=closing:complete=False
  else:i+=1
  node=dict(kind='object' if token=='{' else 'array',items=children,complete=complete)
 elif token.startswith('"'):
  try:node=dict(kind='literal',value=json.loads(token))
  except ValueError:node=dict(kind='unknown-string',raw=token)
  i+=1
 elif token in ('true','false'):node=dict(kind='literal',value=token=='true');i+=1
 elif token.isdecimal():node=dict(kind='literal',value=int(token));i+=1
 else:node=dict(kind='unresolved-expression');i+=1
 # A value followed by an operator is an expression, never its literal prefix.
 if expression and i<len(ts) and ts[i].group() not in (',','}',']'):
  depth=0
  while i<len(ts):
   t=ts[i].group()
   if depth==0 and t in (',','}',']'):break
   if t in ('(','[','{'):depth+=1
   elif t in (')',']','}'):depth=max(0,depth-1)
   i+=1
  node=dict(kind='unresolved-expression')
 node['raw']=text[ts[start].start():ts[i-1].end()] if i>start else '';return node,i
def literal(node,name=None):
 if name is not None:
  matches=[v['value'] for v in node.get('items',[]) if v['name']==name]
  return literal(matches[0]) if len(matches)==1 else None
 if node['kind']=='literal':return node['value']
 if node['kind']=='array' and node['complete']:
  values=[literal(v) for v in node['items']];return values if all(v is not None for v in values) else None
 if node['kind']=='object' and node['complete']:
  names=[v['name'] for v in node['items']]
  if len(names)!=len(set(names)):return None
  values={v['name']:literal(v['value']) for v in node['items']};return values if all(v is not None for v in values.values()) else None
 return None
def modules(text):
 ts=tokens(text);result=[];i=0
 while i<len(ts)-1:
  if ts[i].group() not in ('aidl_interface','hidl_interface') or ts[i+1].group()!='{':i+=1;continue
  start=i;kind=ts[i].group();properties,end=value(ts,i+1,text,False);result.append(dict(kind=kind,line=text.count('\n',0,ts[start].start())+1,properties=properties,raw=text[ts[start].start():ts[end-1].end()],status='static-properties-parsed' if properties.get('complete') else 'unclosed-module-block',runtime_module_identity_proven=False,android_applicability_proven=False));i=max(end,i+1)
 return result
def source_matches(build_path,patterns,excluded,paths):
 parent=pathlib.PurePosixPath(build_path).parent
 for p in patterns+excluded:
  pure=pathlib.PurePosixPath(p)
  if pure.is_absolute() or '..' in pure.parts or p.startswith(':'):raise ValueError('Nonliteral, external or escaping source selection requires separate proof')
 result=[]
 for path in paths:
  try:relative=pathlib.PurePosixPath(path).relative_to(parent)
  except ValueError:continue
  if any(relative.full_match(p) for p in patterns) and not any(relative.full_match(p) for p in excluded):result.append(path)
 return sorted(set(result))
def main(root):
 root=pathlib.Path(root);inventory=json.loads((root/'outside-corpus-audit/source-inventory-batchinterface-metadata-v1-acquired002.json').read_text());declared=json.loads((root/'source-inventory-release011.json').read_text())['sources'];by_repo=collections.defaultdict(list)
 for source in declared:
  if source['kind']=='aidl' or source['path'].endswith('.hal'):by_repo[(source['repository'],source['revision'])].append(source['path'])
 output=root/'outside-corpus-audit/interface-module-facts-v1';output.mkdir(exist_ok=False);facts=[];counts=collections.Counter();unknown=[]
 for source in inventory['sources']:
  if 'nearest selected-interface build ancestor' not in source['selection_reasons']:continue
  if not source['path'].endswith('.bp'):counts['non-soong-build-files-retained']+=1;continue
  raw=(root/source['local_path']).read_bytes();assert hashlib.sha256(raw).hexdigest()==source['sha256'];counts['soong-files-parsed']+=1
  for module in modules(raw.decode('utf-8')):
   module['source']={k:source[k] for k in ('repository','revision','path','sha256')};properties=module['properties'];module['literal_properties']={name:literal(properties,name) for name in ('name','srcs','exclude_srcs','versions','versions_with_info','imports','frozen','unstable','stability','apex_available','vendor_available','owner')};patterns=literal(properties,'srcs');excluded=literal(properties,'exclude_srcs');module['source_linkage_status']='unresolved source expression/filegroup/defaults'
   if isinstance(patterns,list) and all(isinstance(v,str) for v in patterns) and (excluded is None and not any(v['name']=='exclude_srcs' for v in properties.get('items',[])) or isinstance(excluded,list) and all(isinstance(v,str) for v in excluded)):
    try:module['matched_declared_paths']=source_matches(source['path'],patterns,excluded or [],by_repo[(source['repository'],source['revision'])]);module['source_linkage_status']='literal-source-selectors-matched'
    except ValueError as error:module['source_linkage_reason']=str(error)
   counts[module['kind']]+=1;counts['matched-path-links']+=len(module.get('matched_declared_paths',[]));counts['modules-with-unresolved-selection']+=module['source_linkage_status']!='literal-source-selectors-matched';facts.append(module)
 with (output/'facts.jsonl').open('w') as stream:
  for module in facts:stream.write(json.dumps(module,sort_keys=True)+'\n')
 report=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),counts=dict(counts),modules=len(facts),code_sha256=hashlib.sha256(pathlib.Path(__file__).read_bytes()).hexdigest(),build_files_executed=False,runtime_module_or_os_access_inferred=False,complete=False,limitations=['Literal properties are source facts, not product selection or observed installed module/version','Expressions, filegroups, defaults, imports and exclusions lacking literal proof remain unresolved','Declared build module versions, AIDL frozen dump versions and Android/API/APEX versions remain separate'])
 (output/'report.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
if __name__=='__main__':main(pathlib.Path(__file__).resolve().parent)
