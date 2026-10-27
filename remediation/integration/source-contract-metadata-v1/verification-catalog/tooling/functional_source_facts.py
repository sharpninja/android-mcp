"""Evidence axes for future many-to-many functional grouping, not group policy."""
import re,json,copy
def taxonomy(doc):
 e=doc.get('extensions',{});rows=[];imports=e.get('imports',[])
 for annotation in e.get('annotations',[]):
  name=annotation.get('name','')
  if name.rsplit('.',1)[-1]!='SystemService':continue
  qualified=name=='android.annotation.SystemService' or name=='SystemService' and 'android.annotation.SystemService' in imports
  raw=annotation.get('arguments','').strip();value=re.sub(r'^value\s*=\s*','',raw);literal=None
  if value.startswith('"'):
   try:literal=json.loads(value)
   except ValueError:pass
  symbolic=value if re.fullmatch(r'[A-Za-z_$][\w$]*(?:\.[A-Za-z_$][\w$]*)*',value) else None
  rows.append(dict(dimension='android-system-service-declaration',owner_entry_id=doc['entry_id'],owner=doc.get('owner'),
   service_string_literal=literal if isinstance(literal,str) else None,service_symbol_expression=symbolic,
   annotation_namespace_proven=qualified,source_annotation=copy.deepcopy(annotation),
   confidence='explicit-android-annotation' if qualified else 'annotation-namespace-unresolved',
   provenance=copy.deepcopy(doc.get('provenance',[])),
   functional_group_membership_proven=False,
   unresolved=['Symbolic service values require exact same-snapshot constant resolution' if symbolic else 'Service selector is literal or unresolved','Small functional groups, member roles, descriptions and dependency closure require separate curated/evidenced mapping']))
 return dict(entry_id=doc['entry_id'],functional_evidence=rows,
  namespace_search_evidence=dict(package=doc.get('package'),owner=doc.get('owner'),functional_group_inferred=False),
  imported_group_memberships=[],local_group_overrides_separate=True,
  classification='evidence-present' if rows else 'unclassified-searchable')
