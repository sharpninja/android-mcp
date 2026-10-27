import pathlib,json,collections,hashlib,datetime
R=pathlib.Path(__file__).resolve().parent;E=R/'evidence';p=E/'built-in-validation-release005.json';raw=p.read_bytes();data=json.loads(raw);reports=data.pop('source_reports');groups={};mismatches=[]
for report in reports:
 key=report['kind'];group=groups.setdefault(key,dict(source_reports=0,statuses={},numeric_fields={},other_values={}))
 group['source_reports']+=1;state=report['status'];group['statuses'][state]=group['statuses'].get(state,0)+1
 for name,value in report.items():
  if name in ('source_id','kind','status'):continue
  if isinstance(value,(int,float)) and not isinstance(value,bool):
   aggregate=group['numeric_fields'].setdefault(name,dict(total=0,min=value,max=value));aggregate['total']+=value;aggregate['min']=min(aggregate['min'],value);aggregate['max']=max(aggregate['max'],value)
  else:
   values=group['other_values'].setdefault(name,[])
   if value not in values:values.append(value)
 if report.get('expected_declarations') is not None and report['expected_declarations']!=report['declarations']:mismatches.append(report)
summary=dict(**data,report_count=len(reports),source_reports_by_kind=groups,expected_declaration_mismatches=mismatches,full_report_sha256=hashlib.sha256(raw).hexdigest(),full_report_bytes=len(raw),scope_note='Progress summary of every native per-source report: all top-level checks, all kind/status counts, every numeric field total/range, every other distinct value and all expected-count mismatches. Opaque per-source IDs and individual rows remain in the full immutable local report, not the model prompt. Independent validation/export and source-reconciliation reports are supplied in full.')
(E/'built-in-validation-summary-release005.json').write_text(json.dumps(summary,indent=2))
D=R/'reviews/round-006';error=(D/'stderr.txt').read_text();completion=dict(event='completed-with-technical-launch-failure',completed_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),wrapper_exit_code=1,child_exit_code=None,actual_models=[],requested_model='claude-fable-5-1',requested_effort='max',report_complete=False,visible_response_sha256=hashlib.sha256((D/'response.visible.jsonl').read_bytes()).hexdigest(),provider_events=0,reason=error.strip(),wrapper_exception='BrokenPipeError while sending prompt',child_not_running_confirmed=True,authorization_denied=False)
if (D/'completion.json').exists():raise SystemExit('Do not replace a recorded outcome')
(D/'completion.json').write_text(json.dumps(completion,indent=2));process=json.loads((D/'process.json').read_text());process.update(status='finished-technical-failure',outcome='CLI stdin exceeds10MB',wrapper_exit_code=1);(D/'process.json').write_text(json.dumps(process,indent=2))
status=json.loads((R/'LIVE-STATUS.json').read_text());status.update(updated_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),current_phase='006 launch technically failed before provider events:CLI10MBstdin cap; full failed snapshot preserved, preparing007 progress-summary payload',review_pid=None,failed_review='reviews/round-006/completion.json',new_library_uploads='Not attempted per parent instruction; source upload approval remains parent-managed');(R/'LIVE-STATUS.json').write_text(json.dumps(status,indent=2));print(json.dumps(dict(native_summary_bytes=(E/'built-in-validation-summary-release005.json').stat().st_size,source_reports=len(reports),mismatches=len(mismatches))))
