"""Full declared HIDL corpus audit of an isolated structural parser candidate."""
import pathlib,sys,json,hashlib,gzip,datetime,collections,re
R=pathlib.Path(__file__).resolve().parent;P=R/'hidl-normalizer-candidate-v2/tooling';sys.path.insert(0,str(P));from hidl_source import parse
from aidl_lex import code_mask
D=R/'outside-corpus-audit/hidl-structural-audit-v2'
if D.exists():raise FileExistsError('Preserve existing candidate audit')
D.mkdir();sources=json.loads((R/'outside-corpus-audit/source-inventory-batchhidl1087-acquired001.json').read_text())['sources'];counts=collections.Counter();reports=[];missing=[]
with gzip.open(D/'declarations.jsonl.gz','wt',encoding='utf-8') as out:
 for index,s in enumerate(sources,1):
  raw=(R/s['local_path']).read_bytes();assert hashlib.sha256(raw).hexdigest()==s['sha256'];text=raw.decode('utf-8');parsed=parse(text);source={k:s[k] for k in ('repository','revision','path','sha256')};normalized={r['extras']['keyword_character_offset'] for r in parsed['records'] if 'keyword_character_offset' in r['extras']};lexical={m.start() for m in re.finditer(r'\b(?:interface|struct|safe_union|union|enum)\s+[A-Za-z_]\w*\b[^{};()]*\{',code_mask(text))};unresolved=sorted(lexical-normalized)
  for r in parsed['records']:out.write(json.dumps(dict(source=source,record=r),sort_keys=True,ensure_ascii=False)+'\n');counts[r['kind']]+=1
  report=dict(source=source,records=len(parsed['records']),unsupported_statements=len(parsed['unsupported']),structural_statement_parse_complete=parsed.get('structural_statement_parse_complete',False),lexical_type_declarations=len(lexical),normalized_type_declarations=len(normalized),unresolved_lexical_type_declarations=[dict(character_offset=pos,line=text.count('\n',0,pos)+1) for pos in unresolved],compiler_validation=False,android_availability='unknown')
  reports.append(report)
  if unresolved:missing.append(report)
  if index%100==0:print('HIDL candidate paths',index,'/',len(sources),'records',sum(counts.values()),'unresolved types',sum(len(x['unresolved_lexical_type_declarations']) for x in missing),flush=True)
result=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),candidate_code_sha256={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in P.glob('*.py')},paths=len(sources),records=sum(counts.values()),by_kind=dict(counts),unsupported_statements=sum(r['unsupported_statements'] for r in reports),paths_without_unsupported_statements=sum(r['structural_statement_parse_complete'] for r in reports),lexical_type_declarations=sum(r['lexical_type_declarations'] for r in reports),normalized_type_declarations=sum(r['normalized_type_declarations'] for r in reports),unresolved_lexical_type_declarations=sum(len(r['unresolved_lexical_type_declarations']) for r in reports),source_reports=reports,normalizer_complete=False,compiler_validation=False,global_complete=False,limitations=['All962 selected paths audited, including compiler fixtures and malformed sources','Source structure only; type/import resolution and inherited methods remain incomplete','No compiled descriptor, transaction number, codec or running Android applicability asserted','This isolated candidate is not part of running release010'])
(D/'report.json').write_text(json.dumps(result,indent=2));print(json.dumps({k:v for k,v in result.items() if k!='source_reports'},indent=2))
