"""Unzip all runtime-part ZIPs into one new directory, then run this script.
Python 3.10+, standard library only. No network, device operations or installs.
"""
import pathlib,json,hashlib,zipfile,tempfile,os,sqlite3
R=pathlib.Path(__file__).resolve().parent
manifest=json.loads((R/'runtime-parts-manifest.json').read_text())
def digest(p):
 h=hashlib.sha256()
 with p.open('rb') as f:
  while b:=f.read(1024*1024):h.update(b)
 return h.hexdigest()
output=R/'catalog.sqlite'
if output.exists():raise SystemExit('Refusing to overwrite catalog.sqlite; use a new empty reassembly directory')
joined=R/manifest['archive_name']
if joined.exists():
 if digest(joined)!=manifest['archive_sha256']:raise SystemExit('Existing archive hash mismatch; remove only that incomplete generated archive and retry')
else:
 with tempfile.NamedTemporaryFile(dir=R,prefix='runtime-join-',delete=False) as target:
  temp=pathlib.Path(target.name)
  try:
   for part in manifest['parts']:
    name=part['name']
    if pathlib.Path(name).name!=name:raise ValueError('Unsafe part name')
    source=R/name
    if source.stat().st_size!=part['size_bytes'] or digest(source)!=part['sha256']:raise ValueError('Missing or corrupt part: '+name)
    with source.open('rb') as f:
     while b:=f.read(1024*1024):target.write(b)
  except Exception:temp.unlink(missing_ok=True);raise
 if digest(temp)!=manifest['archive_sha256']:temp.unlink();raise ValueError('Joined archive hash mismatch')
 os.replace(temp,joined)
with zipfile.ZipFile(joined) as archive, tempfile.NamedTemporaryFile(dir=R,prefix='catalog-verified-',delete=False) as target:
 temp=pathlib.Path(target.name)
 with archive.open('catalog.sqlite') as source:
  while b:=source.read(1024*1024):target.write(b)
if temp.stat().st_size!=manifest['sqlite_size_bytes'] or digest(temp)!=manifest['sqlite_sha256']:temp.unlink();raise ValueError('SQLite byte hash mismatch')
os.replace(temp,output)
db=sqlite3.connect('file:'+str(output)+'?mode=ro',uri=True)
assert db.execute('SELECT count(*) FROM bindings').fetchone()[0]==manifest['binding_entries']
assert db.execute('PRAGMA foreign_key_check').fetchall()==[]
db.close()
print('Verified:',output)
print('Entries:',manifest['binding_entries'])
print('SHA-256:',manifest['sqlite_sha256'])
