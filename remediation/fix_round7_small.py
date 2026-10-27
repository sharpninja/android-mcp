import pathlib,re
R=pathlib.Path(__file__).resolve().parent;C=R/'catalog'
p=C/'migrations/002-evidence-and-publication.sql';s=p.read_text();bad="FROM overlays o CROSS JOIN catalog_view v WHERE o.imported_binding_id IS NULL AND o.state IN ('active','deleted') AND NOT EXISTS";assert bad in s;s=s.replace(bad,"FROM overlays o CROSS JOIN catalog_view v WHERE o.imported_binding_id IS NULL AND o.state='active' AND NOT EXISTS")
s=s.replace("new.imported_binding_id IS NULL AND new.state='active' AND EXISTS","new.imported_binding_id IS NULL AND new.state IN ('active','deleted') AND EXISTS");p.write_text(s);(C/'schema.sql').write_text('\n'.join(p.read_text() for p in sorted((C/'migrations').glob('*.sql'))))
p=C/'tooling/build_catalog.py';s=p.read_text().replace("'tooling/build_catalog.py','tooling/parsers.py'","'tooling/build_catalog.py','tooling/cache_fingerprint.py','tooling/parsers.py'")
anchor="  report=next(x for x in per_source if x['source_id']==s['source_id']);report.update(xml_documents=0,items=0,annotations=0,mapped_annotations=0,status='parsed')"
assert anchor in s;s=s.replace(anchor,anchor+"\n  if s.get('platform_api_version') is not None and str(s['platform_api_version']) not in exact_index_targets:\n   report['status']='partial';issue(s,None,'No imported SDK API index for annotation platform '+str(s['platform_api_version']),s['path'],'annotation-platform')")
p.write_text(s)
p=C/'tests/test_source_annotation_targets.py';s=p.read_text().replace('import build_catalog as b,sdk_api_index','import build_catalog as b,sdk_api_index,cache_fingerprint');anchor="  (r/name).write_bytes(data);s=dict";assert anchor in s;s=s.replace(anchor,"  if 'metadata_cache' in extra:extra['metadata_cache'].setdefault('parser_sha256',cache_fingerprint.current(kind))\n"+anchor);p.write_text(s)
p=C/'tests/test_source_integration.py';s=p.read_text().replace('import build_catalog as b','import build_catalog as b\nimport cache_fingerprint');s=s.replace('records=len(records)))','records=len(records),parser_sha256=cache_fingerprint.current(kind)))');p.write_text(s)
p=R/'design/Android MCP Bridge Design.md';s=p.read_text().replace('not135,000 independent shadow permission rows','not hundreds of thousands of independent shadow permission rows').replace('This revision resolves recovered findings F-01–F-08 against current artifacts.','This revision addresses the recovered design/contract findings; the source-coverage finding remains open and is tracked in the latest ledger.')
if '[S22]' not in s:s=re.sub(r'\bS([0-9]+)\b',lambda m:'S'+str(int(m[1])-1) if int(m[1])>=23 else m[0],s)
s+='''

## Device capacity and latency acceptance budgets

These are engineering acceptance targets, not claimed measurements. The reference deployment profile is64-bit Android35+ with8GiB RAM and128GB storage. Broader support must meet the same budgets and all platform gates. Release005's complete SQLite catalog is7,841,099,776 bytes (about7.30GiB); raw source archives and JSON audit exports stay off-device. Keep the entire admitted inventory, never a curated subset to meet a budget.

Before accepting an atomic catalog replacement, require free space >= active_catalog_bytes + incoming_catalog_bytes + bounded_staging_scratch_bytes + configured_log_and_config_quota +1GiB safety reserve. Conservatively budget scratch at incoming_catalog_bytes; the current full-catalog profile therefore requires roughly24GiB available during replacement. Retain the previous immutable catalog until validation and pointer publication succeed; insufficient space yields CATALOG_CAPACITY_REQUIRED and no partial publication. Bridge health reports catalog readiness separately from listener and ADB health.

The headless service target is <=256MiB idle RSS and <=384MiB peak excluding OS-owned graphics buffers, SQLite page cache <=32MiB, one bounded on-demand capture at a time, and no preload of all catalog documents. All-bindings UI lists use indexed queries and virtualization: default100 entries per page, hard maximum1000. Device targets: p95 <=150ms for a metadata page/query, <=25ms for local policy lookup, <=250ms for an owner permission commit, and <=2s to first management view. Backend/ADB/network dispatch latency is measured separately and never hidden in the policy number.

Validate these budgets on each supported device/OS using the complete registry, cold/warm caches and the configured maximum client/rule/log quotas before claiming runtime readiness. Budget failures block that device profile or publication; they do not justify dropping inventory rows or claiming measured feasibility from host tests. No device benchmark or phone operation was performed in this deliverable phase.
''';p.write_text(s)
