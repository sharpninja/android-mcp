"""Conservative Java permission static-import proof over validated cache records.
No cache producer change: this enriches a permission AST after declaration parse.
Wildcard imports, inherited/enclosing shadows and quoted short literals stay
unknown. Callers must supply unique symbols from the exact source snapshot.
"""
import re,copy
from parsers import without_comments
TYPE_KINDS={'class','interface','enum','annotation'}
class Scope:
 def __init__(self,data,records):
  records=list(records);self.types={r['owner']:r for r in records if r['kind'] in TYPE_KINDS};self.fields={};self.imports={}
  for r in records:
   if r['kind']=='field':self.fields.setdefault(r['owner'],set()).add(r['name'])
  starts=[r['extras'].get('source_byte_range',[None])[0] for r in self.types.values()]
  starts=[v for v in starts if isinstance(v,int) and not isinstance(v,bool)]
  if not starts:return
  prefix=without_comments(data[:min(starts)].decode('utf-8'))
  for match in re.finditer(r'^\s*import\s+static\s+([A-Za-z_$][\w$]*(?:\.[A-Za-z_$][\w$]*)+)\s*;',prefix,re.M):
   full=match.group(1);self.imports.setdefault(full.rsplit('.',1)[1],set()).add(full)
 def aliases(self,owner,snapshot_symbols):
  enclosing=sorted([key for key in self.types if owner==key or owner.startswith(key+'.')],key=len,reverse=True)
  if not enclosing or enclosing[0]!=owner:return {}
  for key in enclosing:
   record=self.types[key]
   if record['kind']!='class' or re.search(r'\b(?:extends|implements)\b',record.get('raw','')):return {}
  shadows=set().union(*(self.fields.get(key,set()) for key in enclosing));result={}
  for simple,fulls in self.imports.items():
   if simple in shadows or len(fulls)!=1:continue
   full=next(iter(fulls))
   if full in snapshot_symbols:result[simple]=dict(value=snapshot_symbols[full],qualified_symbol=full,proof='Exact static import; complete current/enclosing declared-field inventory; no inherited type scope; unique same-snapshot permission constant')
  return result
def resolve(ast,aliases):
 def visit(value):
  if isinstance(value,dict):
   if value.get('op')=='unknown' and value.get('reason')=='Unresolved symbolic permission expression' and value.get('raw') in aliases:
    key=value['raw'];proof=aliases[key];return dict(op='permission',name=proof['value'],**{'x-symbol':key,'x-static-import':proof['qualified_symbol'],'x-source-alias-proof':proof['proof']})
   return {k:visit(v) for k,v in value.items()}
  if isinstance(value,list):return [visit(v) for v in value]
  return copy.deepcopy(value)
 return visit(ast)
