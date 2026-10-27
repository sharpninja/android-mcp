"""Offline orchestration simulation; never Anthropic inference or agreement."""
from pathlib import Path
import unittest,tempfile,types,sys,runpy,json,hashlib,datetime,threading
HERE=Path(__file__).resolve().parent

class SupervisorIntegration(unittest.TestCase):
 def test_parallel_passes_continue_and_automatically_reach_one_synthesis(self):
  with tempfile.TemporaryDirectory(prefix='android-mcp-supervisor-test-') as temp:
   root=Path(temp);d=root/'reviews/round-simulation';inp=d/'input';inp.mkdir(parents=True)
   # Run the same real 12-test preflight inside the isolated fixture too. It
   # only inspects invocation constants and synthetic responses; no CLI calls.
   for name in ('hv_invariants.py','hv_runtime.py','test_hv_invariants.py'):(root/name).write_bytes((HERE/name).read_bytes())
   (root/'reviews/round-018').mkdir(parents=True);(root/'reviews/round-018/requirements.txt').write_text('SIMULATION ONLY: full design scope, no actual model score.')
   files=['design-c4/docs/Android MCP Bridge Design.md','design-c4/architecture/workspace.dsl','design-c4/architecture/code-diagrams.md','catalog/SOURCE-METADATA-CONTRACT.md','ui1.svg','ui2.svg']
   rows=[]
   for n,f in enumerate(files):
    p=inp/f;p.parent.mkdir(parents=True,exist_ok=True);p.write_text('Evidence '+str(n));rows.append(dict(file=f,sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
   manifest=dict(fresh_supervised_round=True,scope='OFFLINE SIMULATION ONLY',files=rows,passes={'1':rows[:2],'2':rows[2:4],'3':rows[4:]},byte_identical_aliases=[],binary_dependencies=[])
   (d/'input-manifest.json').write_text(json.dumps(manifest));(d/'supervisor-handoff.json').write_text('{}');(d/'process.json').write_text(json.dumps(dict(status='finished',pid=0)))
   calls={};lock=threading.Lock();final_units=[]
   fake=types.ModuleType('hv_runtime');fake.ROOT=root;fake.ARGS=['claude','--tools','','--effort','max'];fake.ENV={'CLAUDE_CODE_MAX_OUTPUT_TOKENS':'128000'}
   fake.sha=lambda b:hashlib.sha256(b).hexdigest();fake.now=lambda:datetime.datetime.now(datetime.timezone.utc).isoformat();fake.alive=lambda p:False;fake.read_events=lambda p:[];fake.audit_active=lambda *a:dict(simulated=True)
   def capture(target,instruction,units,marker,parent_sha,extra='',context_partition=False):
    target.mkdir(parents=True);stage=target.parent.name
    with lock:calls[stage]=calls.get(stage,0)+1;n=calls[stage]
    if stage=='synthesis':
     final_units.extend(units);text='OFFLINE SIMULATED FINAL; NO CLAUDE SCORE\n'+marker;reason='end_turn';error=False
    elif stage=='pass-02' and len(units)>1:
     return [],dict(exit_code=1,terminal=True,result_metadata=dict(is_error=True,stop_reason='end_turn',terminal_reason='prompt_too_long'))
    elif stage in ('pass-01','pass-03') and n==1:
     text='material partial finding '+stage;reason='max_tokens';error=False
    else:
     text='complete '+stage+' '+','.join(u['file'] for u in units)+'\n'+marker;reason='end_turn';error=False
    return [dict(type='assistant',message=dict(content=[dict(type='text',text=text)]))],dict(exit_code=0,terminal=True,result_metadata=dict(is_error=error,stop_reason=reason))
   fake.capture=capture;oldmodule=sys.modules.get('hv_runtime');oldargv=sys.argv[:]
   try:
    sys.modules['hv_runtime']=fake;sys.argv=[str(HERE/'hv_supervisor.py'),'simulation']
    runpy.run_path(str(HERE/'hv_supervisor.py'),run_name='__main__')
   finally:
    sys.argv=oldargv
    if oldmodule is None:sys.modules.pop('hv_runtime',None)
    else:sys.modules['hv_runtime']=oldmodule
   result=json.loads((d/'completion.supervised.json').read_text());self.assertTrue(result['report_complete'])
   self.assertEqual(2,calls['pass-01']);self.assertEqual(3,calls['pass-02']);self.assertEqual(2,calls['pass-03']);self.assertEqual(1,calls['synthesis'])
   merged='\n'.join(u['text'] for u in final_units)
   for stage in ('pass-01','pass-03'):self.assertIn('material partial finding '+stage,merged)
   for f in files[2:4]:self.assertIn(f,merged)
   self.assertEqual('complete',json.loads((d/'supervisor/lease.json').read_text())['status'])
   self.assertIsNone(json.loads((d/'supervisor/control.json').read_text())['assistant_caps'])

if __name__=='__main__':unittest.main()
