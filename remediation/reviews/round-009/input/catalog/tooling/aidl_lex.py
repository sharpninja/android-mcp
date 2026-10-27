"""AIDL statement delimiters: angle shifts/comparisons do not open nesting."""
import re
def code_mask(text):
    """Mask comments/strings with equal-length blanks, preserving line offsets."""
    pattern=r'"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'|/\*.*?\*/|//[^\n]*'
    return re.sub(pattern,lambda m:''.join('\n' if c=='\n' else ' ' for c in m.group()),text,flags=re.S)

def comment_mask(text):
    """Remove comments as equal-length spaces; quoted data stays literal data."""
    pattern=r'"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'|/\*.*?\*/|//[^\n]*'
    return re.sub(pattern,lambda m:m.group() if m.group().startswith(('"',"'")) else ''.join('\n' if c=='\n' else ' ' for c in m.group()),text,flags=re.S)

def declaration_spans(text):
    masked=code_mask(text);matches={m.start():m for m in re.finditer(r'\b(interface|parcelable|union|enum)\s+([\w]+(?:\.[\w]+)*)',masked)}
    braces=0;parens=0;boundary=0;active=None;result=[]
    for i,c in enumerate(masked):
        if i in matches and braces==0 and parens==0:
            if active is not None:result.append((active,i))
            active=boundary
        if c=='(':parens+=1
        elif c==')':parens=max(0,parens-1)
        elif c=='{':braces+=1
        elif c=='}':
            braces=max(0,braces-1)
            if braces==0 and parens==0:
                if active is not None:result.append((active,i+1));active=None
                boundary=i+1
        elif c==';' and braces==0 and parens==0:
            if active is not None:result.append((active,i+1));active=None
            boundary=i+1
    if active is not None:result.append((active,len(text)))
    return result

def chunks(text,separator):
    start=0;stack=[];quote=None;escape=False
    for i,c in enumerate(text):
        if quote:
            if escape:escape=False
            elif c=='\\':escape=True
            elif c==quote:quote=None
        elif c in ('"',"'"):quote=c
        elif c in '([{':stack.append(c)
        elif c in ')]}':
            if stack:stack.pop()
        elif c==separator and not stack:
            raw=text[start:i].strip()
            if raw:yield raw,start
            start=i+1
    raw=text[start:].strip()
    if raw:yield raw,start

def mask_spans(text,spans):
    chars=list(text)
    for start,end in spans:
        chars[start:end]=['\n' if c=='\n' else ' ' for c in text[start:end]]
    return ''.join(chars)

def body_range(text,after=0):
    masked=code_mask(text);start=masked.find('{',after)
    if start<0:return None
    depth=0
    for i,c in enumerate(masked[start:],start):
        if c=='{':depth+=1
        elif c=='}':
            depth-=1
            if depth==0:return start,i
    raise ValueError('Unbalanced AIDL declaration body')

def inventory_counts(text):
    """Independent lexical no-drop counts; no type or wire interpretation."""
    counts=dict(declarations=0,statements=0,reconciled=True,issues=[])
    def visit(fragment):
        for start,end in declaration_spans(fragment):
            raw=fragment[start:end];header=re.search(r'\b(interface|parcelable|union|enum)\s+[\w.]+',code_mask(raw))
            if not header:continue
            counts['declarations']+=1
            try:bounds=body_range(raw,header.end())
            except ValueError as error:
                counts['reconciled']=False;counts['issues'].append(str(error));continue
            if bounds is None:continue
            a,b=bounds;body=comment_mask(raw[a+1:b]);nested=declaration_spans(body)
            remaining=mask_spans(body,nested)
            counts['statements']+=sum(1 for _ in chunks(remaining,',' if header.group(1)=='enum' else ';'))
            visit(body)
    visit(text)
    counts['expected_records']=counts['declarations']+counts['statements'] if counts['declarations'] else 1
    return counts
