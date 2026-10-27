"""Fetch Google's exact stable SDK37.2 source-data archive, never an SDK install."""
import pathlib,urllib.request,json,hashlib,datetime,zipfile,shutil,xml.etree.ElementTree as E
R=pathlib.Path(__file__).resolve().parent;metadata=R/'repository2-3.xml';root=E.parse(metadata).getroot()
p=next(p for p in root.findall('remotePackage') if p.attrib['path']=='sources;android-37.2')
assert p.find('channelRef').attrib['ref']=='channel-0';assert p.findtext('type-details/api-level')=='37.2';assert not p.findtext('type-details/codename')
complete=p.find('archives/archive/complete');size=int(complete.findtext('size'));expected=complete.findtext('checksum');url='https://dl.google.com/android/repository/'+complete.findtext('url')
D=R/'sdk37.2';D.mkdir(exist_ok=True);dest=D/complete.findtext('url')
assert shutil.disk_usage(R).free>5*1024*1024*1024
receipt=dict(url=url,package=p.attrib['path'],api_level='37.2',package_revision=p.findtext('revision/major'),extension_level=p.findtext('type-details/extension-level'),channel='stable',published_size_bytes=size,published_sha1=expected,repository_metadata_sha256=hashlib.sha256(metadata.read_bytes()).hexdigest(),license_reference=p.find('uses-license').attrib['ref'],license_acceptance_action=False,sdk_installation=False,platform_binary_archive=False)
if not dest.exists():
 with urllib.request.urlopen(url,timeout=60) as response,dest.with_suffix('.partial').open('wb') as out:
  count=0
  while b:=response.read(1024*1024):
   count+=len(b)
   if count>size:raise ValueError('Archive exceeds published bound')
   out.write(b)
 dest.with_suffix('.partial').replace(dest)
b=dest.read_bytes();assert len(b)==size;assert hashlib.sha1(b).hexdigest()==expected
receipt.update(sha256=hashlib.sha256(b).hexdigest(),retrieved_at=datetime.datetime.fromtimestamp(dest.stat().st_mtime,datetime.timezone.utc).isoformat(),sha1_verified=True)
with zipfile.ZipFile(dest) as z:
 members=[dict(path=i.filename,size_bytes=i.file_size,compressed_bytes=i.compress_size,crc32=f'{i.CRC:08x}') for i in z.infolist() if not i.is_dir()]
 receipt.update(files=len(members),expanded_size_bytes=sum(x['size_bytes'] for x in members),by_extension={ext:sum(x['path'].endswith(ext) for x in members) for ext in ('.java','.kt','.aidl','.xml','.txt','.jar','.class','.so')})
 (D/'archive-members.json').write_text(json.dumps(members,indent=2))
 print('metadata candidates',[m['path'] for m in members if any(s in m['path'].lower() for s in ('api-versions','annotations.zip','source.properties','package.xml','current.txt','manifest.java','permission.java'))][:30],flush=True)
(D/'source-archive-receipt.json').write_text(json.dumps(receipt,indent=2));print(json.dumps(receipt,indent=2),flush=True)
