import pathlib,json,hashlib,datetime
R=pathlib.Path(__file__).resolve().parent;results=[]
sha=lambda b:hashlib.sha256(b).hexdigest()
for d in sorted((R/'reviews').glob('round-*')):
 request=json.loads((d/'request.jsonl').read_text().splitlines()[0]);manifest=json.loads((d/'input-manifest.json').read_text())
 assert request['prompt_sha256']==sha(request['prompt'].encode())
 assert request['manifest_sha256']==sha((d/'input-manifest.json').read_bytes())
 for item in manifest['files']:
  path=item.get('file',item.get('path'))
  assert isinstance(path,str) and path
  assert item['sha256']==sha((d/'input'/path).read_bytes()),str(d/path)
 events=[json.loads(line) for line in (d/'response.visible.jsonl').read_text().splitlines()]
 assert not any(block.get('type') in ('thinking','redacted_thinking') for event in events for block in event.get('message',{}).get('content',[]))
 completion=json.loads((d/'completion.json').read_text()) if (d/'completion.json').exists() else None
 if completion:assert completion['visible_response_sha256']==sha((d/'response.visible.jsonl').read_bytes())
 init=next((event for event in events if event.get('subtype')=='init'),{})
 parsed=next((d/name for name in ('review-report.json','review-report.reassembled.json','review-report.parsed.json','review-report.repaired.json') if (d/name).exists()),None)
 results.append(dict(round=d.name,input_files=len(manifest['files']),input_hashes_match=True,request_hashes_match=True,hidden_reasoning_excluded=True,returned_model=init.get('model'),returned_tools=init.get('tools'),requested_model=request['requested_model'],requested_effort=request['requested_effort'],finished=completion is not None,exit_code=completion.get('exit_code') if completion else None,complete_derived_report=parsed is not None and json.loads(parsed.read_text()).get('report_complete') is True,parsed_report=str(parsed.relative_to(R)) if parsed else None))
report=dict(audited_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),rounds=results)
(R/'evidence/review-receipt-audit.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
