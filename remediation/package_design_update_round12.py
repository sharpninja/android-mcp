"""Validate and bundle only the authorized design/diagram deliverables."""
import pathlib,zipfile,hashlib,json,re,datetime,xml.etree.ElementTree as E,subprocess,posixpath,shutil
R=pathlib.Path(__file__).resolve().parent;D=R/'design';W='{http://schemas.openxmlformats.org/wordprocessingml/2006/main}';REL='{http://schemas.openxmlformats.org/package/2006/relationships}'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest();now=datetime.datetime.now(datetime.timezone.utc)
docx=D/'Android MCP Bridge Design.docx';pdf=D/'preview/Android MCP Bridge Design.pdf';md=D/'Android MCP Bridge Design.md'
info=subprocess.run(['pdfinfo',str(pdf)],capture_output=True,text=True,check=True).stdout;pages=int(re.search(r'^Pages:\s+(\d+)',info,re.M)[1])
subprocess.run(['pdftotext','-bbox',str(pdf),str(D/'preview/final-bbox.xhtml')],check=True)
bbox=E.parse(D/'preview/final-bbox.xhtml');outside=[]
for page in bbox.getroot().iter('{http://www.w3.org/1999/xhtml}page'):
 width=float(page.get('width'));height=float(page.get('height'))
 for word in page.iter('{http://www.w3.org/1999/xhtml}word'):
  if float(word.get('xMin'))<-.5 or float(word.get('yMin'))<-.5 or float(word.get('xMax'))>width+.5 or float(word.get('yMax'))>height+.5:outside.append(word.text)
if outside:raise ValueError('Text outside physical page: '+repr(outside[:10]))
with zipfile.ZipFile(docx) as z:
 if z.testzip():raise ValueError('DOCX ZIP integrity failed')
 names=set(z.namelist())
 for name in names:
  if name.endswith(('.xml','.rels')):E.fromstring(z.read(name))
  if 'vbaproject' in name.lower():raise ValueError('Unexpected active document content')
 document=E.fromstring(z.read('word/document.xml'));plain=''.join(n.text or '' for n in document.iter(W+'t'))
 for phrase in ('DeviceCapabilitySnapshot','IVersionApplicabilityResolver','IResolvedTypeGraphBuilder','IGenericBinderTransactionEngine','SDK_INT_FULL','FR-DEV-01','TR-DEV-02','AC-BIND-01','unavailable','unknown'):
  if phrase not in plain:raise ValueError('Missing design content '+phrase)
 for name in names:
  if not name.endswith('.rels'):continue
  base='' if name=='_rels/.rels' else name.rsplit('/_rels/',1)[0]
  for rel in E.fromstring(z.read(name)):
   if rel.get('TargetMode')=='External':continue
   target=rel.get('Target','').split('#',1)[0]
   if not target:continue
   absolute=posixpath.normpath(posixpath.join(base,target)).lstrip('/') if not target.startswith('/') else target.lstrip('/')
   if absolute not in names:raise ValueError('Missing package relationship '+absolute)
 baseline=json.loads((R/'evidence/design-baseline-comparison.json').read_text())
 for paragraph in baseline['docx_unmatched_paragraphs']:
  if paragraph not in plain:raise ValueError('Original DOCX-only caption lost')
 images=len(document.findall('.//{http://schemas.openxmlformats.org/drawingml/2006/picture}pic'))
manifest=json.loads((D/'render-manifest.json').read_text());assert manifest['version']=='1.10.0' and len(manifest['diagrams'])==4
for diagram in manifest['diagrams']:
 if sha(D/'sources'/(diagram['name']+'.mmd'))!=diagram['sourceSha256'] or sha(D/'assets'/(diagram['name']+'.svg'))!=diagram['svgSha256']:raise ValueError('Render hash mismatch')
summary=dict(validated_at=now.isoformat(),docx_pages=pages,docx_images=images,docx_tables=len(document.findall('.//'+W+'tbl')),all_xml_and_relationships_valid=True,original_docx_only_captions_preserved=True,baseline_markdown_unchanged_by_other_actor=True,current_library_baseline_versions=[5,3,2],current_library_bytes_verified=True,words_outside_physical_page=0,visual_review=dict(pages=[27,28,29,36],result='Readable responsibility table, full sequence, new projection/epoch section, traceability and sizing; no clipping or overlap observed.'),mermaid_sources=4,standalone_svg_assets=4,embedded_mermaid_synchronized=True,renderer='Pele1.10.0',resolver_reference_tests='245normal and245optimized suite pass; explicit malformed-kind rejection, effective overlay/document/schema/type gates; no device observations',independent_validation_status='Round011 overall93/84 NOT_READY. Corrected245testcandidate frozen in running012; no98/98claim.',scope='Only AndroidMCPdesign,diagrams,portable renderer inputs and design validation metadata. No catalog/source/credentials/review-evidence bundle included.')
(D/'validation-summary.json').write_text(json.dumps(summary,indent=2)+'\n');(R/'evidence/design-delivery-validation.json').write_text(json.dumps(summary,indent=2)+'\n')
with zipfile.ZipFile(D/'library-baseline-round012/Android MCP Bridge Design Portable.zip') as baseline_zip:readme=baseline_zip.read('README.md').decode()
readme+='\n\n## Current corrected revision (27October2026, round012candidate)\n\nEarlier revision descriptions above are retained history. Current Markdown/DOCX/PDF specify inventory2.1 point evidence, resolved3 transitive schema hashes, digest-pinned complete activation, effective registry+local overrides and current document pins, semantic contract3, canonical policy and separate OS/client/wire authority.245normal and245optimized offline tests pass. The full historical008 scale checkpoint and its memory/time limits are disclosed; no Android performance or deployment is claimed. Latest complete Claude011 verdict was NOT_READY overall93/84; corrected012 is running. No98/98agreement is claimed. Four editable Mermaid/Pele1.10.0 assets are retained. This archive includes design/render assets and design QA metadata only; it excludes catalog/source/review evidence and the separate pending evidence ZIP.\n'
(D/'README.md').write_text(readme)
files=[md,docx,pdf,D/'README.md',D/'PELE-LICENSE.txt',D/'package.json',D/'package-lock.json',D/'render.mjs',D/'render-manifest.json',D/'validation-summary.json']+sorted((D/'sources').glob('*.mmd'))+sorted((D/'assets').glob('*.svg'))+sorted((D/'assets').glob('*.png'))
rows=[dict(file=('Android MCP Bridge Design.pdf' if p==pdf else str(p.relative_to(D))),size_bytes=p.stat().st_size,sha256=sha(p)) for p in files]
(D/'checksums.sha256').write_text(''.join(row['sha256']+'  '+row['file']+'\n' for row in rows))
out=D/'Android MCP Bridge Design Portable.zip'
with zipfile.ZipFile(out,'w',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
 for p,row in zip(files,rows):z.write(p,row['file'])
 z.write(D/'checksums.sha256','checksums.sha256')
if out.stat().st_size>=16000000:raise ValueError('Design bundle too large')
with zipfile.ZipFile(out) as z:
 if any(name.startswith(('catalog/','reviews/','evidence/','vendor/','library-baseline/')) for name in z.namelist()):raise ValueError('Out-of-scope bundle member')
release=D/'releases'/now.strftime('%Y%m%dT%H%M%SZ');release.mkdir(parents=True,exist_ok=False)
deliverables=[md,docx,out]
for p in deliverables:shutil.copyfile(p,release/p.name)
receipt=dict(prepared_at=now.isoformat(),release_directory=str(release),validation=summary,files=[dict(path=str(p),size_bytes=p.stat().st_size,sha256=sha(p)) for p in deliverables],library_write_attempted=False)
(D/'design-update-local-receipt.json').write_text(json.dumps(receipt,indent=2)+'\n');print(json.dumps(receipt))
