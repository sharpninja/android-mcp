import pathlib,zipfile
R=pathlib.Path(__file__).resolve().parent/'design';D=R/'docx-diagnostic';D.mkdir(exist_ok=True)
with zipfile.ZipFile(R/'library-baseline/Android MCP Bridge Design.docx') as z:old={n:z.read(n) for n in z.namelist()}
with zipfile.ZipFile(R/'Android MCP Bridge Design.docx') as z:new={n:z.read(n) for n in z.namelist()}
minimal=b'''<?xml version="1.0" encoding="UTF-8" standalone="yes"?><w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body><w:p><w:r><w:t>Render diagnostic</w:t></w:r></w:p></w:body></w:document>'''
cases={'baseline-min':dict(old,**{'word/document.xml':minimal}),'current-min':dict(new,**{'word/document.xml':minimal}),'numbering-only':dict(old,**{'word/numbering.xml':new['word/numbering.xml']}),'content-types-only':dict(old,**{'[Content_Types].xml':new['[Content_Types].xml']})}
for name,parts in cases.items():
 with zipfile.ZipFile(D/(name+'.docx'),'w',zipfile.ZIP_DEFLATED) as z:
  for path,value in parts.items():z.writestr(path,value)
print(str(D))
