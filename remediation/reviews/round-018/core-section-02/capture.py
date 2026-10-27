import pathlib,json,hashlib,subprocess,datetime
R=pathlib.Path('/home/sharpninja/github/2026-10-26/task-3/remediation');D=pathlib.Path('/home/sharpninja/github/2026-10-26/task-3/remediation/reviews/round-018/core-section-02');number='core-section-02'
now=lambda:datetime.datetime.now(datetime.timezone.utc).isoformat()
sha=lambda b:hashlib.sha256(b).hexdigest()
prompt=(D/'prompt.txt').read_text()
cli='/home/sharpninja/.local/share/mise/installs/claude/2.1.300/claude'

args=[cli,'--safe-mode','--strict-mcp-config','--mcp-config','{"mcpServers":{}}','--tools','','--permission-mode','dontAsk','--no-session-persistence','--print','--output-format','stream-json','--verbose','--model','claude-fable-5-1','--effort','max','--max-turns','1']
version=subprocess.run([cli,'--version'],capture_output=True,text=True,check=True).stdout.strip()
request=dict(event='request',started_at=now(),invocation=args,cli_version=version,requested_model='claude-fable-5-1',requested_effort='max',tools=[],prompt_sha256=sha(prompt.encode()),manifest_sha256=sha((D/'input-manifest.json').read_bytes()),prompt=prompt)
(D/'request.jsonl').write_text(json.dumps(request)+'\n')
texts=[];models=set();result=None;hidden=0
with (D/'stderr.txt').open('x') as err,(D/'response.visible.jsonl').open('x') as visible:
 p=subprocess.Popen(args,cwd=D,stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=err,text=True)
 (D/'process.json').write_text(json.dumps(dict(pid=p.pid,status='running',started_at=request['started_at'],invocation=args),indent=2));print('Review launched',number,'PID',p.pid,flush=True)
 p.stdin.write(prompt);p.stdin.close()
 for line in p.stdout:
  try:event=json.loads(line)
  except ValueError:
   visible.write(json.dumps(dict(type='unparsed-visible-line',text=line))+'\n');visible.flush();continue
  if event.get('type')=='assistant':
   message=event.get('message',{});model=message.get('model')
   if model:models.add(model)
   blocks=message.get('content',[]);hidden+=sum(b.get('type') in ('thinking','redacted_thinking') for b in blocks)
   message['content']=[b for b in blocks if b.get('type') not in ('thinking','redacted_thinking')]
   for b in message['content']:
    if b.get('type')=='text':texts.append(b['text']);(D/'review-report.txt').write_text('\n\n'.join(texts))
  if event.get('type')=='result':result=event;models.update(event.get('modelUsage',{}))
  visible.write(json.dumps(event)+'\n');visible.flush()
 code=p.wait()
if result and result.get('result') and result['result'] not in texts:texts.append(result['result'])
(D/'review-report.txt').write_text('\n\n'.join(texts));report=None
for candidate in [''.join(texts)]+list(reversed(texts)):
 try:report=json.JSONDecoder().raw_decode(candidate[candidate.index('{'):])[0];break
 except (ValueError,IndexError):pass
if report is not None:(D/'review-report.json').write_text(json.dumps(report,indent=2))
completion=dict(event='completed',completed_at=now(),exit_code=code,actual_models=sorted(models),requested_effort='max',invocation=args,cli_version=version,visible_response_sha256=sha((D/'response.visible.jsonl').read_bytes()),report_sha256=sha((D/'review-report.txt').read_bytes()),report_complete=isinstance(report,dict) and report.get('report_complete') is True,hidden_blocks_excluded=hidden,result_metadata={k:v for k,v in (result or {}).items() if k!='result'})
(D/'completion.json').write_text(json.dumps(completion,indent=2));(D/'process.json').write_text(json.dumps(dict(pid=p.pid,status='finished',**completion),indent=2))
print(json.dumps({k:v for k,v in completion.items() if k not in ('result_metadata','invocation')}),flush=True)
