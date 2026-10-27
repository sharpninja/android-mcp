import pathlib,json,hashlib,difflib
R=pathlib.Path(__file__).resolve().parent;C=R/'catalog';S=pathlib.Path('/home/sharpninja/github/2026-10-26/task/android-catalog-20261026');action=json.loads((S/'integration/hidl-source011-actions.json').read_text());base=pathlib.Path(action['base']);candidate=pathlib.Path(action['candidate'])
assert hashlib.sha256(pathlib.Path(action['patch']).read_bytes()).hexdigest()==action['patch_sha256']
copied=[]
for name in ('tooling/hidl_source.py','tests/test_hidl_source.py'):
 src=candidate/name;row=next(v for v in action['files'] if v['file']==name);assert hashlib.sha256(src.read_bytes()).hexdigest()==row['candidate_sha256'];assert not (C/name).exists();(C/name).write_bytes(src.read_bytes());copied.append(name)
old=(base/'tooling/build_catalog.py').read_text();new=(candidate/'tooling/build_catalog.py').read_text();p=C/'tooling/build_catalog.py';s=p.read_text()
assert 'hidl_source.py' not in s;s=s.replace("'tooling/aidl_lex.py',","'tooling/aidl_lex.py','tooling/hidl_source.py',")
guard=next(line for line in new.splitlines() if 'HIDL package/source-release version does not establish' in line)
needle=next(line for line in s.splitlines() if "Source api_level must be positive" in line);s=s.replace(needle,needle+'\n'+guard)
start=new.index("  elif kind=='evidence' and s.get('scope')=='aosp-pinned-hidl-source-evidence':");end=new.index("  elif kind in ('evidence','sdk-history'):",start);branch=new[start:end]
assert branch not in old;s=s.replace("  elif kind in ('evidence','sdk-history'):",branch+"  elif kind in ('evidence','sdk-history'):");p.write_text(s)
p=C/'tests/test_hidl_import.py';assert not p.exists();s=(candidate/'tests/test_hidl_import.py').read_text().replace('import build_catalog,test_source_annotation_targets','import build_catalog,test_build,hashlib,copy')
s=s.replace("h=test_source_annotation_targets.SourceTargets();inv=h.fixture(root);spec=json.loads(inv.read_text());s=h.source(root,spec,'evidence','IFoo.hal',raw,scope='aosp-pinned-hidl-source-evidence',api_level=None,channel='unknown',platform_api_version=None);s.update(extras);inv.write_text(json.dumps(spec));return inv", "inv=test_build.BuildTests().fixture(root);spec=json.loads(inv.read_text());(root/'IFoo.hal').write_bytes(raw);s=copy.deepcopy(spec['sources'][2]);s.update(kind='evidence',path='IFoo.hal',local_path='IFoo.hal',scope='aosp-pinned-hidl-source-evidence',api_level=None,channel='unknown',platform_api_version=None,sha256=hashlib.sha256(raw).hexdigest(),git_blob=hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\\0'+raw).hexdigest(),size_bytes=len(raw));s.update(extras);spec['sources'].append(s);inv.write_text(json.dumps(spec));return inv")
p.write_text(s)
(R/'evidence/hidl-source011-merge.json').write_text(json.dumps(dict(source_actions=str(S/'integration/hidl-source011-actions.json'),source_patch_sha256=action['patch_sha256'],copied_byte_identical=copied,adapted_test_fixture='test_hidl_import.py uses own standard fixture, assertions unchanged',preserved='Newer own producer/projection/contracts and source active files',limitations=action['limitations']),indent=2)+'\n')
print('Merged HIDL parser/import branch into owned candidate; no source workspace writes')
