#!/usr/bin/env python3
"""Offline, conservative ingestion foundation. Does not fetch or claim complete coverage.

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

def permission_logic(anns):
    out=[]
    for name,args,raw in anns:
        short=name.split('.')[-1]
        if short not in ('RequiresPermission','Read','Write','EnforcePermission','PermissionManuallyEnforced','RequiresNoPermission'):continue
        if short in ('Read','Write') and 'RequiresPermission' not in name:continue
        if short=='PermissionManuallyEnforced':logic='manual'
        elif short=='RequiresNoPermission':logic='explicit-none'
        elif re.search(r'\bconditional\s*=\s*true',args):logic='conditional'
        elif re.search(r'\ballOf\s*=',args):logic='all'
        elif re.search(r'\banyOf\s*=',args):logic='any'
        else:logic='value'
        pairs={}
        for item in split_top(args):
            if '=' in item:
                k,v=item.split('=',1);pairs[k.strip()]=v.strip()
            elif item:pairs['value']=item
        out.append((name,logic,permission_ast(name,args),raw))
    return out or [(None,'unknown',{'op':'unknown','reason':'No recognized permission annotation in this declaration; absence is not permission-free.'},None)]

def type_name(argument):
    cleaned,_=annotations(argument)
    cleaned=re.sub(r'\b(final|optional)\s+','',cleaned).strip()
    # Metalava v2 normally omits parameter names. Strip only explicit trailing names.
    m=re.match(r'(.+)\s+([A-Za-z_$][\w$]*)$',cleaned)
    if m and not m.group(1).endswith(('extends','super')):cleaned=m.group(1)
    return re.sub(r'\s+',' ',cleaned)

def parse_signature(line,package,owner):
    clean,anns=annotations(line);clean=clean.strip().rstrip(';')
    kind=clean.split()[0]
    if kind not in ('ctor','method','field','enum_constant','property'):raise ValueError('unsupported signature declaration')
    body=clean[len(kind):].strip()
    if kind in ('ctor','method'):
        a=body.find('(');b=body.rfind(')')
        if a<0 or b<a:raise ValueError('unresolved parameter list')
        head=body[:a].split();name=head[-1];ret=None if kind=='ctor' else head[-2]
        args=[] if not body[a+1:b].strip() else [type_name(x) for x in split_top(body[a+1:b])]
        canonical=owner+'#'+('<init>' if kind=='ctor' else name)+'('+','.join(args)+')'
    else:
        head=body.split('=',1)[0].split();name=head[-1];ret=head[-2];args=[];canonical=owner+'#'+name
    return dict(signature=canonical,package=package,owner=owner,kind=kind,name=name,return_type=ret,arguments_json=json.dumps(args),raw_signature=line),anns

def run(inventory,out):
    inventory=pathlib.Path(inventory);spec=json.loads(inventory.read_text());out=pathlib.Path(out);out.mkdir(parents=True,exist_ok=True)
    dbpath=out/'catalog.sqlite'
    if dbpath.exists():raise ValueError('Refusing to overwrite existing catalog')
    db=sqlite3.connect(dbpath);db.executescript((pathlib.Path(__file__).resolve().parent.parent/'schema.sql').read_text())
    db.execute('INSERT INTO catalog_meta VALUES (?,?)',('coverage','INCOMPLETE: offline ingestion foundation; inspect issues and source inventory'))
    totals={'sources':0,'signature_declarations':0,'parsed_declarations':0,'retained_failures':0}
    def issue(s,n,reason,raw):
        totals['retained_failures']+=1;db.execute('INSERT INTO issues(source_id,line,category,reason,raw) VALUES (?,?,?,?,?)',(s,n,'unsupported',reason,raw))
    def binding(row,anns,s,n):
        bid=digest(row['signature'].encode());row={'binding_id':bid,**row}
        db.execute('INSERT OR IGNORE INTO bindings('+','.join(row)+') VALUES ('+','.join('?' for _ in row)+')',list(row.values()))
        db.execute('INSERT INTO occurrences VALUES (?,?,?)',(bid,s,n))
        db.execute('INSERT INTO binding_declarations VALUES (?,?,?,?)',(bid,s,n,json.dumps(row)))
        for name,logic,expression,raw in permission_logic(anns):db.execute('INSERT INTO permission_relations(binding_id,source_id,line,annotation,logic,expression_json,raw_annotation) VALUES (?,?,?,?,?,?,?)',(bid,s,n,name,logic,json.dumps(expression),raw))
    for src in spec['sources']:
        path=inventory.parent/src['path'];data=path.read_bytes()
        if digest(data)!=src['sha256']:raise ValueError('Hash mismatch: '+str(path))
        if src['channel']!='stable':raise ValueError('Development snapshots must use a separate catalog')
        if not re.fullmatch('[0-9a-f]{40}',src['revision']) and not src['revision'].startswith('sdk-artifact:'):raise ValueError('Pin immutable commit or exact SDK artifact revision')
        sid=digest((src['url']+'@'+src['revision']+':'+src['sha256']).encode())
        keys=['path','url','revision','retrieved_at','sha256','api_level','channel','kind','license']
        db.execute('INSERT INTO sources VALUES ('+','.join('?' for _ in range(10))+')',[sid]+[src[k] for k in keys]);totals['sources']+=1
        text=data.decode('utf-8')
        if src['kind']=='metalava':
            package=owner=None
            for n,line in enumerate(text.splitlines(),1):
                stripped=line.strip()
                if not stripped or stripped.startswith('//') or stripped=='}':continue
                try:
                    clean,anns=annotations(stripped)
                    if clean.startswith('package '):
                        package=clean.split()[1];row=dict(signature=package,package=package,owner=None,kind='package',name=package,return_type=None,arguments_json='[]',raw_signature=stripped);binding(row,anns,sid,n)
                    elif re.search(r'\b(class|interface|enum|@interface)\s',clean):
                        m=re.search(r'\b(class|interface|enum)\s+([\w.$]+)',clean)
                        if not m:raise ValueError('unsupported class header')
                        owner=package+'.'+m.group(2);row=dict(signature=owner,package=package,owner=owner,kind=m.group(1),name=m.group(2),return_type=None,arguments_json='[]',raw_signature=stripped);binding(row,anns,sid,n)
                    else:
                        totals['signature_declarations']+=1
                        if not owner:raise ValueError('member without owner')
                        row,anns=parse_signature(stripped,package,owner);binding(row,anns,sid,n);totals['parsed_declarations']+=1
                except Exception as e:issue(sid,n,str(e),stripped)
        elif src['kind']=='manifest':
            import xml.etree.ElementTree as ET
            ns='{http://schemas.android.com/apk/res/android}'
            for p in ET.fromstring(text).iter('permission'):
                db.execute('INSERT INTO permission_definitions VALUES (?,?,?,?)',(p.attrib[ns+'name'],sid,p.attrib.get(ns+'protectionLevel'),json.dumps(p.attrib)))
        elif src['kind']=='aidl':
            code=re.sub(r'/\*.*?\*/|//[^\n]*','',text,flags=re.S)
            p=re.search(r'\bpackage\s+([\w.]+)\s*;',code);d=re.search(r'\b(interface|parcelable|union|enum)\s+(\w+)',code)
            pkg=p.group(1) if p else None;descriptor=pkg+'.'+d.group(2) if p and d and d.group(1)=='interface' else None
            db.execute('INSERT INTO aidl_schemas VALUES (?,?,?,?,?,?,?,?)',(sid,pkg,descriptor,d.group(1) if d else None,text,'raw-only; members unresolved','{}','unknown'))
            issue(sid,1,'AIDL schema retained intact; member grammar, transactions, imports, permission mappings and codecs are unresolved',text)
        else:issue(sid,None,'Source retained in inventory but unsupported kind',src['kind'])
    db.commit()
    validation={'integrity_check':db.execute('PRAGMA integrity_check').fetchone()[0],'foreign_key_check':db.execute('PRAGMA foreign_key_check').fetchall(),'counts':{},'reconciliation':totals}
    for table in ('sources','bindings','occurrences','binding_declarations','permission_definitions','permission_relations','aidl_schemas','issues'):
        cursor=db.execute('SELECT * FROM '+table);cols=[x[0] for x in cursor.description];rows=cursor.fetchall();validation['counts'][table]=len(rows)
        with (out/(table+'.jsonl')).open('w') as f:
            for r in rows:f.write(json.dumps(dict(zip(cols,r)),ensure_ascii=False)+'\n')
    assert totals['signature_declarations']==totals['parsed_declarations']+db.execute("SELECT count(*) FROM issues i JOIN sources s USING(source_id) WHERE s.kind='metalava' AND i.raw NOT LIKE '%class %' AND i.raw NOT LIKE '%interface %'").fetchone()[0]
    (out/'validation.json').write_text(json.dumps(validation,indent=2));db.close();return validation

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('inventory');p.add_argument('output');a=p.parse_args();print(json.dumps(run(a.inventory,a.output),indent=2))
