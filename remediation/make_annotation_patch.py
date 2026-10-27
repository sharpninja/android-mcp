"""Focused source-task handoff; never writes the active source task."""
import pathlib,difflib,json,hashlib
R=pathlib.Path(__file__).resolve().parent;D=R/'integration';S=pathlib.Path('/home/sharpninja/github/2026-10-26/task/android-catalog-20261026/catalog/tooling/build_catalog.py')
old=S.read_text();new=old
anchor="     if bid and not db.execute('SELECT 1 FROM bindings WHERE binding_id=?',(bid,)).fetchone():bid=None\n     anns=[]"
replacement="""     if bid and not db.execute('SELECT 1 FROM bindings WHERE binding_id=?',(bid,)).fetchone():bid=None
     target_match_kind='exact' if bid else 'unknown';matched_canonical=canonical if bid else None
     # Method-only spelling equivalence. Keep the37.2 exact-index/type guard;
     # field/name-only permission linking is deliberately untouched.
     if sep and canonical and '(' in canonical:
      head,args=canonical.split('(',1);short=re.sub(r'(?<![\\w.$])java\\.lang\\.','',args)
      candidates=list(dict.fromkeys([canonical,head+'('+short]));matches=[]
      for candidate in candidates:
       candidate_id=exact_index_targets.get(candidate) if s.get('api_level')=='37.2' else sha('java:'+candidate)
       if candidate_id and db.execute('SELECT 1 FROM bindings WHERE binding_id=?',(candidate_id,)).fetchone():matches.append((candidate,candidate_id))
      if len({key for spelling,key in matches})==1:
       matched_canonical,bid=matches[0]
       target_match_kind='exact' if matched_canonical==canonical else 'java-lang-spelling-equivalence'
      else:bid=None;matched_canonical=None;target_match_kind='unresolved-or-ambiguous'
     anns=[]"""
if new.count(anchor)!=1:raise ValueError('Source context changed; review manually without overwriting')
new=new.replace(anchor,replacement)
old_evidence="annotation=a,target_resolution='exact' if bid else 'unknown')"
if new.count(old_evidence)!=1:raise ValueError('Source evidence context changed')
new=new.replace(old_evidence,"annotation=a,target_resolution='exact' if bid else 'unknown',target_match_kind=target_match_kind,matched_canonical_signature=matched_canonical)")
(D/'annotation-method-spelling.patch').write_text(''.join(difflib.unified_diff(old.splitlines(True),new.splitlines(True),fromfile='a/catalog/tooling/build_catalog.py',tofile='b/catalog/tooling/build_catalog.py')))
(D/'build_catalog.annotation-method-only.py').write_text(new)
(D/'annotation-method-manifest.json').write_text(json.dumps(dict(source_path=str(S),source_sha256=hashlib.sha256(old.encode()).hexdigest(),patched_sha256=hashlib.sha256(new.encode()).hexdigest(),scope='Method java.lang spelling lookup only;37.2 exact_index_targets guard preserved; no field-link, parser/cache, identity, metadata or active source modifications'),indent=2))
print(D/'annotation-method-spelling.patch')
