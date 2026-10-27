"""Offline decision algebra. Not authentication, an Android backend or an MCP service."""
from dataclasses import dataclass

@dataclass(frozen=True)
class Rule:
    effect: str
    schema_hash: str
    allowed_targets: frozenset
    max_items: int

def resolve_fixture_route(route,request,aliases,schemas,wire_mappings):
    """Fixture-only canonicalization, with explicitly supplied proven wire mappings.

    No Binder transaction codes or decoders are inferred from AIDL declaration order.
    The caller supplies a decoded canonical fixture argument; a real backend must
    prove that its byte decoder produces this same typed representation.
    """
    if route=='generated':
        binding,schema=aliases[request['tool_name']]
    elif route=='generic':
        binding=request['binding_id'];schema=schemas[binding]
        if request['schema_hash']!=schema:raise ValueError('Stale generic schema')
    elif route=='raw':
        key=(request['descriptor'],request['transaction_code'])
        if key not in wire_mappings:raise ValueError('Unknown raw requires separate broad service authority')
        if not request.get('decoded_and_validated'):raise ValueError('Known raw cannot bypass canonical argument validation')
        binding,schema=wire_mappings[key]
    else:raise ValueError('Unknown route')
    if schemas.get(binding)!=schema:raise ValueError('Stale alias or wire mapping')
    return [(binding,schema,request['target'],request['items'])]

def evaluate(binding_plan, binding_rules, entry_rule, *, entry_schema, authority, emergency=False):
    """All aliases supply the SAME decoded binding plan; raw unknown uses a raw key.

    Target and numeric bounds here are fixture examples of the closed constraint
    schemas. Real adapters must validate every argument/output field before admission.
    Each tuple is (canonical binding key, exact schema hash, target, requested items).
    """
    if emergency or not binding_plan or entry_rule is None or entry_rule.schema_hash!=entry_schema: return 'deny'
    effects=[]
    for key, schema, target, items in binding_plan:
        rule=binding_rules.get(key)
        if rule is None or rule.schema_hash!=schema: return 'deny'
        for r in (rule,entry_rule):
            if r.effect not in ('allow','ask') or target not in r.allowed_targets or not isinstance(items,int) or isinstance(items,bool) or items<0 or items>r.max_items: return 'deny'
            effects.append(r.effect)
    if not authority: return 'os-unavailable'
    return 'ask' if 'ask' in effects else 'admit-candidate'

@dataclass(frozen=True)
class RawGrant:
    connection_id: str
    descriptor: str
    code_min: int
    code_max: int
    flags_mask: int
    contract_hash: str
    rule: Rule
    allowed_handles: frozenset = frozenset()
    allowed_displays: frozenset = frozenset()

def evaluate_raw_fixture(request,raw_grants,entry_rule,*,entry_schema,authority,wire_mappings,schemas,binding_rules,emergency=False,binding_entry_rule=None,binding_entry_schema='binding.invoke-v1'):
    """Known schemas always take binding policy; raw grants cannot override it."""
    if (request['descriptor'],request['transaction_code']) in wire_mappings:
        try:plan=resolve_fixture_route('raw',request,{},schemas,wire_mappings)
        except (KeyError,ValueError):return 'deny'
        checks=[evaluate(plan,binding_rules,binding_entry_rule,entry_schema=binding_entry_schema,authority=True,emergency=emergency),evaluate(plan,binding_rules,entry_rule,entry_schema=entry_schema,authority=True,emergency=emergency)]
        if 'deny' in checks:return 'deny'
        if not authority:return 'os-unavailable'
        return 'ask' if 'ask' in checks else 'admit-candidate'
    key=(request.get('connection_id'),request['descriptor']);grant=raw_grants.get(key)
    if grant is None or grant.connection_id!=key[0] or grant.descriptor!=key[1] or grant.contract_hash!=request.get('contract_hash'):return 'deny'
    if request.get('handle') not in grant.allowed_handles or request.get('display_id') not in grant.allowed_displays:return 'deny'
    code=request['transaction_code'];flags=request.get('flags',0)
    if not isinstance(code,int) or isinstance(code,bool) or not grant.code_min<=code<=grant.code_max:return 'deny'
    if not isinstance(flags,int) or isinstance(flags,bool) or flags<0 or flags&~grant.flags_mask:return 'deny'
    raw_key='raw:'+grant.connection_id+':'+grant.descriptor
    return evaluate([(raw_key,grant.rule.schema_hash,request['target'],request['items'])],{raw_key:grant.rule},entry_rule,entry_schema=entry_schema,authority=authority,emergency=emergency)

def policy_entrypoints(route,known_raw=False):
    if route in ('generated','generic'):return ('binding.invoke',)
    if route=='raw':return ('binding.invoke','binder.transact') if known_raw else ('binder.transact',)
    raise ValueError('Convenience wrappers use their own registered policy tool_id')
