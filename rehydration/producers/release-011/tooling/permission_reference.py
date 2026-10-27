"""Functional facts from the complete official Manifest.permission reference.
Documentation is not a compiled manifest definition and grants no OS authority.
All named constant sections are retained, including missing/ambiguous values.
"""
from html.parser import HTMLParser
import re,json,collections
class Fields(HTMLParser):
 def __init__(self):
  super().__init__(convert_charrefs=True);self.depth=0;self.active=[];self.rows=[];self.other=[];self.signature=False;self.script=0
 def append(self,text,signature=False):
  for item in self.active:
   item['text'].append(text)
   if signature:item['signature'].append(text)
 def handle_starttag(self,tag,attributes):
  attrs=dict(attributes)
  if tag in ('script','style'):self.script+=1
  if self.script:return
  if tag=='div':
   self.depth+=1
   if 'data-version-added' in attrs:self.active.append(dict(depth=self.depth,attrs=attrs,line=self.getpos()[0],names=[],text=[],signature=[]))
  if tag in ('p','div','br','h3','pre','li'):self.append('\n')
  if tag=='h3' and 'api-name' in attrs.get('class','').split():
   for item in self.active:item['names'].append(attrs.get('id'))
  if tag=='pre' and 'api-signature' in attrs.get('class','').split():self.signature=True
 def handle_data(self,text):
  if not self.script:self.append(text,self.signature)
 def handle_endtag(self,tag):
  if tag in ('script','style'):
   self.script=max(0,self.script-1);return
  if self.script:return
  if tag=='pre':self.signature=False
  if tag in ('p','div','br','h3','pre','li'):self.append('\n')
  if tag=='div':
   completed=[item for item in self.active if item['depth']==self.depth]
   self.active=[item for item in self.active if item['depth']!=self.depth]
   for item in completed:self.finish(item)
   self.depth=max(0,self.depth-1)
 def finish(self,item):
  if len(item['names'])!=1:return
  name=item['names'][0];signature=' '.join(''.join(item['signature']).split())
  if not name or not re.search(r'\bpublic\s+static\s+final\s+(?:java\.lang\.)?String\s+'+re.escape(name)+r'\b',signature):
   self.other.append(dict(name=name,line=item['line'],signature=signature,reason='Not a recognized documented String constant section'));return
  text=''.join(item['text']);values=re.findall(r'Constant\s+Value\s*:\s*("(?:[^"\\]|\\.)*")',text);value=None;unknown=None
  if len(values)==1:
   try:value=json.loads(values[0])
   except ValueError:unknown='Documented literal is not a recognized string encoding'
  else:unknown='Missing or ambiguous documented constant literal'
  match=re.search(r'Protection\s+level\s*:\s*([^\r\n]+)',text,re.I)
  self.rows.append(dict(name=name,symbol='android.Manifest.permission.'+name,value=value,line=item['line'],introduced_raw=item['attrs'].get('data-version-added'),deprecated_raw=item['attrs'].get('data-version-deprecated'),removed_raw=item['attrs'].get('data-version-removed'),protection_level_raw=match.group(1).strip() if match else None,signature=signature,value_status='documented-constant' if value is not None else 'unknown',unknown_reason=unknown,authority='Documentation fact only; not a compiled definition, OS grant or MCP client permission'))
def parse(data):
 text=data.decode('utf-8') if isinstance(data,bytes) else data;parser=Fields();parser.feed(text);parser.close();counts=collections.Counter(row['name'] for row in parser.rows)
 return dict(fields=parser.rows,duplicate_field_names=sorted(name for name,count in counts.items() if count>1),nonconstant_or_unsupported_sections=parser.other,unclosed_version_sections=len(parser.active),known_values=sum(row['value'] is not None for row in parser.rows),unknown_values=sum(row['value'] is None for row in parser.rows),source_semantics='Unversioned official API reference snapshot; raw introduced/deprecated/removed and protection metadata preserved separately')
