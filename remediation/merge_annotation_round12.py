import pathlib,json,hashlib
R=pathlib.Path(__file__).resolve().parent;C=R/'catalog';S=pathlib.Path('/home/sharpninja/github/2026-10-26/task/android-catalog-20261026');a=json.loads((S/'integration/annotation-erasure-source011-actions.json').read_text());B=pathlib.Path(a['base']);N=pathlib.Path(a['candidate']);assert hashlib.sha256(pathlib.Path(a['patch']).read_bytes()).hexdigest()==a['patch_sha256'];assert (C/'tooling/hidl_source.py').exists()
p=C/'tooling/annotation_link.py';assert p.read_bytes()==(B/'tooling/annotation_link.py').read_bytes();p.write_bytes((N/'tooling/annotation_link.py').read_bytes())
old=(B/'tooling/build_catalog.py').read_text();new=(N/'tooling/build_catalog.py').read_text();p=C/'tooling/build_catalog.py';s=p.read_text()
# The four exact edits are independent of the owner's permission-reference changes.
oldlines=old.splitlines();newlines=new.splitlines()
import difflib
matcher=difflib.SequenceMatcher(a=oldlines,b=newlines);edits=0
for op,i,j,x,y in matcher.get_opcodes():
 if op=='equal':continue
 before='\n'.join(oldlines[i:j]);after='\n'.join(newlines[x:y])
 if not before:
  anchor='\n'.join(oldlines[max(0,i-1):i]);assert s.count(anchor)==1;s=s.replace(anchor,anchor+'\n'+after,1)
 else:assert s.count(before)==1;s=s.replace(before,after,1)
 edits+=1
p.write_text(s)
for name in ('test_annotation_jvm_erasure.py','test_annotation_jvm_erasure_import.py'):
 p=C/'tests'/name;assert not p.exists();s=(N/'tests'/name).read_text()
 if name.endswith('_import.py'):
  s=s.replace('import build_catalog,sdk_api_index,test_source_annotation_targets','import build_catalog,sdk_api_index,test_build,copy,cache_fingerprint')
  s=s.replace("h=test_source_annotation_targets.SourceTargets();inv=h.fixture(root);", "inv=test_build.BuildTests().fixture(root);")
  s=s.replace("   h.source(root,spec,'sdk-api-index','index.xml',data,platform_api_version='37.2',metadata_cache=dict(path=cache.name,sha256=hashlib.sha256(cache.read_bytes()).hexdigest(),records=len(records)))", "   self.source(root,spec,'sdk-api-index','index.xml',data,platform_api_version='37.2',metadata_cache=dict(path=cache.name,sha256=hashlib.sha256(cache.read_bytes()).hexdigest(),records=len(records),parser_sha256=cache_fingerprint.current('sdk-api-index')))")
  s=s.replace("   h.source(root,spec,'sdk-annotations'", "   self.source(root,spec,'sdk-annotations'")
  s=s.replace(' def check(self,item,expected', ''' def source(self,root,spec,kind,name,data,**extras):
  (root/name).write_bytes(data);s=copy.deepcopy(spec['sources'][0]);s.update(kind=kind,path=name,local_path=name,sha256=hashlib.sha256(data).hexdigest(),git_blob=hashlib.sha1(b'blob '+str(len(data)).encode()+b'\\0'+data).hexdigest(),size_bytes=len(data));s.update(extras);spec['sources'].append(s)
 def check(self,item,expected''')
 p.write_text(s)
(R/'evidence/annotation-source011-merge.json').write_text(json.dumps(dict(source_actions=str(S/'integration/annotation-erasure-source011-actions.json'),patch_sha256=a['patch_sha256'],builder_edits=edits,annotation_link_byte_identical_to_owner=True,import_test_fixture_adapted=True,prospective_matches=a['prior_unresolved_current_annotations_with_exact_descriptor'],prospective_permission_matches=a['of_which_permission_annotations'],prospective_remaining=a['expected_remaining_prior_unmatched'],production_updated=False,source_files_modified=False),indent=2)+'\n')
print('Merged exact-descriptor annotation fallback; source guard/type-variable behavior retained; no production claim')
