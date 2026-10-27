"""Conservative manifest source normalization with exact transformation receipt."""
import re,xml.etree.ElementTree as ET
def parse(payload):
 if not payload:return None,dict(status='empty-source-no-declarations',permission_elements=0,valid_android_manifest=False)
 if payload[:4]==b'\x03\x00\x08\x00':raise ET.ParseError('Compiled Android binary XML requires a separate resource-table decoder')
 try:text=payload.decode('utf-8-sig')
 except UnicodeDecodeError:return ET.fromstring(payload),dict(status='parsed-xml-declared-encoding',normalization=None)
 # Only whitespace and XML comments may precede a late declaration. It is not
 # legal XML there, but the transformation does not change the manifest tree.
 position=0
 while True:
  match=re.match(r'\s+|<!--[\s\S]*?-->',text[position:])
  if match:position+=match.end()
  else:break
 receipt=dict(status='parsed',normalization=None)
 if position and text.startswith('<?xml ',position):
  declaration=re.match(r'<\?xml\s+version=["\']1\.0["\'](?:\s+encoding=["\'](?:utf-8|UTF-8)["\'])?(?:\s+standalone=["\'](?:yes|no)["\'])?\s*\?>',text[position:])
  if declaration:
   start,end=position,position+declaration.end();raw=text[start:end]
   text=text[:start]+''.join('\n' if c=='\n' else ' ' for c in raw)+text[end:]
   receipt.update(normalization=dict(kind='late-utf8-xml-declaration-masked-after-comments',character_span=[start,end],raw_declaration=raw,raw_source_unchanged=True))
 root=ET.fromstring(text)
 if root.tag!='manifest':raise ET.ParseError('Root is not an Android manifest element')
 return root,receipt
