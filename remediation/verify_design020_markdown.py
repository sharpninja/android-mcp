"""Verify a complete human-readable Claude report; never create or change scores."""
from pathlib import Path
import json,re,hashlib,datetime,sys,time
R=Path(__file__).resolve().parent;D=R/'reviews/round-020'
if '--wait' in sys.argv:
    while not (D/'completion.json').exists():time.sleep(2)
c=json.loads((D/'completion.json').read_text());text=(D/'review-report.txt').read_text();plain=text.replace('*','');source_sha=hashlib.sha256(text.encode()).hexdigest()
def extract(label):
    m=re.search(label+r'\s*[:=]\s*(\d+(?:\.\d+)?)\s*/\s*100',plain,re.I)
    if not m:return None
    n=float(m.group(1));return n if 0<=n<=100 else None
a=extract(r'DESIGN\s+ACCURACY');b=extract(r'DESIGN\s+COMPLETENESS')
head='\n'.join(plain.splitlines()[:35]);verdict=re.search(r'\b(NOT_ACCEPTED|ACCEPTED|NOT ACCEPTED)\b',head)
v=verdict.group(1).replace(' ','_') if verdict else None
end=text.strip().rstrip('`').strip().endswith('END_DESIGN_REVIEW_020')
result=c.get('result_metadata',{});complete=bool(a is not None and b is not None and v and end and c['exit_code']==0 and not result.get('is_error') and result.get('stop_reason')=='end_turn')
assessment=dict(verified_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),report_format='Markdown requested by the review prompt',source_report='review-report.txt',source_report_sha256=source_sha,original_completion_preserved=True,original_json_parser_report_complete=c.get('report_complete'),report_complete=complete,scores={'design':{'accuracy_0_100':a,'completeness_0_100':b}},verdict=v,required_end_marker_present=end,actual_models=c['actual_models'],requested_effort=c['requested_effort'],terminal_reason=result.get('terminal_reason'),stop_reason=result.get('stop_reason'),acceptance_not_automatically_acted_on=True,method='Extract only literal score headers and verdict from the complete visible model report; no inferred or author-assigned quality score')
with (D/'completion.markdown.json').open('x') as f:f.write(json.dumps(assessment,indent=2)+'\n')
print(json.dumps(assessment));raise SystemExit(0 if complete else 1)
