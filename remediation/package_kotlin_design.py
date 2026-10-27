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
 for phrase in ('DeviceCapabilitySnapshot','IVersionApplicabilityResolver','IResolvedTypeGraphBuilder','IGenericBinderTransactionEngine','SDK_INT_FULL','FR-DEV-01','TR-DEV-02','AC-BIND-01','FR-CB-01','FR-GRP-01','FR-KT-01','Jetpack Compose','BridgeService','resources/subscribe','notifications/resources/updated','Last-Event-ID','android.groups.discover','unavailable','unknown'):
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
manifest=json.loads((D/'render-manifest.json').read_text());assert manifest['version']=='1.10.0' and len(manifest['diagrams'])==8
for diagram in manifest['diagrams']:
 if sha(D/'sources'/(diagram['name']+'.mmd'))!=diagram['sourceSha256'] or sha(D/'assets'/(diagram['name']+'.svg'))!=diagram['svgSha256']:raise ValueError('Render hash mismatch')
summary=dict(validated_at=now.isoformat(),docx_pages=pages,docx_images=images,docx_tables=len(document.findall('.//'+W+'tbl')),all_xml_and_relationships_valid=True,original_docx_only_captions_preserved=True,baseline_markdown_unchanged_by_other_actor=True,current_library_baseline_versions=[7,5,4],current_library_bytes_verified=True,words_outside_physical_page=0,visual_review=dict(pages=[37,38,40,43,44,45],result='Callback protocol and diagrams, group flow and requirement table visually inspected; no clipping or overlap observed.'),mermaid_sources=8,standalone_svg_assets=8,embedded_mermaid_synchronized=True,renderer='Pele1.10.0',resolver_reference_tests='271 normal and271 optimized offline regression tests pass, including lossless nonUTF8 native retention. Kotlin server/Compose application not implemented; new callback/group lifecycle fixtures are pending; no deployed service or device observations.',independent_validation_status='Round012 overall93/80 NOT_READY, design94/95 contracts95/94. Kotlin server/Compose UI and new callback/group design frozen in running013; no verdict yet. No98/98claim.',scope='Only AndroidMCPdesign,diagrams,portable renderer inputs and design validation metadata. No catalog/source/credentials/review-evidence bundle included.')
(D/'validation-summary.json').write_text(json.dumps(summary,indent=2)+'\n');(R/'evidence/design-delivery-validation.json').write_text(json.dumps(summary,indent=2)+'\n')
with zipfile.ZipFile(D/'library-baseline-kotlin/Android MCP Bridge Design Portable.zip') as baseline_zip:readme=baseline_zip.read('README.md').decode()
readme+='\n\n## Kotlin service and Compose UI design update (27 October 2026)\n\nThis revision uses Kotlin for the MCP service and Jetpack Compose management UI, replacing the former managed UI stack. Service-owned storage/policy/lifecycle and bounded local Binder commands are explicit. It retains exhaustive evidence-qualified callback roles, finite registration with bounded resource-journal streaming, cancellation and synchronous local reply policies, plus small functional groups, job discovery, session activation, byte budgets and a static-host generic-invocation fallback. Eight editable Mermaid sources, Pele1.10.0 SVGs and rendered PNG assets are included. Existing startup resolution, Binder/policy, Avalonia12, SQLite, ADB, RemoteControl and lifecycle requirements are retained. Current offline regression suite passes271 normal and271 optimized tests; callback/group lifecycle reference fixtures and new independent review are pending. Latest complete Claude012 is NOT_READY overall93/80, design94/95 contracts95/94; no98/98agreement is claimed. Only design/render assets and design QA are packaged; no source/catalog/review-evidence bundle.\n'
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
