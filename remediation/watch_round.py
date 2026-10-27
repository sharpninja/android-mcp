"""Bounded observation of an existing receipt stream; never launches a reviewer."""
import datetime,json,pathlib,sys,time
R=pathlib.Path(__file__).resolve().parent
D=R/'reviews'/('round-'+sys.argv[1].zfill(3))
def state():
    rows=(D/'response.visible.jsonl').read_text().splitlines();events=[]
    for row in rows:
        try:events.append(json.loads(row))
        except ValueError:pass
    messages=[e for e in events if e.get('type')=='assistant' and any(b.get('type')=='text' and b.get('text') for b in e.get('message',{}).get('content',[]))]
    result=dict(observed_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),round=D.name,events=len(events),visible_messages=len(messages),stderr_bytes=(D/'stderr.txt').stat().st_size,finished=(D/'completion.json').exists())
    if result['finished']:result['completion']=json.loads((D/'completion.json').read_text())
    return result
initial=state()
if not initial['finished']:
    deadline=time.monotonic()+45
    while time.monotonic()<deadline:
        time.sleep(min(5,max(0,deadline-time.monotonic())))
        if (D/'completion.json').exists():break
observed=state()
live_path=R/'LIVE-STATUS.json'
if live_path.exists():
    live=json.loads(live_path.read_text())
    live.update(updated_at=observed['observed_at'],review_events=observed['events'],review_visible_assistant_messages=observed['visible_messages'],review_error_log_empty=observed['stderr_bytes']==0,
                review_phase='Complete receipt recovered; inspect exact report before remediation' if observed['finished'] else 'Provider review active; no complete verdict yet')
    if observed['finished']:
        live['review_pid']=None
        live['current_phase']=D.name+' process finished; inspect exact report and remediate findings'
        report_path=D/'review-report.json'
        if report_path.exists():
            report=json.loads(report_path.read_text())
            if report.get('report_complete') is True:
                live.update(latest_review_scores=report['scores'],latest_review_overall=report['overall'],latest_review_directory=str(D))
                live['production_scores_scope']='Latest completed independent review assesses the production evidence frozen in '+D.name+'; later working-copy changes are not included.'
                observed.update(scores=report['scores'],overall=report['overall'],findings=len(report.get('findings',[])))
    live_path.write_text(json.dumps(live,indent=2)+'\n')
print(json.dumps(observed))
