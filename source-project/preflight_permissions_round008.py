"""Offline structural permission-AST preflight before the expensive SQLite build."""
import pathlib,json,sys,collections,zipfile,xml.etree.ElementTree as ET,datetime
R=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(R/'catalog-round008v2/tooling'))
from build_catalog import cached_records
from parsers import signature,aidl_all,permission
from aidl_members import enrich
import jsonschema
I=R/sys.argv[1];sources=json.loads(I.read_text())['sources'];validator=jsonschema.Draft202012Validator(json.loads((R/'catalog-round008v2/contracts/PermissionExpression.schema.json').read_text()))
counts=collections.Counter();failures=[];unsupported=[]
def check(anns,s,line,scope):
 ast=permission(anns,aidl=s['kind']=='aidl');counts['expressions']+=1;counts['op:'+ast['op']]+=1
 for error in validator.iter_errors(ast):failures.append(dict(path=s['path'],repository=s['repository'],line=line,scope=scope,ast=ast,error=error.message,annotations=anns))
for i,s in enumerate(sources):
 if s['status']!='downloaded':continue
 kind=s['kind'];path=R/s['local_path'];rows=[]
 if kind in ('sdk-signature','source-signature'):rows=signature(path.read_text())
 elif kind in ('java-source','sdk-api-index'):rows=cached_records(s,R)
 elif kind=='aidl':
  try:
   text=path.read_text();rows=[]
   for item in aidl_all(text):
    span=item.get('declaration_span');fragment=text
    if span:fragment=''.join('\n' if c=='\n' else ' ' for c in text[:span[0]])+text[span[0]:span[1]]
    parsed=enrich(fragment,item);counts['declarations']+=1;counts['aidl_declaration_roots']+=1;counts['aidl_status:'+parsed['parse_status']]+=1;check(parsed['annotations'],s,parsed.get('declaration_line',1),'AIDL declaration');rows.extend(parsed['members'])
  except ValueError as e:unsupported.append(dict(path=s['path'],reason=str(e)));continue
 elif kind=='sdk-annotations':
  with zipfile.ZipFile(path) as z:
   for member in z.namelist():
    if not member.endswith('.xml'):continue
    try:xml=ET.fromstring(z.read(member))
    except ET.ParseError as e:unsupported.append(dict(path=s['path'],member=member,reason=str(e)));continue
    for ordinal,item in enumerate(xml.findall('item'),1):
     anns=[dict(name=node.get('name',''),arguments=','.join(v.get('name','')+'='+v.get('val','') for v in node.findall('val')),raw=ET.tostring(node,encoding='unicode')) for node in item.findall('annotation')];check(anns,s,ordinal,member+':'+item.get('name',''))
 for row in rows:
  counts['declarations']+=1;check(row['annotations'],s,row['line'],'declaration')
  for ordinal,p in enumerate(row['parameters']):check(p['annotations'],s,row['line'],'parameter:'+str(ordinal))
 if i%3000==0:print('preflight paths',i,'/',len(sources),'failures',len(failures),flush=True)
report=dict(completed_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),inventory=str(I.relative_to(R)),source_paths=len(sources),counts=dict(counts),schema_failures=failures,raw_preserved_unsupported=unsupported)
(R/sys.argv[2]).write_text(json.dumps(report,indent=2));print(json.dumps(dict(counts=dict(counts),schema_failures=len(failures),unsupported_sources=len(unsupported)),indent=2))
if failures:raise SystemExit(1)
