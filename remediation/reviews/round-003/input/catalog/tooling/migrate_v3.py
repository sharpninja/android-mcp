"""Checked offline COPY migration. Never modifies original release or active files."""
import pathlib,sqlite3,json,os,sys
P=pathlib.Path(__file__).resolve().parents[1]

def migrate(source,destination):
    source=pathlib.Path(source).resolve();target=pathlib.Path(destination).resolve()
    staging=target.with_suffix(target.suffix+'.staging')
    if target.exists() or staging.exists():raise ValueError('Destination must be new')
    old=sqlite3.connect(source.as_uri()+'?mode=ro',uri=True);old.row_factory=sqlite3.Row
    if old.execute('PRAGMA user_version').fetchone()[0]!=2:raise ValueError('Only storage user_version=2 can migrate to v3')
    if old.execute('PRAGMA integrity_check').fetchone()[0]!='ok' or old.execute('PRAGMA foreign_key_check').fetchall():raise ValueError('Source integrity/FK failure')
    new=sqlite3.connect(staging);new.execute('PRAGMA foreign_keys=ON')
    try:
        for p in sorted((P/'migrations').glob('*.sql')):new.executescript(p.read_text())
        # SQLite optimizer statistics are internal and are regenerated, not imported.
        tables=[r[0] for r in old.execute("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'")]
        new.execute('PRAGMA defer_foreign_keys=ON');new.execute('BEGIN IMMEDIATE')
        for table in tables:
            if table=='catalog_view':continue
            if table=='effective_entries':
                active=old.execute('SELECT registry_id FROM active_registry WHERE singleton=1').fetchone()
                if not active and old.execute('SELECT count(*) FROM effective_entries').fetchone()[0]:raise ValueError('No active registry for legacy effective entries')
                for row in old.execute('SELECT entry_id,document_json,record_sha256 FROM effective_entries'):
                    new.execute('INSERT INTO registry_entries VALUES(?,?,?,?)',(active[0],row[0],row[1],row[2]))
                if new.execute('SELECT count(*) FROM registry_entries').fetchone()[0]!=old.execute('SELECT count(*) FROM effective_entries').fetchone()[0]:raise ValueError('Entry count mismatch')
                continue
            for row in old.execute('SELECT * FROM "'+table+'"'):
                r=dict(row)
                if table in ('registries','import_jobs') and 'status' in r:r['state']=r.pop('status')
                if table=='permission_relations':r['logic']=json.loads(r['expression_json'])['op']
                if table=='catalog_meta' and r['key']=='schema_version':r['value']='1.2.0'
                if table=='effective_entries':r['etag']='"cat1:1:'+r['record_sha256']+':'+r['entry_id']+':0:active"'
                destination_table='legacy_import_jobs' if table=='import_jobs' else table
                cols=','.join('"'+k+'"' for k in r)
                new.execute('INSERT INTO "'+destination_table+'"('+cols+') VALUES('+','.join('?' for _ in r)+')',list(r.values()))
            actual=new.execute('SELECT count(*) FROM "'+('legacy_import_jobs' if table=='import_jobs' else table)+'"').fetchone()[0]
            expected=old.execute('SELECT count(*) FROM "'+table+'"').fetchone()[0]
            if actual!=expected:raise ValueError('Count mismatch '+table)
        if new.execute('PRAGMA integrity_check').fetchone()[0]!='ok' or new.execute('PRAGMA foreign_key_check').fetchall():raise ValueError('Migrated integrity/FK failure')
        new.commit();new.close();old.close();os.rename(staging,target)
    except Exception:
        new.rollback();new.close();old.close()
        # Preserve failed staging bytes for diagnosis; never present as published output.
        raise

if __name__=='__main__':migrate(sys.argv[1],sys.argv[2])
