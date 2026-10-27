"""Mandatory operational invariants for user-authorized unbounded design HV."""
import ast,json,re,hashlib

class PolicyViolation(Exception):pass
class ProviderBlocked(Exception):pass
class OutcomeUncertain(Exception):pass

def preflight(args,env,instruction,coverage,expected,source='',supervised=False):
    bad=[a for a in args if a.split('=',1)[0] in ('--max-turns','--max-budget-usd','--timeout','--max-time','--max-duration')]
    if bad:raise PolicyViolation('Forbidden assistant invocation cap: '+repr(bad))
    if env.get('CLAUDE_CODE_MAX_OUTPUT_TOKENS') not in (None,'128000'):raise PolicyViolation('Request must use the documented provider/model maximum, not a lower assistant token allowance')
    for k,v in env.items():
        if k.startswith('HV_') and any(t in k for t in ('MAX_','DEADLINE','TIMEOUT','BUDGET')) and v is not None:raise PolicyViolation('Forbidden review policy cap: '+k)
    # Scope is the orchestration instruction, never product limits quoted in artifacts.
    if re.search(r'(?:at most|under|<=|no more than|maximum)\s*\d[\d,]*\s*(?:words|findings|characters of (?:the )?report|tokens of visible)',instruction,re.I):raise PolicyViolation('Forbidden assistant report-size/word/finding cap')
    if set(coverage)!=set(expected):raise PolicyViolation('Input coverage omission or substitution')
    if len(coverage)!=len(set(coverage)):raise PolicyViolation('Duplicate assigned coverage without explicit aliases')
    if source:
        tree=ast.parse(source)
        for n in ast.walk(tree):
            if isinstance(n,ast.Call):
                name=n.func.attr if isinstance(n.func,ast.Attribute) else n.func.id if isinstance(n.func,ast.Name) else ''
                if name in ('run','Popen','communicate','wait') and any(k.arg=='timeout' for k in n.keywords):raise PolicyViolation('Assistant elapsed timeout in process orchestration')
            if isinstance(n,ast.If) and isinstance(n.test,ast.Compare):
                t=ast.unparse(n.test)
                if re.search(r'len\((?:prompt|report|texts_out|visible)',t) and any(isinstance(x,(ast.Raise,ast.Break,ast.Return)) for x in n.body):raise PolicyViolation('Assistant input/report/global output guard')
            if isinstance(n,ast.For) and isinstance(n.target,ast.Name) and re.search('attempt|retry',n.target.id,re.I) and isinstance(n.iter,ast.Call) and getattr(n.iter.func,'id',None)=='range':raise PolicyViolation('Finite continuation retry loop')
        if 'Stage incomplete; preserve and recover only' in source and not supervised:raise PolicyViolation('Incomplete-stage exit requires a live recovery supervisor')
    return dict(assistant_caps=None,coverage_complete=True,provider_per_response_request=env.get('CLAUDE_CODE_MAX_OUTPUT_TOKENS'),continuation_supervised=supervised)

def visible_text(events):
    chunks=[];fallback=None
    for e in events:
        if e.get('type')=='assistant':
            chunks.extend(b['text'] for b in e.get('message',{}).get('content',[]) if b.get('type')=='text')
        if e.get('type')=='result':fallback=e.get('result')
    if not chunks and fallback:chunks=[fallback]
    return chunks

def assess(events,completion,marker):
    if completion.get('terminal') is False:raise OutcomeUncertain('No known terminal outcome; never launch duplicate')
    result=completion.get('result_metadata',{});chunks=visible_text(events);combined=''.join(chunks);human='\n\n'.join(chunks)
    terminal_ok=completion.get('exit_code')==0 and not result.get('is_error') and result.get('stop_reason') in ('end_turn',None)
    if terminal_ok and (human.strip().rstrip('`').strip().endswith(marker) or completion.get('report_complete') is True):return dict(status='complete',report=human,format='visible Markdown/end marker')
    # Complete JSON split across assistant messages is valid; preserve every original
    # event and record this deterministic concatenation instead of re-reviewing it.
    candidate=combined.strip()
    if candidate.startswith('```'):candidate=re.sub(r'^```(?:json)?\s*','',candidate).removesuffix('```').strip()
    try:parsed=json.loads(candidate)
    except ValueError:parsed=None
    if terminal_ok and isinstance(parsed,dict) and parsed.get('report_complete') is True:return dict(status='complete',report=human,format='complete JSON assembled from all visible message fragments',json=parsed)
    reason=str(result.get('terminal_reason','')).lower();status=result.get('api_error_status')
    if status in (401,403,429) or result.get('permission_denials') or (result.get('safety_stops') and result.get('is_error')) or any(x in reason for x in ('auth','permission','rate_limit','billing','quota','refill_breaker')):
        raise ProviderBlocked('Provider/auth/service denial: '+json.dumps(dict(status=status,reason=reason,permission_denials=result.get('permission_denials'),safety_stops=result.get('safety_stops'))))
    return dict(status='context' if any(x in reason for x in ('prompt_too_long','context')) else 'continue',report=human,reason=reason or result.get('stop_reason'))

def split_units(units):
    """Only after an actual provider context failure; no arbitrary input cap."""
    if len(units)>1:
        at=len(units)//2;return [units[:at],units[at:]]
    u=units[0];text=u['text']
    if len(text)<2:raise ProviderBlocked('Provider cannot accept even one indivisible evidence unit; capability blocker')
    at=len(text)//2;base=u.get('start',0)
    return [[dict(u,text=text[:at],start=base,end=base+len(text[:at]))],[dict(u,text=text[at:],start=base+len(text[:at]),end=base+len(text))]]

def coverage_signature(units):
    """A one-file context split is still a partial range, even with the same key."""
    return {(u.get('key'),u.get('start',0),u.get('end',len(u['text'])),hashlib.sha256(u['text'].encode()).hexdigest()) for u in units}

def continue_until_complete(units,invoke,marker,prior=''):
    """No finite retry count or elapsed/output budget. Only known terminal retries."""
    accumulated=prior;continuation=('Continue the same immutable stage from the FULL VISIBLE PARTIAL OUTPUT below, preserving every finding and remaining coverage; no assistant cap.\n'+prior) if prior else ''
    while True:
        events,completion=invoke(units,continuation)
        state=assess(events,completion,marker)
        if state['status']=='complete':return accumulated+('\n\n' if accumulated else '')+state['report']
        if state['report']:accumulated+=('\n\n' if accumulated else '')+state['report']
        if state['status']=='context':
            reports=[]
            for child in split_units(units):reports.append(continue_until_complete(child,invoke,marker))
            return accumulated+'\n\nCOMPLETE PROVIDER-CONTEXT PARTS (no artifact omitted)\n'+'\n\n'.join(reports)
        continuation='The previous terminal response was incomplete. Continue the same immutable stage, preserving all prior findings and remaining coverage. No word/time/turn/output cap. Finish the assigned evidence and end with '+marker+'.\nFULL VISIBLE PARTIAL OUTPUT, UNMODIFIED:\n'+accumulated
