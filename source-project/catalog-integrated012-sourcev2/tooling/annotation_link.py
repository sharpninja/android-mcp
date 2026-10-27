"""Conservative annotation spelling fallback: a type variable is not java.lang."""
import re
from parsers import annotations,split_top,typ
def xml_jvm_key(item_name,owners):
 """Exact descriptor spelling from explicit XML types and known SDK owners.
 Generic outer-type erasure is allowed only through encode's known-class proof.
 Unknown type variables, malformed generics and guessed java.lang names fail.
 """
 from jvm_source_link import encode
 target=re.sub(r'\s+\d+$','',item_name);owner,separator,tail=target.partition(' ')
 if not separator or owner not in owners or '(' not in tail:return None
 start=tail.find('(');end=tail.rfind(')')
 if end<start or tail[end+1:].strip():return None
 match=re.search(r'([\w$]+)$',tail[:start])
 if not match:return None
 name=match.group(1);result=tail[:match.start()].strip();constructor=not result and name==owner.rsplit('.',1)[-1]
 if not result and not constructor:return None
 parameters=[encode(typ(value),owner,owners) for value in split_top(tail[start+1:end]) if value.strip()]
 returned='V' if constructor else encode(typ(result),owner,owners)
 if returned is None or any(value is None or value=='V' for value in parameters):return None
 return owners[owner]+'#'+('<init>' if constructor else name)+'('+''.join(parameters)+')'+returned
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
def same_snapshot_source_ids(db,bid,annotation_source,source_by_id):
 # An explicit platform version plus the same repository commit proves the
 # legacy signature and annotation archives belong to one source snapshot.
 if not all(annotation_source.get(k) for k in ('repository','revision','platform_api_version')):return set()
 return {source_id for source_id, in db.execute('SELECT source_id FROM binding_declarations WHERE binding_id=?',(bid,)) if source_by_id[source_id]['kind']=='sdk-signature' and all(source_by_id[source_id].get(k)==annotation_source[k] for k in ('repository','revision','platform_api_version'))}
def snapshot_declared_type_matches(db,bid,item_name,eligible_source_ids,sha):
 import json
 target=re.sub(r'\s+\d+$','',item_name);owner,sep,tail=target.partition(' ')
 if not sep:return True # type/package target; it declares no return/field type
 if '(' in tail:
  head=tail.split('(',1)[0];name=re.search(r'[\w$]+$',head)
  if not name:return False
  expected=typ(head[:name.start()]) or None
 else:
  head=tail.rsplit(' ',1)
  if len(head)!=2:return False
  expected=typ(head[0])
 checked=[]
 for source_id,raw in db.execute('SELECT source_id,document_json FROM binding_declarations WHERE binding_id=?',(bid,)):
  if source_id not in eligible_source_ids:continue
  doc=json.loads(raw);actual=doc.get('return_type',{}).get('text')
  if expected is None:checked.append(doc['kind']=='constructor' and actual is None);continue
  if actual==expected:checked.append(True);continue
  shortened=re.sub(r'(?<![\w.$])java\.lang\.','',expected)
  if actual!=shortened:checked.append(False);continue
  header=db.execute('SELECT document_json FROM binding_declarations WHERE binding_id=? AND source_id=?',(sha('java:'+doc.get('owner','')),source_id)).fetchone()
  if not header:checked.append(False);continue
  roots=set(re.findall(r'(?<![\w.$])java\.lang\.([A-Za-z_$][\w$]*)',expected));formals=formal_names(doc['provenance'][0]['raw_signature'])|formal_names(json.loads(header[0])['provenance'][0]['raw_signature']);checked.append(not bool(roots&formals))
 return bool(checked) and all(checked)
def fallback_supported(db,bid,xml_canonical,source_by_id,sha,eligible_source_ids=None):
 # The SDK's signature format supplies the abbreviation evidence. Do not
 # infer a java.lang class from an identically spelled formal type variable.
 roots=set(re.findall(r'(?<![\w.$])java\.lang\.([A-Za-z_$][\w$]*)',xml_canonical.split('(',1)[-1]));evidence=[]
 for source_id,document in db.execute('SELECT source_id,document_json FROM binding_declarations WHERE binding_id=?',(bid,)):
  if eligible_source_ids is not None and source_id not in eligible_source_ids:continue
  if source_by_id[source_id]['kind']!='sdk-signature':continue
  import json
  doc=json.loads(document);owner=doc.get('owner');header=db.execute('SELECT document_json FROM binding_declarations WHERE binding_id=? AND source_id=?',(sha('java:'+owner),source_id)).fetchone() if owner else None
  if not header:return False
  method_raw=doc['provenance'][0]['raw_signature'];owner_raw=json.loads(header[0])['provenance'][0]['raw_signature']
  evidence.append(not (roots & (formal_names(method_raw)|formal_names(owner_raw))))
 return bool(evidence) and all(evidence)
