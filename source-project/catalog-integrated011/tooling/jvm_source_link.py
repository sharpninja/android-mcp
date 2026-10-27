"""Exact link only when a source type has a proven current SDK JVM spelling.
No wildcard-import guesses or type-variable bound erasure are performed.
Generics' outer types may be erased only when that outer type is known.
"""
PRIMITIVES={'byte':'B','char':'C','double':'D','float':'F','int':'I','long':'J','short':'S','boolean':'Z','void':'V'}
def encode(value,owner,owners):
 if not value:return None
 value=value.replace('...','[]').strip();dims=0
 while value.endswith('[]'):dims+=1;value=value[:-2].strip()
 if value in PRIMITIVES:
  if value=='void' and dims:return None
  return '['*dims+PRIMITIVES[value]
 # Strip balanced parameterized arguments, retaining the outer known type.
 depth=0;outer=[]
 for c in value:
  if c=='<':depth+=1
  elif c=='>':
   depth-=1
   if depth<0:return None
  elif depth==0:outer.append(c)
 if depth:return None
 value=''.join(outer).strip()
 candidate=owners.get(value)
 # Same-package/nested candidates are not assumed in the presence of unresolved
 # imports/type-variable shadowing; only explicitly qualified names link here.
 return '['*dims+'L'+candidate+';' if candidate else None
def key(r,owners):
 owner=r['extras'].get('owner_binary_name')
 if not owner or owners.get(r['owner'])!=owner:return None
 if r['kind'] in ('class','interface','enum','annotation'):return owner
 # The lifecycle index lacks a field descriptor/type. A same-name field is
 # retained as source evidence but cannot prove a typed JVM identity.
 if r['kind']=='field':return None
 if r['kind'] not in ('method','constructor'):return None
 args=[encode(p['type'],r['owner'],owners) for p in r['parameters']]
 ret='V' if r['kind']=='constructor' else encode(r.get('return_type'),r['owner'],owners)
 if ret is None or any(a is None or a=='V' for a in args):return None
 return owner+'#'+('<init>' if r['kind']=='constructor' else r['name'])+'('+''.join(args)+')'+ret
