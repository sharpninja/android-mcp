from pathlib import Path
import os,sys,json,hashlib,datetime,subprocess
R=Path('/home/sharpninja/github/2026-10-26/task-3/remediation');D=Path('/home/sharpninja/github/2026-10-26/task-3/remediation/reviews/round-022/pass-02');number='pass-02'
now=lambda:datetime.datetime.now(datetime.timezone.utc).isoformat()
sha=lambda b:hashlib.sha256(b).hexdigest()
prompt=(D/"prompt.txt").read_text()
manifest={"controls":{'assistant_elapsed_limit': None, 'assistant_prompt_size_limit': None, 'assistant_report_word_limit': None, 'assistant_turn_limit': None, 'assistant_aggregate_output_limit': None, 'requested_per_response_output_tokens': 128000, 'provider_reported_prior_context_window': 1000000, 'provider_reported_prior_max_output_tokens': 64000, 'provider_limits_are_not_review_completion': True, 'tool_display': 'Only small process metadata is printed to orchestration UI; complete visible content is written without truncation'}}
rows=[];texts=[]
cli='/home/sharpninja/.local/share/mise/installs/claude/2.1.300/claude'
args=[cli,'--safe-mode','--strict-mcp-config','--mcp-config','{"mcpServers":{}}','--tools','','--permission-mode','dontAsk','--no-session-persistence','--print','--output-format','stream-json','--verbose','--model','claude-fable-5-1','--effort','max']
overrides={'CLAUDE_CODE_MAX_OUTPUT_TOKENS':'128000'}
version=subprocess.run([cli,'--version'],capture_output=True,text=True,check=True).stdout.strip()
request=dict(event='request',started_at=now(),invocation=args,environment_overrides=overrides,cli_version=version,requested_model='claude-fable-5-1',requested_effort='max',tools=[],prompt_sha256=sha(prompt.encode()),manifest_sha256=sha((D/'input-manifest.json').read_bytes()),prompt=prompt,assistant_controls=manifest['controls'])
(D/'request.jsonl').write_text(json.dumps(request)+'\n')
texts_out=[];models=set();result=None;hidden=0
with (D/'stderr.txt').open('x') as err,(D/'response.visible.jsonl').open('x') as visible:
    child=subprocess.Popen(args,cwd=D,stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=err,text=True,env=dict(os.environ,**overrides))
    (D/'process.json').write_text(json.dumps(dict(pid=child.pid,status='running',started_at=request['started_at'],invocation=args,environment_overrides=overrides),indent=2)+'\n')
    print(json.dumps(dict(launched_at=request['started_at'],pid=child.pid,manifest_sha256=request['manifest_sha256'],prompt_bytes=len(prompt.encode()),files=len(rows),distinct_full_text_files=len(texts),assistant_caps=None,requested_output_tokens=128000,tools=[])),flush=True)
    child.stdin.write(prompt);child.stdin.close()
    for line in child.stdout:
        try:event=json.loads(line)
        except ValueError:
            visible.write(json.dumps(dict(type='unparsed-visible-line',text=line))+'\n');visible.flush();continue
        if event.get('type')=='assistant':
            message=event.get('message',{});model=message.get('model')
            if model:models.add(model)
            blocks=message.get('content',[]);hidden+=sum(b.get('type') in ('thinking','redacted_thinking') for b in blocks)
            message['content']=[b for b in blocks if b.get('type') not in ('thinking','redacted_thinking')]
            for block in message['content']:
                if block.get('type')=='text':texts_out.append(block['text']);(D/'review-report.txt').write_text('\n\n'.join(texts_out))
        if event.get('type')=='result':result=event;models.update(event.get('modelUsage',{}))
        visible.write(json.dumps(event)+'\n');visible.flush()
    code=child.wait()
if result and result.get('result') and result['result'] not in texts_out:texts_out.append(result['result'])
text='\n\n'.join(texts_out);(D/'review-report.txt').write_text(text)
completion=dict(event='completed',completed_at=now(),exit_code=code,actual_models=sorted(models),requested_effort='max',invocation=args,environment_overrides=overrides,cli_version=version,visible_response_sha256=sha((D/'response.visible.jsonl').read_bytes()),report_sha256=sha(text.encode()),hidden_blocks_excluded=hidden,result_metadata={k:v for k,v in (result or {}).items() if k!='result'},report_complete=bool(text.strip().rstrip('`').strip().endswith('END_DESIGN_STAGE_'+number) and code==0 and not (result or {}).get('is_error') and (result or {}).get('stop_reason')=='end_turn'),no_assistant_caps=True)
(D/'completion.json').write_text(json.dumps(completion,indent=2)+'\n');(D/'process.json').write_text(json.dumps(dict(pid=child.pid,status='finished',**completion),indent=2)+'\n')
print(json.dumps({k:v for k,v in completion.items() if k not in ('invocation','result_metadata')}),flush=True)
