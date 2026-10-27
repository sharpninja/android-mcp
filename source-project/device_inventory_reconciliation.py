"""Read-only exact SQLite-to-startup-inventory export reconciliation."""
import pathlib,json,hashlib,itertools,sqlite3,datetime,sys
def verify(db,out):
 out=pathlib.Path(out);registries=db.execute('SELECT registry_id FROM active_registry').fetchall()
 if len(registries)!=1:raise ValueError('Exactly one active registry required')
 registry=registries[0][0];manifest=json.loads((out/'device_inventory_manifest.json').read_text());digest=hashlib.sha256();count=0;sentinel=object()
 rows=db.execute('SELECT entry_id,document_json,record_sha256 FROM registry_inventory WHERE registry_id=? ORDER BY entry_id',(registry,))
 with (out/'device_inventory.jsonl').open('rb') as lines:
  for row,line in itertools.zip_longest(rows,lines,fillvalue=sentinel):
   if row is sentinel or line is sentinel:raise ValueError('Startup inventory has missing or extra rows')
   entry,raw,stored=row;payload=raw.encode('utf-8')
   if hashlib.sha256(payload).hexdigest()!=stored:raise ValueError('Stored inventory document digest mismatch '+entry)
   if line!=payload+b'\n':raise ValueError('Inventory export differs from SQLite '+entry)
   if json.loads(raw)['entry_id']!=entry:raise ValueError('Inventory entity identity differs from row '+entry)
   digest.update(line);count+=1
   if count%100000==0:print('reconciled startup inventory',count,flush=True)
 if manifest.get('contract')!='urn:android-binding-catalog:2.0.0:DeviceResolution' or manifest.get('registry_id')!=registry or manifest.get('entries')!=count or manifest.get('inventory_sha256')!=digest.hexdigest():raise ValueError('Startup inventory manifest mismatch')
 return dict(entries=count,registry_id=registry,inventory_sha256=digest.hexdigest(),all_record_hashes_verified=True,sqlite_jsonl_byte_equivalent=True,device_operations=False)
def main(root,release):
 root=pathlib.Path(root).resolve();out=root/'catalog/output'/('release-'+release);report=root/('device-inventory-reconciliation-release'+release+'.json')
 if report.exists():raise FileExistsError('Preserve existing immutable reconciliation')
 db=sqlite3.connect((out/'catalog.sqlite').as_uri()+'?mode=ro',uri=True)
 try:result=verify(db,out)
 finally:db.close()
 result.update(completed_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),release=release,database_open_mode='read-only',complete=False);report.write_text(json.dumps(result,indent=2));print(json.dumps(result,indent=2))
if __name__=='__main__':main(pathlib.Path(__file__).resolve().parent,sys.argv[1])
