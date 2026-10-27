"""Synchronize Markdown into the existing DOCX package using stdlib OOXML.
Preserves styles, header/footer and document identity; no software installation.
"""
import pathlib,zipfile,xml.etree.ElementTree as E,re,io,struct,datetime,json,posixpath
R=pathlib.Path(__file__).resolve().parent;D=R/'design';source=D/'library-baseline-kotlin/Android MCP Bridge Design.docx';output=D/'Android MCP Bridge Design.docx'
W='http://schemas.openxmlformats.org/wordprocessingml/2006/main';RR='http://schemas.openxmlformats.org/officeDocument/2006/relationships';PR='http://schemas.openxmlformats.org/package/2006/relationships';A='http://schemas.openxmlformats.org/drawingml/2006/main';WP='http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing';PIC='http://schemas.openxmlformats.org/drawingml/2006/picture';CT='http://schemas.openxmlformats.org/package/2006/content-types'
for prefix,uri in [('w',W),('r',RR),('a',A),('wp',WP),('pic',PIC)]:E.register_namespace(prefix,uri)
def q(name,ns=W):return '{'+ns+'}'+name
def sub(parent,name,attrs=None,text=None,ns=W):
 node=E.SubElement(parent,q(name,ns),{q(k,ns):str(v) for k,v in (attrs or {}).items()});node.text=text;return node
with zipfile.ZipFile(source) as archive:parts={n:archive.read(n) for n in archive.namelist()}
document=E.fromstring(parts['word/document.xml']);body=document.find(q('body'));section=body.find(q('sectPr'))
for node in list(body):body.remove(node)
relationships=E.fromstring(parts['word/_rels/document.xml.rels']);count=1000
for rel in list(relationships):
 if rel.get('Type','').endswith(('/image','/hyperlink')):relationships.remove(rel)
numbering=E.fromstring(parts.get('word/numbering.xml',('<w:numbering xmlns:w="'+W+'"/>').encode()));list_counter=max([1000]+[int(v) for node in numbering.iter() for k,v in node.attrib.items() if k in (q('numId'),q('abstractNumId')) and v.isdigit()])
def list_id(kind):
 global list_counter
 list_counter+=1;identifier=list_counter
 abstract=E.SubElement(numbering,q('abstractNum'),{q('abstractNumId'):str(identifier)})
 level=E.SubElement(abstract,q('lvl'),{q('ilvl'):'0'});sub(level,'start',{'val':1});sub(level,'numFmt',{'val':'decimal' if kind=='ordered' else 'bullet'});sub(level,'lvlText',{'val':'%1.' if kind=='ordered' else '\u2022'});sub(level,'lvlJc',{'val':'left'});props=sub(level,'pPr');sub(props,'ind',{'left':360,'hanging':240})
 num=E.SubElement(numbering,q('num'),{q('numId'):str(identifier)});sub(num,'abstractNumId',{'val':identifier});return identifier
bullet_id=list_id('bullet');ordered_id=None;previous_list=None
def relationship(target,kind,external=False):
 global count
 count+=1;identifier='rIdDesign'+str(count);attrs=dict(Id=identifier,Type=RR+'/'+kind,Target=target)
 if external:attrs['TargetMode']='External'
 E.SubElement(relationships,q('Relationship',PR),attrs);return identifier
def run(parent,text,bold=False,italic=False,code=False,hyperlink=False,size=None):
 if not text:return
 node=sub(parent,'r');props=sub(node,'rPr');sub(props,'rFonts',{'ascii':'DejaVu Sans Mono' if code else 'DejaVu Sans','hAnsi':'DejaVu Sans Mono' if code else 'DejaVu Sans'})
 sub(props,'sz',{'val':size or (18 if code else 21)})
 if bold:sub(props,'b')
 if italic:sub(props,'i')
 if hyperlink:sub(props,'color',{'val':'24577A'});sub(props,'u',{'val':'single'})
 value=sub(node,'t',text=text);value.set('{http://www.w3.org/XML/1998/namespace}space','preserve')
def inline(parent,text,bold=False,italic=False,size=None):
 i=0;plain=[]
 def flush():
  if plain:run(parent,''.join(plain),bold,italic,size=size);plain.clear()
 while i<len(text):
  if text[i]=='`':
   end=text.find('`',i+1)
   if end>=0:flush();run(parent,text[i+1:end],bold,italic,code=True,size=size);i=end+1;continue
  if text.startswith('**',i):
   end=text.find('**',i+2)
   if end>=0:flush();inline(parent,text[i+2:end],True,italic,size);i=end+2;continue
  if text[i]=='[':
   depth=1;j=i+1
   while j<len(text) and depth:
    if text[j]=='[':depth+=1
    elif text[j]==']':depth-=1
    j+=1
   if depth==0 and j<len(text) and text[j]=='(':
    k=j+1;balance=1
    while k<len(text) and balance:
     if text[k]=='(':balance+=1
     elif text[k]==')':balance-=1
     k+=1
    if not balance:
     target=text[j+1:k-1].strip('<>');label=text[i+1:j-1]
     if target.startswith(('https://','http://')):
      flush();link=E.SubElement(parent,q('hyperlink'),{q('id',RR):relationship(target,'hyperlink',True)});run(link,label,bold,italic,hyperlink=True,size=size);i=k;continue
  plain.append(text[i]);i+=1
 flush()
def paragraph(text='',style=None,number=None,code=False,parent=None,center=False,page=False):
 p=sub(parent if parent is not None else body,'p');props=sub(p,'pPr')
 if style:sub(props,'pStyle',{'val':style})
 if number:np=sub(props,'numPr');sub(np,'ilvl',{'val':0});sub(np,'numId',{'val':number})
 if center:sub(props,'jc',{'val':'center'})
 if page:sub(props,'pageBreakBefore')
 sub(props,'spacing',{'after':90 if not code else 0,'line':260 if not code else 220,'lineRule':'auto'})
 if style and style.startswith('Heading'):sub(props,'keepNext')
 if code:sub(props,'shd',{'fill':'F2F6FA'});run(p,text,code=True)
 else:
  heading_size={'Title':44,'Heading1':30,'Heading2':25,'Heading3':22}.get(style)
  inline(p,text,bold=heading_size is not None,size=heading_size)
 return p
def table(rows):
 n=max(map(len,rows));total=10224
 ratios=([0.25,0.75] if n==2 else [0.22,0.39,0.39] if n==3 else [1/n]*n);widths=[int(total*r) for r in ratios];widths[-1]+=total-sum(widths)
 t=sub(body,'tbl');props=sub(t,'tblPr');sub(props,'tblW',{'w':total,'type':'dxa'});sub(props,'tblLayout',{'type':'fixed'});borders=sub(props,'tblBorders')
 for edge in ('top','left','bottom','right','insideH','insideV'):sub(borders,edge,{'val':'single','sz':4,'color':'C8D4DE'})
 grid=sub(t,'tblGrid')
 for w in widths:sub(grid,'gridCol',{'w':w})
 for index,row in enumerate(rows):
  tr=sub(t,'tr');tp=sub(tr,'trPr');sub(tp,'cantSplit')
  if index==0:sub(tp,'tblHeader')
  for col in range(n):
   cell=sub(tr,'tc');cp=sub(cell,'tcPr');sub(cp,'tcW',{'w':widths[col],'type':'dxa'});sub(cp,'vAlign',{'val':'top'});sub(cp,'shd',{'fill':'E4EDF5' if index==0 else 'FFFFFF'})
   p=sub(cell,'p');pp=sub(p,'pPr');sub(pp,'spacing',{'after':60,'line':235,'lineRule':'auto'});inline(p,row[col] if col<len(row) else '',bold=index==0,size=18)
 paragraph()
image_count=0
def image(alt,path):
 global image_count
 image_count+=1;png=D/pathlib.PurePosixPath(path).with_suffix('.png');data=png.read_bytes();width,height=struct.unpack('>II',data[16:24]);scale=min(7.0/width,7.9/height);cx=int(width*scale*914400);cy=int(height*scale*914400)
 name='design-'+str(image_count)+'.png';parts['word/media/'+name]=data;rid=relationship('media/'+name,'image')
 paragraph(alt,style='Heading2',page=True)
 p=paragraph(center=True);r=sub(p,'r');drawing=sub(r,'drawing');drawing_inline=E.SubElement(drawing,q('inline',WP),dict(distT='0',distB='0',distL='0',distR='0'))
 E.SubElement(drawing_inline,q('extent',WP),dict(cx=str(cx),cy=str(cy)));E.SubElement(drawing_inline,q('docPr',WP),dict(id=str(image_count),name=alt,descr=alt))
 graphic=E.SubElement(drawing_inline,q('graphic',A));gdata=E.SubElement(graphic,q('graphicData',A),dict(uri=PIC));pic=E.SubElement(gdata,q('pic',PIC));nv=E.SubElement(pic,q('nvPicPr',PIC));E.SubElement(nv,q('cNvPr',PIC),dict(id='0',name=name));E.SubElement(nv,q('cNvPicPr',PIC))
 fill=E.SubElement(pic,q('blipFill',PIC));E.SubElement(fill,q('blip',A),{q('embed',RR):rid});stretch=E.SubElement(fill,q('stretch',A));E.SubElement(stretch,q('fillRect',A));sp=E.SubElement(pic,q('spPr',PIC));xfrm=E.SubElement(sp,q('xfrm',A));E.SubElement(xfrm,q('off',A),dict(x='0',y='0'));E.SubElement(xfrm,q('ext',A),dict(cx=str(cx),cy=str(cy)));geometry=E.SubElement(sp,q('prstGeom',A),dict(prst='rect'));E.SubElement(geometry,q('avLst',A))
 paragraph('Rendered with Pele 1.10.0 from the editable Mermaid source in the portable bundle.')
text=(D/'Android MCP Bridge Design.md').read_text();lines=text.splitlines();i=0;first_heading=True;in_code=False;tables=0
while i<len(lines):
 line=lines[i]
 if line.strip().startswith('```'):in_code=not in_code;i+=1;continue
 if in_code:paragraph(line,code=True);i+=1;continue
 if not line.strip():i+=1;continue
 picture=re.fullmatch(r'!\[([^\]]*)\]\(([^)]+)\)',line.strip())
 if picture:image(picture[1],picture[2]);i+=1;previous_list=None;continue
 if line.lstrip().startswith('|'):
  rows=[]
  while i<len(lines) and lines[i].lstrip().startswith('|'):
   values=[c.strip().replace('\\|','|') for c in re.split(r'(?<!\\)\|',lines[i].strip().strip('|'))]
   if not all(re.fullmatch(r'[:\-\s]+',c or ' ') for c in values):rows.append(values)
   i+=1
  table(rows);tables+=1;previous_list=None;continue
 heading=re.match(r'^(#{1,6})\s+(.+)',line)
 if heading:
  style='Title' if first_heading else 'Heading'+str(min(len(heading[1]),3));paragraph(heading[2],style=style);first_heading=False;i+=1;previous_list=None;continue
 item=re.match(r'^\s*(?:(\d+)\.\s+|([-*])\s+)(.*)',line)
 if item:
  kind='ordered' if item[1] else 'bullet'
  if kind=='ordered' and previous_list!='ordered':ordered_id=list_id('ordered')
  value=item[3];i+=1
  while i<len(lines) and lines[i].strip() and not re.match(r'^\s*(?:\d+\.\s+|[-*]\s+|#|\||```|!\[)',lines[i]):value+=' '+lines[i].strip();i+=1
  paragraph(value,number=ordered_id if kind=='ordered' else bullet_id);previous_list=kind;continue
 value=line.strip();i+=1
 while i<len(lines) and lines[i].strip() and not re.match(r'^\s*(?:\d+\.\s+|[-*]\s+|#|\||```|!\[)',lines[i]):value+=' '+lines[i].strip();i+=1
 paragraph(value);previous_list=None
if section is None:section=E.Element(q('sectPr'))
for child in list(section):
 if child.tag in (q('pgSz'),q('pgMar')):section.remove(child)
sub(section,'pgSz',{'w':12240,'h':15840});sub(section,'pgMar',{'top':900,'bottom':900,'left':1008,'right':1008,'header':420,'footer':420,'gutter':0});body.append(section)
def serialize(root,original=None):
 payload=E.tostring(root,encoding='utf-8',xml_declaration=True)
 if original:
  namespaces=dict(E.iterparse(io.BytesIO(original),events=['start-ns']).__iter__()) if False else {}
  for _,pair in E.iterparse(io.BytesIO(original),events=['start-ns']):namespaces[pair[0]]=pair[1]
  end=payload.find(b'>',payload.find(b'?>')+2);opening=payload[:end]
  for prefix,uri in namespaces.items():
   key=('xmlns:'+prefix if prefix else 'xmlns').encode()
   if key+b'=' not in opening:opening+=b' '+key+b'="'+uri.encode()+b'"'
  payload=opening+payload[end:]
 return payload
orders={
'pPr':'pStyle keepNext keepLines pageBreakBefore framePr widowControl numPr suppressLineNumbers pBdr shd tabs suppressAutoHyphens kinsoku wordWrap overflowPunct topLinePunct autoSpaceDE autoSpaceDN bidi adjustRightInd snapToGrid spacing ind contextualSpacing mirrorIndents suppressOverlap jc textDirection textAlignment textboxTightWrap outlineLvl divId cnfStyle rPr sectPr pPrChange',
'rPr':'rStyle rFonts b bCs i iCs caps smallCaps strike dstrike outline shadow emboss imprint noProof snapToGrid vanish webHidden color spacing w kern position sz szCs highlight u effect bdr shd fitText vertAlign rtl cs em lang eastAsianLayout specVanish oMath rPrChange',
'sectPr':'headerReference footerReference footnotePr endnotePr type pgSz pgMar paperSrc pgBorders lnNumType pgNumType cols formProt vAlign noEndnote titlePg textDirection bidi rtlGutter docGrid printerSettings sectPrChange',
'tblPr':'tblStyle tblpPr tblOverlap bidiVisual tblStyleRowBandSize tblStyleColBandSize tblW jc tblCellSpacing tblInd tblBorders shd tblLayout tblCellMar tblLook tblCaption tblDescription tblPrChange',
'tcPr':'cnfStyle tcW gridSpan hMerge vMerge tcBorders shd noWrap tcMar textDirection tcFitText vAlign hideMark headers cellIns cellDel cellMerge tcPrChange'}
for name,sequence in orders.items():
 rank={q(key):i for i,key in enumerate(sequence.split())}
 for node in document.iter(q(name)):node[:]=sorted(node,key=lambda c:rank.get(c.tag,999))
parts['word/document.xml']=serialize(document,parts['word/document.xml']);E.register_namespace('',PR);parts['word/_rels/document.xml.rels']=E.tostring(relationships,encoding='utf-8',xml_declaration=True);parts['word/numbering.xml']=serialize(numbering,parts.get('word/numbering.xml'))
types=E.fromstring(parts['[Content_Types].xml'])
if not any(n.get('Extension')=='png' for n in types):
 E.SubElement(types,q('Default',CT),dict(Extension='png',ContentType='image/png'));E.register_namespace('',CT);parts['[Content_Types].xml']=E.tostring(types,encoding='utf-8',xml_declaration=True)
for name,value in parts.items():
 if name.endswith('.xml') or name.endswith('.rels'):E.fromstring(value)
with zipfile.ZipFile(output,'w',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
 for name,value in parts.items():z.writestr(name,value)
receipt=dict(generated_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),source_docx=str(source),output=str(output),images=image_count,tables=tables,markdown_characters=len(text),all_xml_parsed=True,styles_headers_footers_preserved=True,no_software_installation=True)
(R/'evidence/design-docx-build.json').write_text(json.dumps(receipt,indent=2)+'\n');print(json.dumps(receipt))
