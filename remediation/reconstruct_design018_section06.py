"""Encoding-only recovery; preserve all original model bytes and every word."""
import pathlib,json,hashlib,datetime
R=pathlib.Path(__file__).resolve().parent;D=R/'reviews/round-018/core-section-06'
source=D/'review-report.txt';raw=source.read_text();start=raw.index('{');end=raw.rindex('\n```')
fragment=raw[start:end].strip();inside=False;escaped=False;out=[];changes=[]
for index,char in enumerate(fragment):
 if inside and not escaped and ord(char)<32:
  replacement=json.dumps(char)[1:-1];out.append(replacement);changes.append(dict(character_offset=index,codepoint=ord(char),replacement=replacement));continue
 out.append(char)
 if escaped:escaped=False;continue
 if inside and char=='\\':escaped=True
 elif char=='"':inside=not inside
repaired=''.join(out);report=json.loads(repaired)
if report.get('report_complete') is not True or not isinstance(report.get('findings'),list):raise ValueError('Full report content not verified')
ids=[v['id'] for v in report['findings']]
if len(ids)!=len(set(ids)):raise ValueError('Duplicate finding ID')
for finding in report['findings']:
 for key in ('id','severity','artifact','evidence','issue','exact_minimal_correction','verification_needed'):
  if key not in finding:raise ValueError('Missing material finding field '+key)
sha=lambda b:hashlib.sha256(b).hexdigest()
target=D/'review-report.recovered.json'
with target.open('x') as f:f.write(json.dumps(report,indent=2)+'\n')
audit=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),original_sha256=sha(source.read_bytes()),recovered_sha256=sha(target.read_bytes()),json_character_range=[start,end],outside_json_preserved_in_original=True,only_transform='Escape raw control characters occurring inside JSON strings; no words removed or invented. An internal markdown fence remains literal string content.',changes=changes,findings=len(ids),unique_ids=True,model_report_complete=True,original_cli_exit=0,original_parser_report_complete=False,original_files_unmodified=True)
with (D/'report-reconstruction.json').open('x') as f:f.write(json.dumps(audit,indent=2)+'\n')
print(json.dumps(dict(findings=len(ids),encoding_repairs=len(changes),report_complete=True,recovered=str(target))))
