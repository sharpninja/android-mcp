"""Checked offline COPY migration. Never modifies original release or active files."""
import pathlib,sqlite3,json,os,sys
P=pathlib.Path(__file__).resolve().parents[1]

def migrate(source,destination):
    source=pathlib.Path(source).resolve();target=pathlib.Path(destination).resolve()
    staging=target.with_suffix(target.suffix+'.staging')
    if target.exists() or staging.exists():raise ValueError('Destination must be new')
    old=sqlite3.connect(source.as_uri()+'?mode=ro',uri=True);old.row_factory=sqlite3.Row
    if old.execute('PRAGMA integrity_check').fetchone()[0]!='ok' or old.execute('PRAGMA foreign_key_check').fetchall():raise ValueError('Source integrity/FK failure')
    new=sqlite3.connect(staging);new.execute('PRAGMA foreign_keys=ON')
    try:
        for p in sorted((P/'migrations').glob('*.sql')):new.executescript(p.read_text())
        tables=[r[0] for r in old.execute("SELECT name FROM sqlite_master WHERE type='table'")]
        new.execute('PRAGMA defer_foreign_keys=ON');new.execute('BEGIN IMMEDIATE')
        for table in tables:
            if table=='catalog_view':continue
            for row in old.execute('SELECT * FROM "'+table+'"'):
                r=dict(row)
                if table in ('registries','import_jobs') and 'status' in r:r['state']=r.pop('status')
                if table=='permission_relations':r['logic']=json.loads(r['expression_json'])['op']
                if table=='catalog_meta' and r['key']=='schema_version':r['value']='1.2.0'
                if table=='effective_entries':r['etag']='"cat1:1:'+r['record_sha256']+':'+r['entry_id']+':0:active"'
                cols=','.join('"'+k+'"' for k in r)
                new.execute('INSERT INTO "'+table+'"('+cols+') VALUES('+','.join('?' for _ in r)+')',list(r.values()))
            actual=new.execute('SELECT count(*) FROM "'+table+'"').fetchone()[0]
            expected=old.execute('SELECT count(*) FROM "'+table+'"').fetchone()[0]
            if actual!=expected:raise ValueError('Count mismatch '+table)
        if new.execute('PRAGMA integrity_check').fetchone()[0]!='ok' or new.execute('PRAGMA foreign_key_check').fetchall():raise ValueError('Migrated integrity/FK failure')
        new.commit();new.close();old.close();os.rename(staging,target)
    except Exception:
        new.rollback();new.close();old.close()
        # Preserve failed staging bytes for diagnosis; never present as published output.
        raise

if __name__=='__main__':migrate(sys.argv[1],sys.argv[2])
