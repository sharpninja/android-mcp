"""Fetch only selected metadata members of Google's SDK platform ZIP by range.
No android.jar/classes/native library payload is requested or stored.
"""
import pathlib,json,urllib.request,hashlib,datetime,xml.etree.ElementTree as E,sys
R=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(R/'catalog/tooling'))
from sdk_zip_metadata import extract_metadata
root=E.parse(R/'repository2-3.xml').getroot();p=next(x for x in root.findall('remotePackage') if x.attrib['path']=='platforms;android-37.2');c=p.find('archives/archive/complete');size=int(c.findtext('size'));url='https://dl.google.com/android/repository/'+c.findtext('url')
D=R/'sdk37.2/platform-metadata';D.mkdir(parents=True,exist_ok=True);requests=[];etag=None
def get(start,count):
 global etag
 headers={'Range':f'bytes={start}-{start+count-1}','Accept-Encoding':'identity'}
 if etag:headers['If-Match']=etag
 with urllib.request.urlopen(urllib.request.Request(url,headers=headers),timeout=45) as response:
  if response.status!=206:raise ValueError('Server did not honor range; refusing full platform archive download')
  content_range=response.headers.get('Content-Range');expected=f'bytes {start}-{start+count-1}/{size}'
  if content_range!=expected:raise ValueError('Unexpected Content-Range '+str(content_range))
  tag=response.headers.get('ETag')
  if etag and tag!=etag:raise ValueError('SDK archive ETag changed during metadata retrieval')
  etag=tag;data=response.read(count+1)
  if len(data)!=count:raise ValueError('Unexpected metadata range length')
  requests.append(dict(start=start,length=count,status=206,sha256=hashlib.sha256(data).hexdigest(),retrieved_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),etag=etag));return data
try:
 files,entries=extract_metadata(size,get)
 for name,b in files.items():
  if pathlib.PurePosixPath(name).is_absolute() or '..' in pathlib.PurePosixPath(name).parts:raise ValueError('Unsafe metadata path')
  dest=D/name;dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(b)
 receipt=dict(url=url,package='platforms;android-37.2',api_level='37.2',package_revision=p.findtext('revision/major'),extension_level=p.findtext('type-details/extension-level'),channel='stable',published_archive_sha1=c.findtext('checksum'),full_archive_sha1_verified=False,reason='Only metadata byte ranges fetched; platform binary payloads deliberately excluded',etag=etag,requests=requests,downloaded_bytes=sum(r['length'] for r in requests),members=[dict(path=n,bytes=len(b),sha256=hashlib.sha256(b).hexdigest()) for n,b in files.items()])
 (D/'archive-members.json').write_text(json.dumps(entries,indent=2));(D/'metadata-range-receipt.json').write_text(json.dumps(receipt,indent=2));print(json.dumps(receipt,indent=2),flush=True)
except Exception as e:
 (D/'metadata-range-failure.json').write_text(json.dumps(dict(url=url,error=str(e),requests=requests),indent=2));raise
