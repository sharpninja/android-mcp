import pathlib,shutil,json,datetime,hashlib,difflib
R=pathlib.Path(__file__).resolve().parent;B=R/'catalog-integrated012-sourcev1';D=R/'catalog-integrated012-sourcev2'
if D.exists():raise ValueError('Preserve previous candidate')
shutil.copytree(B,D,ignore=shutil.ignore_patterns('__pycache__'))
name='tooling/build_catalog.py';p=D/name;before=p.read_text();raw=before
old="'tooling/permission_reference.py','contracts/DeviceResolution.schema.json'";new="'tooling/permission_reference.py','tooling/manifest_source.py','contracts/DeviceResolution.schema.json'"
if raw.count(old)!=1:raise ValueError('Fingerprint anchor mismatch')
raw=raw.replace(old,new)
old="    xml=ET.fromstring(path.read_bytes());ns='{http://schemas.android.com/apk/res/android}'\n    defs=list(xml.iter('permission'));report['permission_elements']=len(defs)"
new="    from manifest_source import parse as parse_manifest\n    xml,manifest_report=parse_manifest(path.read_bytes());report.update(manifest_report);ns='{http://schemas.android.com/apk/res/android}'\n    defs=list(xml.iter('permission')) if xml is not None else [];report['permission_elements']=len(defs)"
if raw.count(old)!=1:raise ValueError('Manifest parser anchor mismatch')
raw=raw.replace(old,new)
old="    report['status']='parsed'\n   except ET.ParseError as e:issue(s,None,'Manifest XML unresolved: '+str(e),path.read_text(errors='replace'));report['status']='unsupported'"
new="""   except ET.ParseError as e:
    payload=path.read_bytes()
    try:raw_manifest=payload.decode('utf-8');encoding='utf-8'
    except UnicodeDecodeError:
     import base64
     raw_manifest=base64.b64encode(payload).decode('ascii');encoding='base64'
    issue(s,None,'Manifest XML unresolved: '+str(e),raw_manifest);report.update(status='unsupported',raw_source_encoding=encoding,raw_source_retained_in_issues=True)"""
if raw.count(old)!=1:raise ValueError('Manifest failure anchor mismatch')
p.write_text(raw.replace(old,new));shutil.copyfile(R/'manifest_source.py',D/'tooling/manifest_source.py');shutil.copyfile(R/'test_manifest_source.py',D/'tests/test_manifest_source.py')
patch=''.join(difflib.unified_diff(before.splitlines(True),p.read_text().splitlines(True),fromfile='a/catalog/tooling/build_catalog.py',tofile='b/catalog/tooling/build_catalog.py'))
(R/'integration/manifest-source012.patch').write_text(patch)
receipt=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),candidate=str(D),base=str(B),build_catalog_before_sha256=hashlib.sha256(before.encode()).hexdigest(),build_catalog_after_sha256=hashlib.sha256(p.read_bytes()).hexdigest(),patch='integration/manifest-source012.patch',new_helpers=['tooling/manifest_source.py','tests/test_manifest_source.py'],source_processing_changed=True,java_sdk_parser_cache_fingerprints_changed=False,active011_unchanged=True)
(R/'integration/manifest-source012-actions.json').write_text(json.dumps(receipt,indent=2));print(json.dumps(receipt,indent=2))
