"""APEX source JSON/Soong literals only; build files are never executed."""
import json,re,pathlib
from interface_module_facts import tokens,value,literal,mask_comments
def manifest(text):
 duplicates=[]
 def pairs(items):
  result={}
  for key,value in items:
   if key in result:duplicates.append(key)
   result[key]=value
  return result
 masked=mask_comments(text)
 try:raw=json.loads(masked,object_pairs_hook=pairs)
 except ValueError as error:return dict(status='unsupported-json',reason=str(error),raw=text,applicability=dict(unknown_reason='APEX source manifest could not be parsed'))
 if not isinstance(raw,dict) or duplicates:return dict(status='ambiguous-json',duplicate_keys=duplicates,raw=text,applicability=dict(unknown_reason='APEX manifest object/keys unresolved'))
 name=raw.get('name');version=raw.get('version');normalized=None
 if isinstance(version,int) and not isinstance(version,bool) and 0<=version<=2**63-1:normalized=str(version)
 elif isinstance(version,str) and re.fullmatch(r'0|[1-9][0-9]*',version) and int(version)<=2**63-1:normalized=version
 return dict(status='source-manifest-literals-parsed',name=name if isinstance(name,str) and name else None,
  version_literal=version,normalized_version_code=normalized,raw_properties=raw,
  comment_normalization='comments masked without altering string literals or line positions' if masked!=text else None,raw_source_text=text,
  version_domain='APEX source-manifest version code, not Android API or AIDL frozen version',
  installed_identity_or_version_proven=False,
  applicability=dict(unknown_reason='Product selection, build version overrides and installed module version range are unproven'))
def build_modules(text):
 ts=tokens(text);rows=[];i=0;kinds={'apex','apex_defaults','prebuilt_apex','apex_set'}
 while i<len(ts)-1:
  if ts[i].group() not in kinds or ts[i+1].group()!='{':i+=1;continue
  start=i;properties,end=value(ts,i+1,text,False)
  rows.append(dict(kind=ts[i].group(),line=text.count('\n',0,ts[i].start())+1,
   properties=properties,literal_properties={name:literal(properties,name) for name in ('name','manifest','defaults','apex_name','version','min_sdk_version','updatable','package_name')},
   raw=text[ts[start].start():ts[end-1].end()],installed_module_version_proven=False))
  i=max(i+1,end)
 return rows
def explicit_manifest_path(build_path,module):
 value=module['literal_properties'].get('manifest')
 if not isinstance(value,str):return None
 pure=pathlib.PurePosixPath(value)
 if pure.is_absolute() or '..' in pure.parts or value.startswith(':'):return None
 return str(pathlib.PurePosixPath(build_path).parent/pure)
