import pathlib
p=pathlib.Path(__file__).resolve().parent/'catalog/tooling/build_catalog.py';s=p.read_text()
s=s.replace('exact_index_targets={};jvm_owners={};jvm_keys={};linked_permissions=0','exact_index_targets={};index_versions={};jvm_owners={};jvm_keys={};linked_permissions=0')
needle="   for r in cached_records(s,root):\n    if '#' not in r['canonical']:jvm_owners[r['owner']]=r['extras']['jvm_owner']\n    jvm_keys[r['canonical']]=sha('jvm:'+r['canonical'])"
replacement="""   detected=set()
   for r in cached_records(s,root):
    platform=str(r['extras'].get('target_platform_api',''))
    if not platform:raise ValueError('API index cache lacks target platform version')
    if s.get('platform_api_version') is not None and str(s['platform_api_version'])!=platform:raise ValueError('API index target platform mismatch')
    detected.add(platform)
    if '#' not in r['canonical']:jvm_owners.setdefault(platform,{})[r['owner']]=r['extras']['jvm_owner']
    jvm_keys.setdefault(platform,{})[r['canonical']]=sha('jvm:'+r['canonical'])
   if len(detected)>1:raise ValueError('API index cache mixes target platform versions')
   index_versions[s['source_id']]=str(s.get('platform_api_version') or next(iter(detected),''))
   exact_index_targets.setdefault(index_versions[s['source_id']],{})"""
assert needle in s;s=s.replace(needle,replacement)
s=s.replace("candidate=source_jvm_key(r,jvm_owners)\n   if candidate in jvm_keys:extensions['proven_current_sdk_jvm_identity']=candidate;target_jvm=jvm_keys[candidate]", "platform=str(s.get('platform_api_version',''));candidate=source_jvm_key(r,jvm_owners.get(platform,{}))\n   if candidate in jvm_keys.get(platform,{}):extensions['proven_current_sdk_jvm_identity']=candidate;target_jvm=jvm_keys[platform][candidate]")
s=s.replace("   if target in exact_index_targets and exact_index_targets[target]!=bid:exact_index_targets[target]=None\n   else:exact_index_targets[target]=bid", "   targets=exact_index_targets[index_versions[s['source_id']]]\n   if target in targets and targets[target]!=bid:targets[target]=None\n   else:targets[target]=bid")
s=s.replace("if s.get('platform_api_version')=='37.2':\n      current=exact_index_targets.get(canonical)","if s.get('platform_api_version') is not None:\n      platform=str(s['platform_api_version']);current=exact_index_targets.get(platform,{}).get(canonical)")
# Source package declarations are version bound as well, never a cross-version fallback.
s=s.replace("if db.execute('SELECT 1 FROM bindings WHERE binding_id=?',(package_bid,)).fetchone():matches.append((package_candidate,package_bid))", "if any(item['kind']=='java-source' and str(item.get('platform_api_version',''))==platform for item in sources) and db.execute('SELECT 1 FROM bindings WHERE binding_id=?',(package_bid,)).fetchone():matches.append((package_candidate,package_bid))")
p.write_text(s)
p=p.parents[1]/'tests/test_source_integration.py';s=p.read_text().replace("metadata_cache=dict(path=cache.name, sha256=hashlib.sha256(cache.read_bytes()).hexdigest(), records=len(records)))","platform_api_version='37.2',metadata_cache=dict(path=cache.name, sha256=hashlib.sha256(cache.read_bytes()).hexdigest(), records=len(records)))");p.write_text(s)
