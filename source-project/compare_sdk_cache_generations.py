import pathlib,json,hashlib,datetime
R=pathlib.Path(__file__).resolve().parent;D=R/'sdk37.2'
old=json.loads((D/'java-extraction-generation001-report.json').read_text());new=json.loads((D/'java-extraction-report.json').read_text())
def records(report):return {pathlib.Path(s['cache_path']).name:(s['cache_sha256'],s['records']) for s in report['source_reports']}
a,b=records(old),records(new);changed=[k for k in a if a[k]!=b.get(k)]
result=dict(checked_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),generation001_parser_sha256=old['parser_sha256'],generation002_parser_sha256=new['parser_sha256'],java_files_old=old['java_files'],java_files_new=new['java_files'],declarations_old=old['declaration_records'],declarations_new=new['declaration_records'],error_regions_old=old['parse_error_regions'],error_regions_new=new['parse_error_regions'],unique_cache_blobs_old=len(a),unique_cache_blobs_new=len(b),changed_cache_blobs=changed,source_archive_sha256=new['source_archive_sha256'],original_caches_preserved='sdk37.2/java-cache',new_caches='sdk37.2/java-cache-v2')
if a.keys()!=b.keys() or changed or old['java_files']!=new['java_files'] or old['declaration_records']!=new['declaration_records'] or new['parse_error_regions']:raise ValueError(json.dumps(result))
(D/'cache-generation-comparison.json').write_text(json.dumps(result,indent=2));print(json.dumps(result,indent=2))
