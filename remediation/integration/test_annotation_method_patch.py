"""Focused patch tests against current source schema/parsers, only private fixtures."""
import pathlib,sys,importlib.util,tempfile,json,hashlib,zipfile,sqlite3,contextlib,io
R=pathlib.Path(__file__).resolve().parent;C=pathlib.Path('/home/sharpninja/github/2026-10-26/task/android-catalog-20261026/catalog')
sys.path.insert(0,str(C/'tooling'));spec=importlib.util.spec_from_file_location('annotation_patch_builder',R/'build_catalog.annotation-method-only.py');builder=importlib.util.module_from_spec(spec);spec.loader.exec_module(builder);builder.P=C
results=[]
with tempfile.TemporaryDirectory() as td:
 for case in ('unique-method','ambiguous-method','sdk372-guard'):
  root=pathlib.Path(td)/case;root.mkdir()
  text='package a {\n public class A {\n method public void go(String);\n'+(' method public void go(java.lang.String);\n' if case=='ambiguous-method' else '')+' }\n}'
  (root/'api.txt').write_text(text)
  with zipfile.ZipFile(root/'annotations.zip','w') as z:z.writestr('a.xml','<root><item name="a.A void go(java.lang.String)"><annotation name="android.annotation.RequiresPermission"><val name="value" val="&quot;android.permission.CAMERA&quot;"/></annotation></item></root>')
  sources=[]
  for kind,name in [('sdk-signature','api.txt'),('sdk-annotations','annotations.zip')]:
   raw=(root/name).read_bytes();sources.append(dict(kind=kind,path=name,local_path=name,status='downloaded',sha256=hashlib.sha256(raw).hexdigest(),git_blob=hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest(),size_bytes=len(raw),url='https://android.googlesource.com/mock/'+name,revision='1'*40,retrieved_at='2026-10-26T00:00:00Z',license='fixture',scope='source',repository='fixture',api_level='37.2' if case=='sdk372-guard' and kind=='sdk-annotations' else 37,channel='stable'))
  (root/'inventory.json').write_text(json.dumps(dict(sources=sources)))
  with contextlib.redirect_stdout(io.StringIO()):builder.build(root/'inventory.json',root/'out')
  db=sqlite3.connect(root/'out/catalog.sqlite');row=db.execute('SELECT binding_id,document_json FROM annotation_evidence').fetchone();db.close();matched=row[0] is not None
  if matched!=(case=='unique-method'):raise ValueError('Unexpected annotation match '+case)
  results.append(dict(case=case,matched=matched,evidence=json.loads(row[1])['target_match_kind']))
(R/'annotation-method-test-results.json').write_text(json.dumps(dict(cases=results,passed=3,source_active_files_modified=False),indent=2));print(json.dumps(results,indent=2))
