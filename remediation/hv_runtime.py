"""Preflight-enforced full-capture transport for unbounded HV continuations."""
from pathlib import Path
import os,sys,json,hashlib,datetime,subprocess,time
from hv_invariants import preflight,visible_text
ROOT=Path(__file__).resolve().parent
CLI='/home/sharpninja/.local/share/mise/installs/claude/2.1.300/claude'
ARGS=[CLI,'--safe-mode','--strict-mcp-config','--mcp-config','{"mcpServers":{}}','--tools','','--permission-mode','dontAsk','--no-session-persistence','--print','--output-format','stream-json','--verbose','--model','claude-fable-5-1','--effort','max']
ENV={'CLAUDE_CODE_MAX_OUTPUT_TOKENS':'128000'}
now=lambda:datetime.datetime.now(datetime.timezone.utc).isoformat()
sha=lambda b:hashlib.sha256(b).hexdigest()

def read_events(path):return [json.loads(line) for line in path.read_text().splitlines()] if path.exists() else []

def capture(target,instruction,units,marker,parent_sha,extra='',context_partition=False):
    target.mkdir(parents=True,exist_ok=False)
    keys=[u['key'] for u in units]
    policy=preflight(ARGS,ENV,instruction,keys,keys,Path(__file__).read_text(),supervised=True)
    pieces=[]
    for u in units:
        body=u['text'];meta={k:v for k,v in u.items() if k!='text'};meta['body_sha256']=sha(body.encode())
        pieces.append('\nBEGIN EXACT EVIDENCE '+json.dumps(meta)+'\n'+body+'\nEND EXACT EVIDENCE '+u['key']+'\n')
    prompt=instruction+'\n'+extra+'\n'+''.join(pieces)
    for piece in pieces:
        if piece not in prompt:raise ValueError('Evidence omitted from prompt')
    (target/'prompt.txt').write_text(prompt)
    (target/'input-manifest.json').write_text(json.dumps(dict(parent_sha256=parent_sha,units=[{k:v for k,v in u.items() if k!='text'} for u in units],context_partition=context_partition,policy=policy),indent=2)+'\n')
    version=subprocess.run([CLI,'--version'],capture_output=True,text=True,check=True).stdout.strip()
    req=dict(event='request',started_at=now(),invocation=ARGS,environment_overrides=ENV,cli_version=version,requested_model='claude-fable-5-1',requested_effort='max',tools=[],prompt=prompt,prompt_sha256=sha(prompt.encode()),manifest_sha256=sha((target/'input-manifest.json').read_bytes()),invariant=policy)
    (target/'request.jsonl').write_text(json.dumps(req)+'\n')
    models=set();result=None;hidden=0;has_text=False
    with (target/'stderr.txt').open('x') as err,(target/'response.visible.jsonl').open('x') as out,(target/'review-report.txt').open('x') as report:
        child=subprocess.Popen(ARGS,cwd=target,stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=err,text=True,env=dict(os.environ,**ENV))
        (target/'process.json').write_text(json.dumps(dict(pid=child.pid,capture_pid=os.getpid(),status='running',started_at=req['started_at'],invocation=ARGS,environment_overrides=ENV),indent=2)+'\n')
        child.stdin.write(prompt);child.stdin.close()
        for line in child.stdout:
            try:event=json.loads(line)
            except ValueError:event=dict(type='unparsed-visible-line',text=line)
            if event.get('type')=='assistant':
                message=event.get('message',{});model=message.get('model')
                if model:models.add(model)
                blocks=message.get('content',[]);hidden+=sum(b.get('type') in ('thinking','redacted_thinking') for b in blocks)
                message['content']=[b for b in blocks if b.get('type') not in ('thinking','redacted_thinking')]
                for block in message['content']:
                    if block.get('type')=='text':
                        if has_text:report.write('\n\n')
                        report.write(block['text']);report.flush();has_text=True
            if event.get('type')=='result':result=event;models.update(event.get('modelUsage',{}))
            out.write(json.dumps(event)+'\n');out.flush()
        code=child.wait()
        if not has_text and (result or {}).get('result'):report.write(result['result']);report.flush()
    text=(target/'review-report.txt').read_text()
    done=dict(event='completed',completed_at=now(),exit_code=code,terminal=True,actual_models=sorted(models),requested_effort='max',invocation=ARGS,environment_overrides=ENV,visible_response_sha256=sha((target/'response.visible.jsonl').read_bytes()),report_sha256=sha(text.encode()),hidden_blocks_excluded=hidden,result_metadata={k:v for k,v in (result or {}).items() if k!='result'},report_complete=bool(text.strip().rstrip('`').strip().endswith(marker) and code==0 and not (result or {}).get('is_error') and (result or {}).get('stop_reason')=='end_turn'),no_assistant_caps=True)
    if result is None:
        stderr=(target/'stderr.txt').read_text()
        if any(x in stderr.lower() for x in ('not found','permission denied','authentication','not available','unknown option')):done['result_metadata'].update(terminal_reason='capability_or_authentication_error',is_error=True)
    (target/'completion.json').write_text(json.dumps(done,indent=2)+'\n');(target/'process.json').write_text(json.dumps(dict(pid=child.pid,capture_pid=os.getpid(),status='finished',**done),indent=2)+'\n')
    return read_events(target/'response.visible.jsonl'),done

def alive(pid):
    try:os.kill(int(pid),0);return True
    except ProcessLookupError:return False
    except PermissionError:return None

def only_output_environment(pid):
    """Select one known nonsecret control variable; never dump other environment values."""
    prefix=b'CLAUDE_CODE_MAX_OUTPUT_TOKENS=';value=None
    with open(f'/proc/{pid}/environ','rb') as stream:
        while True:
            head=bytearray()
            while len(head)<len(prefix):
                c=stream.read(1)
                if not c:return value
                if c==b'\0':break
                head.extend(c)
            if bytes(head)==prefix:
                data=bytearray()
                while True:
                    c=stream.read(1)
                    if not c or c==b'\0':break
                    data.extend(c)
                value=data.decode()
            elif head and (not c or c!=b'\0'):
                while True:
                    c=stream.read(1)
                    if not c or c==b'\0':break

def audit_active(round_dir,output):
    root=json.loads((round_dir/'input-manifest.json').read_text());expected={x['file'] for x in root['files']}-{x['file'] for x in root['byte_identical_aliases']}-{x['file'] for x in root['binary_dependencies']};assigned=[x['file'] for group in root['passes'].values() for x in group]
    checks=[];violations=[]
    preflight(ARGS,ENV,'Unbounded review',assigned,expected,ROOT.joinpath('review_design_unbounded.py').read_text(),supervised=True)
    for p in sorted(round_dir.glob('pass-*/process.json')):
        state=json.loads(p.read_text());pid=state['pid'];row=dict(stage=p.parent.name,pid=pid,status=state.get('status'))
        if state.get('status')=='running' and alive(pid):
            raw=Path(f'/proc/{pid}/cmdline').read_bytes().split(b'\0')
            if raw and raw[-1]==b'':raw.pop()
            actual=[x.decode() for x in raw] # preserve --tools followed by its empty argument
            selected=only_output_environment(pid)
            preflight(actual,{'CLAUDE_CODE_MAX_OUTPUT_TOKENS':selected},'Unbounded review',[],[],supervised=True)
            row.update(actual_arguments=actual,actual_selected_environment={'CLAUDE_CODE_MAX_OUTPUT_TOKENS':selected},other_environment_values_never_recorded=True)
        prompt=p.parent/'prompt.txt';text=prompt.read_text()
        for match in __import__('re').finditer(r'\nBEGIN FULL ARTIFACT (\{[^\n]+\})\n(.*?)\nEND ARTIFACT ([^\n]+)\n',text,__import__('re').S):
            meta=json.loads(match.group(1));body=match.group(2)
            if sha(body.encode())!=meta['sha256']:violations.append(p.parent.name+': full artifact mismatch '+meta['file'])
        req=json.loads((p.parent/'request.jsonl').read_text())
        if req['prompt_sha256']!=sha(prompt.read_bytes()):violations.append('Request prompt mismatch '+p.parent.name)
        checks.append(row)
    source_files=['review_design_unbounded.py','recover_design022.py','hv_invariants.py','hv_runtime.py','hv_supervisor.py']
    sources={name:sha((ROOT/name).read_bytes()) for name in source_files}
    preflight(ARGS,ENV,'Unbounded review',[],[],(ROOT/'recover_design022.py').read_text(),supervised=True)
    result=dict(at=now(),round=str(round_dir),active_processes=checks,assigned_distinct_files=len(assigned),expected_distinct_files=len(expected),complete_input_assignment=set(assigned)==expected,source_sha256=sources,assistant_caps=None,continuation_supervisor_required=True,historical_violation='The original022 coordinator raised on an incomplete stage and exited; reserved unstarted slots transfer future scheduling to the attached supervisor while active01/02 are preserved.',violations=violations,environment_audit='Whitelisted CLAUDE_CODE_MAX_OUTPUT_TOKENS only; no credential/auth environment values recorded')
    with output.open('x') as stream:stream.write(json.dumps(result,indent=2)+'\n')
    if violations:raise ValueError('Active review integrity violation; inspect audit')
    return result
