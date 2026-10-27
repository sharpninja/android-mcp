import pathlib,re,hashlib
P=pathlib.Path(__file__).resolve().parent/'catalog/tests'
for name in ('test_remediation.py','test_round2.py','test_round3.py','test_round4.py','test_round5.py','test_round7.py','test_round8.py','test_sealed_registry.py'):
    p=P/name;s=p.read_text()
    for literal in ('b','h','hash','original','changed'):
        value=hashlib.sha256(('java:a.A' if literal=='b' else literal).encode()).hexdigest()
        s=re.sub(re.escape("'"+literal+"'")+r'(?!\s*\*)',"'"+value+"'",s)
    if name=='test_round8.py':
        s=s.replace('import pathlib,sys,unittest,tempfile,json,hashlib,sqlite3','import pathlib,sys,unittest,tempfile,json,hashlib,sqlite3,contextlib')
        s=s.replace("with sqlite3.connect(root/'out/catalog.sqlite') as db:","with contextlib.closing(sqlite3.connect(root/'out/catalog.sqlite')) as db:")
        s=s.replace("with sqlite3.connect(root/'new.sqlite') as new:","with contextlib.closing(sqlite3.connect(root/'new.sqlite')) as new:")
    p.write_text(s)
