"""Bounded lexical evidence audit. This does not normalize C++ ABI or Binder wire schemas."""
import re,json,pathlib,hashlib,datetime
FAMILIES=('IMPLEMENT_META_INTERFACE','IMPLEMENT_HYBRID_META_INTERFACE','IMPLEMENT_META_NESTED_INTERFACE')
def tokens(text):
 # One lexical pass distinguishes comments and quoted text. Preserve positions.
 return list(re.finditer(r'//[^\n]*|/\*[\s\S]*?\*/|(?:u8|u|U|L)?R"(?P<rawdelim>[^ ()\t\r\n\\]{0,16})\([\s\S]*?\)(?P=rawdelim)"|(?:u8|u|U|L)?"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'',text))
def clean_comments(text):
 chars=list(text)
 for token in tokens(text):
  if token.group().startswith(('//','/*')):
   chars[token.start():token.end()]=['\n' if c=='\n' else ' ' for c in token.group()]
 return ''.join(chars)
def declarations(text):
 clean=clean_comments(text);quoted=[(m.start(),m.end()) for m in tokens(clean) if not m.group().startswith(('//','/*'))];rows=[]
 pattern=re.compile(r'\b('+'|'.join(FAMILIES)+r')\s*\(([^()]*)\)')
 for match in pattern.finditer(clean):
  if any(a<=match.start()<b for a,b in quoted):continue
  prefix=clean[max(0,clean.rfind('\n',0,match.start())):match.start()]
  if re.search(r'#\s*define\s*$',prefix):continue
  args=match.group(2);literal=re.fullmatch(r'\s*(?:[A-Za-z_]\w*\s*,\s*){1,2}(?:u8|u|U|L)?("(?:\\.|[^"\\])*")\s*',args);value=None
  if literal:
   try:value=json.loads(literal.group(1))
   except ValueError:pass
  rows.append(dict(macro=match.group(1),raw=text[match.start():match.end()],line=text.count('\n',0,match.start())+1,literal_argument=value,status='literal-macro-argument-evidence' if value is not None else 'unresolved-macro-arguments',wire_status='unknown'))
 return rows
def registries(text):
 clean=clean_comments(text);rows=[]
 for match in re.finditer(r'\b(kManualInterfaces|kDownstreamManualInterfaces)\s*\[\s*\]\s*=\s*\{([^}]*)\}',clean):
  body=match.group(2);names=[]
  for literal in re.finditer(r'"(?:\\.|[^"\\])*"',body):
   try:names.append(json.loads(literal.group()))
   except ValueError:pass
  residue=re.sub(r'"(?:\\.|[^"\\])*"','',body).replace(',','').strip();rows.append(dict(name=match.group(1),literal_names=names,literal_only=not residue,unresolved_residue=residue,line=text.count('\n',0,match.start())+1))
 return rows
def reference_names(rows):
 # Versioned copies in prebuilts remain separate evidence, never merged into
 # the current platform/frameworks/native registry.
 selected=[r for r in rows if r['source']['repository']=='platform/frameworks/native' and r['source']['path']=='libs/binder/include/binder/IInterface.h']
 return {name for r in selected for name in r['literal_names']}
def main(root):
 root=pathlib.Path(root);paths=['source-inventory-batchnative-search303-acquired002.json','source-inventory-batchnative-manual-v2-acquired002.json'];seen=set();facts=[];registry=[]
 for name in paths:
  for s in json.loads((root/'outside-corpus-audit'/name).read_text())['sources']:
   key=(s['repository'],s['revision'],s['path'])
   if key in seen:continue
   seen.add(key);raw=(root/s['local_path']).read_bytes();assert hashlib.sha256(raw).hexdigest()==s['sha256'];text=raw.decode('utf-8',errors='strict');source={k:s[k] for k in ('repository','revision','path','sha256')}
   facts.extend(dict(source=source,**r) for r in declarations(text));registry.extend(dict(source=source,**r) for r in registries(text))
 names=reference_names(registry);observed={r['literal_argument'] for r in facts if r['literal_argument'] is not None};report=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),audited_source_paths=len(seen),query_families=list(FAMILIES),literal_macro_candidates=facts,registries=registry,reference_registry='platform/frameworks/native/libs/binder/include/binder/IInterface.h at pinned revision; prebuilt versioned registries retained separately',allowlist_literal_count=len(names),observed_literal_count=len(observed),allowlist_not_observed=sorted(names-observed),observed_not_allowlisted=sorted(observed-names),semantic_complete=False,limitations=['Lexical evidence only, no C++ preprocessing, overload or ABI inference','Candidate cohort contains prebuilt versioned copies; cross-registry differences are discovery leads, not current-interface contradiction proof','Unmatched/computed macros and NDK/Rust declarations remain unsupported','Allowlists do not prove shipping/runtime service registration','Allowlist discrepancy requires source reconciliation, not automatic identity merging'])
 out=root/'outside-corpus-audit/native-literal-registry-reconciliation-v2.json';out.write_text(json.dumps(report,indent=2));print(json.dumps({k:v for k,v in report.items() if k not in ('literal_macro_candidates','registries')},indent=2))
if __name__=='__main__':main(pathlib.Path(__file__).resolve().parent)
