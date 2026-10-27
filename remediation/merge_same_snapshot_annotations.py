import pathlib,json,hashlib,shutil
R=pathlib.Path(__file__).resolve().parent;S=pathlib.Path('/home/sharpninja/github/2026-10-26/task/android-catalog-20261026');receipt=json.loads((S/'integration/same-snapshot-annotations-integration-v2.json').read_text());source=S/'catalog-source-followup'
for item in receipt['files']:
 p=source/item['path'];assert hashlib.sha256(p.read_bytes()).hexdigest()==item['candidate_sha256']
 if item['path']!='tooling/build_catalog.py':
  target=R/'catalog'/item['path'];target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,target)
p=R/'catalog/tooling/build_catalog.py';s=p.read_text();anchor="      target_match_kind='current-sdk-index-exact'"
block="""
      if not matches and platform not in index_versions.values():
       from annotation_link import fallback_supported,same_snapshot_source_ids,snapshot_declared_type_matches
       for candidate in dict.fromkeys(candidates):
        candidate_bid=sha('java:'+candidate);eligible=same_snapshot_source_ids(db,candidate_bid,s,source_by_id)
        if eligible and snapshot_declared_type_matches(db,candidate_bid,name,eligible,sha) and (candidate==canonical or not sep or fallback_supported(db,candidate_bid,canonical,source_by_id,sha,eligible)):matches.append((candidate,candidate_bid))
       target_match_kind='same-snapshot-sdk-signature'"""
assert anchor in s;s=s.replace(anchor,anchor+block)
s=s.replace("if matched_canonical!=canonical:target_match_kind='java-lang-spelling-equivalence' if sep else 'package-namespace'","if matched_canonical!=canonical:target_match_kind=('same-snapshot-sdk-signature-' if target_match_kind=='same-snapshot-sdk-signature' else '')+('java-lang-spelling-equivalence' if sep else 'package-namespace')")
old="  if s.get('platform_api_version') is not None and str(s['platform_api_version']) not in exact_index_targets:\n   report['status']='partial';issue(s,None,'No imported SDK API index for annotation platform '+str(s['platform_api_version']),s['path'],'annotation-platform')\n";assert old in s;s=s.replace(old,'')
anchor=' for report in per_source:db.execute'
diagnostic="""  if s.get('platform_api_version') is not None and str(s['platform_api_version']) not in exact_index_targets and report['mapped_annotations']<report['annotations']:
   report['status']='partial';issue(s,None,'No imported SDK API index for annotation platform '+str(s['platform_api_version'])+' and unresolved same-snapshot signature targets remain',s['path'],'annotation-platform')
"""
assert anchor in s;s=s.replace(anchor,diagnostic+anchor);p.write_text(s)
shutil.copyfile(S/'integration/same-snapshot-annotations-integration-v2.json',R/'evidence/source-same-snapshot-annotations-v2.json')
print('Merged source followup into own copy; no source files changed')
