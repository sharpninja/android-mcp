#!/usr/bin/env python3
"""Legacy permission-expression helpers only; no importer or executable entry point.

Input: JSON inventory with sources [{path,url,revision,retrieved_at,sha256,
api_level,channel,kind,license}]. Paths relative to the inventory. Kinds:
metalava, manifest, aidl, evidence. Every source must be hash pinned.
Only use a separately verified, exhaustively enumerated stable source inventory.
"""
import argparse, hashlib, json, pathlib, re, sqlite3

def digest(b): return hashlib.sha256(b).hexdigest()

def split_top(text, sep=','):
    result=[]; start=0; stack=[]; quote=None; escape=False
    for i,c in enumerate(text):
        if quote:
            if escape: escape=False
            elif c=='\\': escape=True
            elif c==quote: quote=None
        elif c in '\"\'': quote=c
        elif c in '(<[{': stack.append(c)
        elif c in ')>]}':
            if stack: stack.pop()
        elif c==sep and not stack: result.append(text[start:i].strip());start=i+1
    result.append(text[start:].strip())
    return result

def annotations(text):
    """Balanced annotation scanner, retaining exact expressions and nested annotations."""
    found=[]; clean=[]; pos=0
    while pos<len(text):
        m=re.search(r'@[\w.$]+',text[pos:])
        if not m: clean.append(text[pos:]);break
        a=pos+m.start(); b=pos+m.end();clean.append(text[pos:a]); name=text[a+1:b]
        end=b
        if b<len(text) and text[b]=='(':
            depth=0; quote=None;esc=False
            for end in range(b,len(text)):
                c=text[end]
                if quote:
                    if esc: esc=False
                    elif c=='\\': esc=True
                    elif c==quote: quote=None
                elif c in '\"\'':quote=c
                elif c=='(':depth+=1
                elif c==')':
                    depth-=1
                    if depth==0: end+=1;break
            else: raise ValueError('unclosed annotation')
        found.append((name,text[b+1:end-1] if end>b else '',text[a:end]));pos=end
    return ''.join(clean),found

def permission_ast(name,args):
    short=name.split('.')[-1]
    if short=='RequiresNoPermission':return {'op':'explicit-none'}
    if short=='PermissionManuallyEnforced':return {'op':'manual','reason':'Manual enforcement declared; backend checks unknown'}
    if short in ('Read','Write'):
        _,nested=annotations(args)
        child=permission_ast(nested[0][0],nested[0][1]) if len(nested)==1 else {'op':'unknown','reason':'Unresolved nested annotation','raw':args}
        return {'op':'access','access':short.lower(),'requirement':child}
    pairs={}
    for item in split_top(args):
        if '=' in item:k,v=item.split('=',1);pairs[k.strip()]=v.strip()
        elif item:pairs['value']=item
    def leaf(value):
        if re.fullmatch(r'"[^"\\]+"',value):return {'op':'permission','name':json.loads(value)}
        return {'op':'unknown','reason':'Unresolved symbolic permission expression','raw':value}
    clauses=[]
    for k in ('value','allOf','anyOf'):
        if k not in pairs:continue
        v=pairs[k]
        clauses.append(leaf(v) if k=='value' else {'op':'all' if k=='allOf' else 'any','children':[leaf(x) for x in split_top(v.strip('{}')) if x]})
    ast=clauses[0] if len(clauses)==1 else {'op':'all','children':clauses} if clauses else {'op':'unknown','reason':'Permission expression absent or unsupported','raw':args}
    if pairs.get('conditional')=='true':ast={'op':'conditional','condition':{'op':'unknown','reason':'Annotation marks conditional but does not identify runtime predicate'},'requirement':ast}
    unknown=set(pairs)-{'value','allOf','anyOf','conditional'}
    if unknown:ast={'op':'all','children':[ast,{'op':'unknown','reason':'Unrecognized annotation attributes','raw':args}]}
    return ast

