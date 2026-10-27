import pathlib
R=pathlib.Path(__file__).resolve().parent;extra=(R/'wire_evidence_ddl.sql').read_text()
for p in (R/'catalog/bridge-config.sql',R/'device_view_ddl.sql'):
 s=p.read_text();assert 'CREATE TABLE wire_proof_revisions' not in s;s=s.replace('COMMIT;',extra+'\nCOMMIT;') if 'COMMIT;' in s else s+'\n'+extra;p.write_text(s)
print('Added append-only wire evidence revisions with atomic active selection; immutable tuple remains unique')
