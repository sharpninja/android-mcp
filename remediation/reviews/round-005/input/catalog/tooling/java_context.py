"""Carry per-file Java import context into the runtime source-detail records."""
def attach(sources,reports):
 by_cache={r['cache_path']:r for r in reports}
 for s in sources:
  if s['kind']!='java-source':continue
  cache=s['metadata_cache'];stat=by_cache.get(cache['path'])
  if not stat or stat['cache_sha256']!=cache['sha256'] or stat['parser_sha256']!=cache['parser_sha256']:raise ValueError('Java context cache mismatch '+s['path'])
  s['java_file_context']={k:stat.get(k) for k in ('package','imports','wildcard_imports','parser','parse_errors','excluded_implementation_nodes')}
  s['java_file_context']['static_import_resolution']='Static/type imports not separately classified; original source retained. No unresolved wildcard/type-variable identity inferred.'
