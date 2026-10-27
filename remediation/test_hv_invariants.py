import unittest
from hv_invariants import *

def response(text,reason='end_turn',error=False,**kw):
    return ([dict(type='assistant',message=dict(content=[dict(type='text',text=text)]))],dict(exit_code=0,terminal=True,result_metadata=dict(stop_reason=reason,is_error=error,**kw)))
class Invariants(unittest.TestCase):
 def test_turn_budget_and_elapsed_caps_rejected(self):
  for flag in ('--max-turns','--max-budget-usd','--timeout'):
   with self.assertRaises(PolicyViolation):preflight(['claude',flag,'1'],{},'',[],[])
  with self.assertRaises(PolicyViolation):preflight([],{},'',[],[],'import subprocess\nsubprocess.run([], timeout=10)')
 def test_report_input_and_scope_caps_rejected(self):
  with self.assertRaises(PolicyViolation):preflight([],{},'Return at most2500 words',[],[])
  with self.assertRaises(PolicyViolation):preflight([],{},'Return at most 2500 words',[],[])
  with self.assertRaises(PolicyViolation):preflight([],{},'',[],['required'])
  with self.assertRaises(PolicyViolation):preflight([],{},'',[],[],'if len(prompt)>260000:\n raise SystemExit()')
 def test_provider_maximum_and_complete_capture_not_total_budget(self):
  r=preflight(['claude','--effort','max'],{'CLAUDE_CODE_MAX_OUTPUT_TOKENS':'128000'},'Unbounded review',['all'],['all'])
  self.assertIsNone(r['assistant_caps'])
  body='visible project evidence '*10000+' END'
  es,c=response(body);self.assertEqual(body,assess(es,c,'END')['report'])
 def test_provider_max_tokens_automatically_continues(self):
  calls=[];answers=[response('first material finding',reason='max_tokens'),response('second material finding\nEND')]
  def invoke(units,continuation):calls.append(continuation);return answers.pop(0)
  text=continue_until_complete([dict(text='all evidence')],invoke,'END')
  self.assertEqual(2,len(calls));self.assertIn('first material finding',calls[1]);self.assertIn('first material finding',text);self.assertIn('second material finding',text)
 def test_partial_json_and_split_messages_complete_without_retry(self):
  es,c=response('');es=[dict(type='assistant',message=dict(content=[dict(type='text',text=t)])) for t in ('{"findings":["keep all"],"report_complete":tr','ue}')]
  self.assertEqual('complete',assess(es,c,'not-needed')['status'])
 def test_partial_json_gets_automatic_continuation(self):
  replies=[response('{"findings":["kept"],',reason='max_tokens'),response('Complete visible report\nEND')];calls=[]
  def invoke(u,c):calls.append(c);return replies.pop(0)
  r=continue_until_complete([dict(text='all')],invoke,'END');self.assertEqual(2,len(calls));self.assertIn('kept',r)
 def test_provider_context_error_splits_all_bytes_without_omission(self):
  seen=[]
  def invoke(units,c):
   if len(units)>1:return response('',error=True,terminal_reason='prompt_too_long',api_error_status=400)
   seen.extend(u['text'] for u in units);return response('reviewed '+units[0]['text']+' END')
  r=continue_until_complete([dict(text='A'),dict(text='B')],invoke,'END');self.assertEqual(['A','B'],seen);self.assertIn('reviewed A',r);self.assertIn('reviewed B',r)
 def test_auth_service_denial_pauses_no_retry_spin(self):
  calls=[]
  def invoke(u,c):calls.append(1);return response('',error=True,terminal_reason='authentication_error',api_error_status=401)
  with self.assertRaises(ProviderBlocked):continue_until_complete([dict(text='all')],invoke,'END')
  self.assertEqual(1,len(calls))
 def test_single_artifact_context_partition_cannot_pass_as_whole(self):
  original=[dict(key='one-artifact',text='ABCD',start=0,end=4)]
  pieces=split_units(original)
  self.assertEqual('ABCD',''.join(p[0]['text'] for p in pieces))
  for part in pieces:self.assertNotEqual(coverage_signature(original),coverage_signature(part))
 def test_uncertain_outcome_never_duplicates_call(self):
  with self.assertRaises(OutcomeUncertain):assess([],dict(terminal=False),'END')
 def test_incomplete_exit_requires_attached_supervisor(self):
  src='raise RuntimeError("Stage incomplete; preserve and recover only")'
  with self.assertRaises(PolicyViolation):preflight([],{},'',[],[],src)
  self.assertTrue(preflight([],{},'',[],[],src,supervised=True)['continuation_supervised'])
 def test_actual_invocation_has_empty_tools_and_max_effort_without_bypass(self):
  from hv_runtime import ARGS,ENV
  self.assertEqual('',ARGS[ARGS.index('--tools')+1])
  self.assertEqual('max',ARGS[ARGS.index('--effort')+1])
  self.assertEqual('claude-fable-5-1',ARGS[ARGS.index('--model')+1])
  self.assertFalse(any('skip-permission' in x or x.startswith('--max-turn') for x in ARGS))
  self.assertEqual('128000',ENV['CLAUDE_CODE_MAX_OUTPUT_TOKENS'])
if __name__=='__main__':unittest.main()
