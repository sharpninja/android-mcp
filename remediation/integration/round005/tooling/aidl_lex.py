"""AIDL statement delimiters: angle shifts/comparisons do not open nesting."""
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
