"""Read selected ZIP metadata members through bounded exact byte ranges.
Never reads android.jar, native libraries or other SDK platform binary payloads.
The caller supplies a range function that must require HTTP206, exact size and
an unchanged ETag. ZIP CRC verifies each selected metadata member.
"""
import io,zipfile
MAX_READ=8*1024*1024
class RangeFile(io.RawIOBase):
 def __init__(self,size,get):self.size=size;self.get=get;self.position=0
 def seekable(self):return True
 def readable(self):return True
 def tell(self):return self.position
 def seek(self,offset,whence=0):
  self.position=offset if whence==0 else self.position+offset if whence==1 else self.size+offset
  if self.position<0 or self.position>self.size:raise ValueError('ZIP seek out of range')
  return self.position
 def read(self,size=-1):
  size=self.size-self.position if size<0 else min(size,self.size-self.position)
  if size>MAX_READ:raise ValueError('Read exceeds metadata byte-range bound')
  b=self.get(self.position,size) if size else b''
  if len(b)!=size:raise ValueError('Short metadata range')
  self.position+=size;return b
def selected(name):
 leaf=name.split('/')[-1]
 return leaf in ('source.properties','sdk.properties','package.xml','api-versions.xml','api-versions-versioned.xml','annotations.zip','public.xml','attrs_manifest.xml','optional.json') or leaf=='AndroidManifest.xml'
def extract_metadata(size,get):
 files={};entries=[]
 with zipfile.ZipFile(RangeFile(size,get)) as archive:
  for i in archive.infolist():
   if i.is_dir():continue
   entries.append(dict(path=i.filename,size_bytes=i.file_size,compressed_bytes=i.compress_size,crc32=f'{i.CRC:08x}',selected=selected(i.filename)))
   if not selected(i.filename):continue
   if i.file_size>32*1024*1024 or i.compress_size>MAX_READ:raise ValueError('Metadata member exceeds bound: '+i.filename)
   files[i.filename]=archive.read(i.filename)
 return files,entries
