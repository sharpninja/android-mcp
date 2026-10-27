"""Verified frozen AIDL source hash -> interface observation predicate.
No source version is treated as an Android or installed APEX version.
"""
import copy,re
def stable_module(module):
 properties=module.get('properties',{})
 if module.get('kind')!='aidl_interface' or not properties.get('complete'):return False
 flags=[v['value'] for v in properties.get('items',[]) if v['name']=='unstable']
 return not flags or len(flags)==1 and flags[0].get('kind')=='literal' and flags[0].get('value') is False
def exact_constraint(source,declaration,audit,published_text,module,linkage):
 def unknown(reason):return dict(unknown_reason=reason)
 if declaration.get('declaration_kind')!='interface' or declaration.get('declaration_depth',0)!=0 or declaration.get('parse_status')!='methods-parsed':return unknown('No complete direct parsed AIDL interface declaration')
 if any(v.get('name','').rsplit('.',1)[-1]=='Descriptor' for v in declaration.get('annotations',[])):return unknown('Descriptor annotation requires explicit compiler descriptor semantics')
 descriptor=declaration.get('descriptor')
 if not isinstance(descriptor,str) or not re.fullmatch(r'[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*',descriptor):return unknown('No qualified AIDL descriptor')
 identity={k:source[k] for k in ('repository','revision','path','sha256')}
 if identity not in audit.get('source_inputs',[]):return unknown('Exact declaration is not in reconciled hash input set')
 if any(source[k]!=audit.get(k) for k in ('repository','revision')):return unknown('Frozen hash audit revision differs')
 if not audit.get('result',{}).get('verified'):return unknown('Frozen dump hash did not verify')
 if linkage.get('status')!='frozen-dump-declared-version-linked':return unknown('Frozen module version not uniquely linked')
 if not stable_module(module):return unknown('Stable AIDL build flag is unresolved or unstable')
 if any(source[k]!=module.get('source',{}).get(k) for k in ('repository','revision')):return unknown('Build declaration revision differs')
 lines=published_text.splitlines();selected=lines[-1].strip() if lines else ''
 expected=audit['result']['computed_hash']
 if selected!=expected:return unknown('Backend tail -1 hash selection differs from verified hash')
 return dict(contract='exact-aidl-source-constraint/1',
  clause=dict(interface=dict(descriptor=descriptor,minimum_version=None,accepted_hashes=[expected])),
  frozen_version_literal=audit['result']['version_literal'],
  source_evidence=[identity,copy.deepcopy(audit['hash_source']),copy.deepcopy(module['source'])],
  runtime_observation=False,android_platform_range=None,wire_codec_proven=False,os_or_client_authority_granted=False,
  source_hash_verified=True,backend_selection='pinned aidl_gen_rule.go --hash tail -1',
  limitations=['Future trusted descriptor/hash reachability facts required','Type dependencies, OS/client grants and codecs remain separate'])
