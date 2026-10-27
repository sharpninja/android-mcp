"""Five known Binder declaration-family token forms, never compiled wire proof."""
import re,json
CPP=('IMPLEMENT_META_INTERFACE','IMPLEMENT_HYBRID_META_INTERFACE','IMPLEMENT_META_NESTED_INTERFACE','AIBinder_Class_define')
def lex(text,language):
 rows=[];issues=[];i=0;length=len(text)
 while i<length:
  start=i
  if text.startswith('//',i):
   end=text.find('\n',i)
   if end<0:end=length
   while language=='cpp' and end<length and text[i:end].rstrip('\r').endswith('\\'):
    next_end=text.find('\n',end+1);end=next_end if next_end>=0 else length
   rows.append((start,end,'comment'));i=end;continue
  if text.startswith('/*',i):
   depth=1;i+=2
   while i<length and depth:
    if language=='rust' and text.startswith('/*',i):depth+=1;i+=2
    elif text.startswith('*/',i):depth-=1;i+=2
    else:i+=1
   if depth:issues.append(dict(position=start,reason='Unclosed block comment'))
   rows.append((start,i,'comment'));continue
  raw=re.match(r'(?:b|c)?r(#{0,255})"',text[i:]) if language=='rust' else re.match(r'(?:u8|u|U|L)?R"([^ ()\t\r\n\\]{0,16})\(',text[i:])
  if raw:
   closing='"'+raw.group(1) if language=='rust' else ')'+raw.group(1)+'"';end=text.find(closing,i+raw.end())
   if end<0:issues.append(dict(position=start,reason='Unclosed raw literal'));end=length
   else:end+=len(closing)
   rows.append((start,end,'literal'));i=end;continue
  if text[i]=='"':
   i+=1;closed=False
   while i<length:
    if text[i]=='\\':i+=2
    elif text[i]=='"':i+=1;closed=True;break
    else:i+=1
   if not closed:issues.append(dict(position=start,reason='Unclosed string literal'))
   rows.append((start,min(i,length),'literal'));continue
  if text[i]=="'":
   match=re.match(r"'(?:\\(?:u\{[0-9a-fA-F]+\}|x[0-9a-fA-F]{2}|.)|[^'\\\r\n])'",text[i:]) if language=='rust' else re.match(r"'(?:\\.|[^'\\\r\n])*'",text[i:])
   if match:i+=match.end();rows.append((start,i,'literal'));continue
  i+=1
 return rows,issues
def mask(text,spans,literals=True):
 chars=list(text)
 for a,b,kind in spans:
  if kind=='comment' or literals:chars[a:b]=['\n' if c=='\n' else ' ' for c in text[a:b]]
 return ''.join(chars)
def balanced(code,start):
 pairs={'(':')','[':']','{':'}'};stack=[]
 for i in range(start,len(code)):
  c=code[i]
  if c in pairs:stack.append(pairs[c])
  elif c in (')',']','}'):
   if not stack or c!=stack.pop():return None
   if not stack:return i
 return None
def literal(raw,language):
 value=raw.strip()
 raw_match=re.fullmatch(r'(?:b|c)?r(#{0,255})"([\s\S]*)"\1',value) if language=='rust' else re.fullmatch(r'(?:u8|u|U|L)?R"([^ ()\t\r\n\\]{0,16})\(([\s\S]*)\)\1"',value)
 if raw_match:return raw_match.group(2)
 match=re.fullmatch(r'(?:u8|u|U|L|b|c)?("(?:\\.|[^"\\])*")',value,re.S)
 if match:
  try:return json.loads(match.group(1))
  except ValueError:return None
 return None
def parse(text,language):
 if language not in ('cpp','rust'):raise ValueError('Explicit source language required')
 spans,issues=lex(text,language);code=mask(text,spans);comments_only=mask(text,spans,False);facts=[]
 pattern=r'\b(declare_binder_interface)\s*!\s*([({])' if language=='rust' else r'\b('+'|'.join(CPP)+r')\s*(\()'
 for match in re.finditer(pattern,code):
  family=match.group(1);start=match.end()-1;line_begin=code.rfind('\n',0,match.start())+1
  if language=='cpp' and re.search(r'#\s*define\s*$',code[line_begin:match.start()]):continue
  end=balanced(code,start)
  if end is None:
   facts.append(dict(family=family,line=text.count('\n',0,match.start())+1,status='unclosed-token-form',raw=text[match.start():],descriptor_literal=None,compiled_endpoint_proven=False));continue
  symbol=None;expression=None
  if language=='rust':
   prefix=re.match(r'\s*((?:r#)?[A-Za-z_]\w*)\s*\[',code[start+1:end])
   if prefix:
    opening=start+prefix.end();closing=balanced(code,opening)
    if closing is not None and closing<end:symbol=prefix.group(1);expression=comments_only[opening+1:closing].strip()
  else:
   args=[];begin=start+1;stack=[]
   for i in range(start+1,end):
    c=code[i]
    if c in '([{':stack.append(c)
    elif c in ')]}':
     if stack:stack.pop()
    elif c==',' and not stack:args.append(comments_only[begin:i].strip());begin=i+1
   args.append(comments_only[begin:end].strip());expression=args[0] if family=='AIBinder_Class_define' else args[-1]
   if family!='AIBinder_Class_define':symbol=args[0]
  value=literal(expression,language) if expression is not None else None
  facts.append(dict(family=family,line=text.count('\n',0,match.start())+1,character_span=[match.start(),end+1],declared_symbol_token=symbol,descriptor_expression=expression,descriptor_literal=value,status='literal-argument-candidate' if value is not None else 'computed-or-unresolved-argument',raw=text[match.start():end+1],compiled_endpoint_proven=False,wire_transaction_schema=None,preprocessor_or_build_conditions='unresolved',runtime_registration='unknown'))
 return dict(facts=facts,lexical_issues=issues,compiled_or_runtime_proven=False)
