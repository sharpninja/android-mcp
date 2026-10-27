from pathlib import Path
import re,json,datetime
R=Path(__file__).resolve().parents[1];D=R.parent/'publication/android-mcp-git';rx=re.compile(rb'"type"\s*:\s*"(?:thinking|redacted_thinking)"');hits=[];count=0;size=0
for p in (D/'remediation').rglob('*'):
 if not p.is_file():continue
 count+=1;raw=p.read_bytes();size+=len(raw)
 if rx.search(raw):hits.append(dict(file=str(p.relative_to(D)),category='potential_hidden_reasoning_structured_block; content not printed'))
result=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),files_scanned=count,bytes_scanned=size,potential_hidden_blocks=hits,pass_=not hits,scope='Normal sealed checkpoint; prior secret-signature scan performed during staging. Values never printed.')
(R/'publication-plan/checkpoint-receipt-privacy-scan.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result))
raise SystemExit(1 if hits else 0)
