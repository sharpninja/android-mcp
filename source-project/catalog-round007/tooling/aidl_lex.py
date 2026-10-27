"""AIDL statement delimiters: angle shifts/comparisons do not open nesting."""
import re
def code_mask(text):
    """Mask comments/strings with equal-length blanks, preserving line offsets."""
    pattern=r'"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'|/\*.*?\*/|//[^\n]*'
    return re.sub(pattern,lambda m:''.join('\n' if c=='\n' else ' ' for c in m.group()),text,flags=re.S)

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
