"""Conservative annotation spelling fallback: a type variable is not java.lang."""
import re
from parsers import annotations,split_top
def formal_names(raw):
 clean=annotations(raw)[0];starts=[]
 type_match=re.search(r'\b(?:class|interface|enum)\s+[\w.$]+\s*(<)',clean)
 if type_match:starts.append(type_match.start(1))
 prefix=clean.split('(',1)[0]
 for match in re.finditer(r'(?<![\w.$])<',prefix):starts.append(match.start())
 names=set()
 for start in starts:
  depth=0;end=None
  for i,c in enumerate(clean[start:],start):
   if c=='<':depth+=1
   elif c=='>':
    depth-=1
    if depth==0:end=i;break
  if end is None:continue
  for part in split_top(clean[start+1:end]):
   m=re.match(r'\s*([A-Za-z_$][\w$]*)',part)
   if m:names.add(m.group(1))
 return names
def fallback_supported(db,bid,xml_canonical,source_by_id,sha):
 # The SDK's signature format supplies the abbreviation evidence. Do not
 # infer a java.lang class from an identically spelled formal type variable.
 roots=set(re.findall(r'(?<![\w.$])java\.lang\.([A-Za-z_$][\w$]*)',xml_canonical.split('(',1)[-1]));evidence=[]
 for source_id,document in db.execute('SELECT source_id,document_json FROM binding_declarations WHERE binding_id=?',(bid,)):
  if source_by_id[source_id]['kind']!='sdk-signature':continue
  import json
  doc=json.loads(document);owner=doc.get('owner');header=db.execute('SELECT document_json FROM binding_declarations WHERE binding_id=? AND source_id=?',(sha('java:'+owner),source_id)).fetchone() if owner else None
  if not header:return False
  method_raw=doc['provenance'][0]['raw_signature'];owner_raw=json.loads(header[0])['provenance'][0]['raw_signature']
  evidence.append(not (roots & (formal_names(method_raw)|formal_names(owner_raw))))
 return bool(evidence) and all(evidence)
