"""Offline decision algebra. Not authentication, an Android backend or an MCP service."""
from dataclasses import dataclass

@dataclass(frozen=True)
class Rule:
    effect: str
    schema_hash: str
    allowed_targets: frozenset
    max_items: int

def evaluate(binding_plan, binding_rules, entry_rule, *, authority, emergency=False):
    """All aliases supply the SAME decoded binding plan; raw unknown uses a raw key.

    Target and numeric bounds here are fixture examples of the closed constraint
    schemas. Real adapters must validate every argument/output field before admission.
    Each tuple is (canonical binding key, exact schema hash, target, requested items).
    """
    if emergency or not binding_plan or entry_rule is None: return 'deny'
    effects=[]
    for key, schema, target, items in binding_plan:
        rule=binding_rules.get(key)
        if rule is None or rule.schema_hash!=schema: return 'deny'
        for r in (rule,entry_rule):
            if r.effect not in ('allow','ask') or target not in r.allowed_targets or not isinstance(items,int) or isinstance(items,bool) or items<0 or items>r.max_items: return 'deny'
            effects.append(r.effect)
    if not authority: return 'os-unavailable'
    return 'ask' if 'ask' in effects else 'admit-candidate'
