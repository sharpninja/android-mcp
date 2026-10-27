"""Escape unescaped embedded code quotes only; preserve reviewer text and scores."""
import pathlib,json,re,hashlib,datetime
R=pathlib.Path(__file__).resolve().parent;D=R/'reviews/round-009';source=D/'review-report.txt';raw=source.read_text();payload=raw[raw.index('{'):raw.rfind('}')+1];lines=payload.splitlines(True);edits=[]
for i,line in enumerate(lines):
 m=re.match(r'^(\s*"[^"\n]+"\s*:\s*")(.*)("\s*,?\s*)$',line.rstrip('\n'))
 if not m:continue
 content=m[2];fixed=[];slashes=0;positions=[]
 for j,char in enumerate(content):
  if char=='"' and slashes%2==0:fixed.append('\\');positions.append(j)
  fixed.append(char)
  slashes=slashes+1 if char=='\\' else 0
 if positions:
  replacement=m[1]+''.join(fixed)+m[3]+('\n' if line.endswith('\n') else '');lines[i]=replacement
  edits.append(dict(json_line=i+1,internal_quote_positions=positions,operation='Insert JSON escape before literal embedded code quote; reviewer wording unchanged'))
fixed=''.join(lines);report=json.loads(fixed)
if report.get('report_complete') is not True:raise ValueError('Incomplete report')
target=D/'review-report.repaired.json';target.write_text(json.dumps(report,indent=2)+'\n')
proof=dict(recovered_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),source=str(source),source_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),derived=str(target),derived_sha256=hashlib.sha256(target.read_bytes()).hexdigest(),edits=edits,scope='JSON syntax repair only. Original visible report/receipts/completion remain unchanged; no scores, verdicts or finding words altered. This review predates startup-device-view requirement.')
(D/'report-extraction.json').write_text(json.dumps(proof,indent=2)+'\n')
state=json.loads((R/'LIVE-STATUS.json').read_text());state.update(updated_at=proof['recovered_at'],review_pid=None,latest_review_directory=str(D),latest_review_scores=report['scores'],latest_review_overall=report['overall'],review_phase='009completed; exact report reconstructed with JSONquoteescape receipt',new_requirement_scope='Startupdevice-view+genericBinderdesign revision addedafter009freeze;009doesnotassessit',current_phase='Updated designDOCXrendering validated; resolver13tests pass; preparing guardeddesign-onlyLibraryreplacements and revisedHV')
(R/'LIVE-STATUS.json').write_text(json.dumps(state,indent=2)+'\n');print(json.dumps(dict(scores={k:[v['accuracy_0_100'],v['completeness_0_100']] for k,v in report['scores'].items()},overall=report['overall'],repaired_lines=len(edits))))
