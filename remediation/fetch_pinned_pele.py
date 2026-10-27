"""Fetch exact lockfile render input into this task only; no package installer."""
import base64,hashlib,json,pathlib,tarfile,urllib.request,io,datetime
R=pathlib.Path(__file__).resolve().parent/'design';lock=json.loads((R/'package-lock.json').read_text());pin=lock['packages']['node_modules/pele']
if pin['version']!='1.10.0':raise ValueError('Unexpected Pele pin')
archive=urllib.request.urlopen(pin['resolved'],timeout=60).read();algorithm,expected=pin['integrity'].split('-',1)
if algorithm!='sha512' or base64.b64encode(hashlib.sha512(archive).digest()).decode()!=expected:raise ValueError('Pele integrity mismatch')
target=R/'vendor/pele';target.mkdir(parents=True,exist_ok=False)
with tarfile.open(fileobj=io.BytesIO(archive),mode='r:gz') as tar:
 for item in tar.getmembers():
  parts=pathlib.PurePosixPath(item.name).parts
  if not parts or parts[0]!='package' or '..' in parts or item.issym() or item.islnk():raise ValueError('Unsafe package archive member')
  path=target.joinpath(*parts[1:])
  if item.isdir():path.mkdir(parents=True,exist_ok=True)
  elif item.isfile():
   path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(tar.extractfile(item).read())
  else:raise ValueError('Unexpected package archive member')
meta=json.loads((target/'package.json').read_text());receipt=dict(fetched_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),version=meta['version'],source=pin['resolved'],integrity=pin['integrity'],archive_sha256=hashlib.sha256(archive).hexdigest(),task_only=True,package_installer_used=False,existing_installed_software_modified=False)
(R/'pele-input-receipt.json').write_text(json.dumps(receipt,indent=2)+'\n');print(json.dumps(dict(version=meta['version'],main=meta.get('main'),exports=meta.get('exports'),receipt='design/pele-input-receipt.json')))
