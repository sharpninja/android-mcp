"""Full raw SDK lifecycle facts for resolver integration; never select a device.
No target filtering, default availability or interpretation of extension routes.
Owner and member constraints remain separate so neither can override the other.
"""
import pathlib,json,hashlib,collections,datetime,xml.etree.ElementTree as ET,xml.parsers.expat
def enumerate_evidence(data):
 root=ET.fromstring(data)
 if root.tag!='api' or root.get('version')!='4':raise ValueError('Unsupported lifecycle format')
 locations=[];parser=xml.parsers.expat.ParserCreate()
 def start(tag,attrs):
  if tag in ('class','method','field'):locations.append((tag,parser.CurrentLineNumber))
 parser.StartElementHandler=start;parser.Parse(data,True);rows=[];position=0
 for owner in root.findall('class'):
  for ordinal,node in enumerate([owner]+[n for n in owner if n.tag in ('method','field')]):
   tag,line=locations[position];position+=1
   if tag!=node.tag:raise ValueError('Lifecycle locator mismatch')
   rows.append(dict(owner_binary_name=owner.get('name'),tag=node.tag,member_ordinal=ordinal,line=line,attributes=dict(node.attrib),owner_attributes=dict(owner.attrib),supertypes=[dict(n.attrib,relation=n.tag) for n in owner if n.tag in ('extends','implements')] if node is owner else None,semantics='Exact source facts; absent constraints unknown/not declared, never assumed unrestricted'))
 if position!=len(locations):raise ValueError('Uninventoried lifecycle declaration')
 return rows,[dict(n.attrib) for n in root if n.tag=='sdk']
def run(root):
 root=pathlib.Path(root);source=root/'sdk37.2/platform-metadata/android-37.2/data/api-versions.xml';data=source.read_bytes();digest=hashlib.sha256(data).hexdigest();rows,sdks=enumerate_evidence(data)
 out=root/'startup-availability-evidence-v1'
 if out.exists():raise ValueError('Preserve immutable evidence output')
 out.mkdir();target=out/'api-lifecycle-facts.jsonl'
 with target.open('w') as stream:
  for row in rows:stream.write(json.dumps(dict(row,source_sha256=digest),sort_keys=True,separators=(',',':'))+'\n')
 manifest=dict(schema_version='source-evidence-1',at=datetime.datetime.now(datetime.timezone.utc).isoformat(),source=dict(path=str(source.relative_to(root)),sha256=digest,platform_api='37.2',sdk_package_revision=1,sdk_extension_level=24),sdk_definitions=sdks,counts=dict(collections.Counter(r['tag'] for r in rows)),facts_file=dict(path=target.name,bytes=target.stat().st_size,sha256=hashlib.sha256(target.read_bytes()).hexdigest()),producer_sha256=hashlib.sha256(pathlib.Path(__file__).read_bytes()).hexdigest(),complete_source_index=True,complete_global_catalog=False,limitations=['No device matching or binding grant performed','Includes all historical/future/removed lifecycle nodes; none dropped by target filtering','Raw module/sdks routes require reviewed resolver semantics','Owner and member constraints both apply; absent facts remain unknown'])
 (out/'manifest.json').write_text(json.dumps(manifest,indent=2));print(json.dumps(manifest,indent=2));return manifest
if __name__=='__main__':run(pathlib.Path(__file__).resolve().parent)
