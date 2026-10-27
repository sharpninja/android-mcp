"""Restore exact frozen source bytes, including SDK ZIP metadata, on explicit request.
Dry run by default. Stops on access denial; temporary429 uses the same URL with
Retry-After backoff. Does not install SDKs, accept licenses, or read platform
binary members. Existing mismatching bytes cause failure, never silent overwrite.
"""
import pathlib,json,hashlib,urllib.request,urllib.error,time,datetime,email.utils,zipfile,xml.etree.ElementTree as ET,sys
from sdk_zip_metadata import extract_metadata
def get(url,headers=None):
 for retry in range(3):
  time.sleep(1)
  try:return urllib.request.urlopen(urllib.request.Request(url,headers=headers or {}),timeout=60)
  except urllib.error.HTTPError as e:
   if e.code!=429 or retry==2:raise
   pause=60*(retry+1);header=e.headers.get('Retry-After')
   if header:
    try:pause=max(pause,float(header))
    except ValueError:pause=max(pause,(email.utils.parsedate_to_datetime(header)-datetime.datetime.now(datetime.timezone.utc)).total_seconds())
   while pause>0:step=min(pause,60);time.sleep(step);pause-=step
def verify(data,s):
 if hashlib.sha256(data).hexdigest()!=s['sha256']:raise ValueError('Source SHA256 mismatch '+s['path'])
 if hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()!=s['git_blob']:raise ValueError('Source local address mismatch '+s['path'])
def restore_member(s,root):
 t=s['transport'];archive=root/t['archive_local_path']
 if not archive.exists():
  archive.parent.mkdir(parents=True,exist_ok=True);temporary=archive.with_suffix('.download-partial')
  with get(t['archive_url']) as response,temporary.open('wb') as f:
   count=0
   while b:=response.read(1024*1024):
    count+=len(b)
    if count>64*1024*1024:raise ValueError('Source-data archive exceeds bounded size')
    f.write(b)
  data=temporary.read_bytes()
  if hashlib.sha256(data).hexdigest()!=t['archive_sha256'] or hashlib.sha1(data).hexdigest()!=t['archive_sha1']:raise ValueError('Source archive hash mismatch')
  temporary.replace(archive)
 data=archive.read_bytes()
 if hashlib.sha256(data).hexdigest()!=t['archive_sha256'] or hashlib.sha1(data).hexdigest()!=t['archive_sha1']:raise ValueError('Existing source archive hash mismatch')
 with zipfile.ZipFile(archive) as z:return z.read(t['archive_member'])
def restore_metadata(s,root):
 t=s['transport'];receipt=json.loads((root/t['metadata_receipt']).read_text());url=t['archive_url'];tag=receipt['etag']
 metadata=ET.parse(root/'repository2-3.xml').getroot();package=next(p for p in metadata.findall('remotePackage') if p.attrib['path']=='platforms;android-37.2');size=int(package.findtext('archives/archive/complete/size'))
 def part(start,count):
  with get(url,{'Range':f'bytes={start}-{start+count-1}','If-Match':tag,'Accept-Encoding':'identity'}) as response:
   if response.status!=206 or response.headers.get('Content-Range')!=f'bytes {start}-{start+count-1}/{size}' or response.headers.get('ETag')!=tag:raise ValueError('Metadata range/version mismatch; refusing full platform download')
   data=response.read(count+1)
   if len(data)!=count:raise ValueError('Metadata range length mismatch')
   return data
 return extract_metadata(size,part)[0]
def main(inventory,download=False):
 inv=pathlib.Path(inventory).resolve();root=inv.parent;sources=json.loads(inv.read_text())['sources'];missing=[];seen=set()
 for s in sources:
  if s['status']!='downloaded' or s['git_blob'] in seen:continue
  seen.add(s['git_blob']);p=root/s['local_path']
  if p.exists():verify(p.read_bytes(),s)
  else:missing.append(s)
 print('Missing exact source blobs:',len(missing),flush=True)
 if not download:return
 metadata_cache={};source_archives={}
 import base64
 for s in missing:
  t=s.get('transport',{});kind=t.get('kind')
  if kind=='sdk-zip-member':
   # Cache selected archive members in memory once, bounded by source archive.
   key=t['archive_url']
   if key not in source_archives:
    first=restore_member(s,root);archive=root/t['archive_local_path']
    source_archives[key]=zipfile.ZipFile(archive)
   data=source_archives[key].read(t['archive_member'])
  elif kind=='bounded-sdk-zip-range-member':
   key=t['archive_url']
   if key not in metadata_cache:metadata_cache[key]=restore_metadata(s,root)
   data=metadata_cache[key][t['archive_member']]
  else:
   with get(s['url']+'?format=TEXT') as response:data=base64.b64decode(response.read(),validate=True)
  verify(data,s);p=root/s['local_path'];p.parent.mkdir(parents=True,exist_ok=True);tmp=p.with_suffix('.restoring');tmp.write_bytes(data);tmp.replace(p)
 for archive in source_archives.values():archive.close()
if __name__=='__main__':main(sys.argv[1],'--download' in sys.argv)
