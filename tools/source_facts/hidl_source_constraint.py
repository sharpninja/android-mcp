"""Exact HIDL .hal bytes + published identifier hash -> source constraint.
The output is a predicate for future observed interface facts, not an observation,
wire contract, authority grant, or proof that a service exists on any device.
"""
import re,copy
def exact_constraint(source,record,facts):
 if record.get('kind')!='interface':return dict(unknown_reason='Published package/type hash does not establish an interface descriptor')
 descriptor=record.get('canonical');digest=source.get('sha256')
 if not isinstance(descriptor,str) or not re.fullmatch(r'[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*@\d+\.\d+::[A-Za-z_]\w*',descriptor):return dict(unknown_reason='No direct parsed HIDL interface FQName')
 if not isinstance(digest,str) or not re.fullmatch('[0-9a-f]{64}',digest):return dict(unknown_reason='No exact SHA256 source bytes')
 matches=[v for v in facts if v.get('format')=='published-hidl-hash-hex64-and-identifier'
  and v.get('declared_identifier')==descriptor and v.get('literal')==digest
  and all(v['source'].get(k)==source.get(k) for k in ('repository','revision'))]
 if not matches:return dict(unknown_reason='No same-revision published HIDL interface hash equals these exact source bytes')
 identities=[{k:source[k] for k in ('repository','revision','path','sha256')}]
 for fact in matches:
  if fact['source'] not in identities:identities.append(copy.deepcopy(fact['source']))
 return dict(contract='exact-hidl-source-constraint/1',
  clause=dict(interface=dict(descriptor=descriptor,minimum_version=None,accepted_hashes=[digest])),
  source_evidence=identities,published_lines=[v['line'] for v in matches],
  source_bytes_hash_equals_published_identifier_hash=True,runtime_observation=False,
  android_platform_range=None,wire_codec_proven=False,os_or_client_authority_granted=False,
  normative_reference='https://source.android.com/docs/core/architecture/hidl/hashing')
