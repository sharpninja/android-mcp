"""Reserve only an unstarted scheduling slot; never interrupt active Claude calls."""
from pathlib import Path
import json,datetime,os
R=Path(__file__).resolve().parent;D=R/'reviews/round-022';now=lambda:datetime.datetime.now(datetime.timezone.utc).isoformat()
reserved=[];active=[]
for stage in ('pass-03','synthesis'):
    p=D/stage
    try:p.mkdir()
    except FileExistsError:
        if (p/'process.json').exists():active.append(dict(stage=stage,process=json.loads((p/'process.json').read_text())))
        continue
    (p/'supervisor-reservation.json').write_text(json.dumps(dict(at=now(),reason='Explicit user instruction: transfer future scheduling to preflight-checked automatic continuation supervisor; no API request/process exists in this slot',active_calls_preserved=True),indent=2)+'\n');reserved.append(stage)
receipt=dict(at=now(),reserved_unstarted_slots=reserved,existing_slots_not_touched=active,active_pass01_pass02_preserved=True,original_coordinator_behavior='Its mkdir(exist_ok=False) guard prevents a duplicate request in a reserved slot; all current CLI capture processes remain running. Supervisor will own these slots after regression preflight.',new_inference_launched=False)
(D/'supervisor-handoff.json').write_text(json.dumps(receipt,indent=2)+'\n');print(json.dumps(receipt))
