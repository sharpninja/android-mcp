import pathlib,json,datetime,hashlib,collections,re,zipfile
R=pathlib.Path(__file__).resolve().parent;D=R/'reviews/round-014';manifest=json.loads((D/'input-manifest.json').read_text());request=json.loads((D/'request.jsonl').read_text());completion=json.loads((D/'completion.json').read_text());now=datetime.datetime.now(datetime.timezone.utc).isoformat()
def category(path):
 if path.startswith('design-c4/'):
  if '/build/receipts/' in path:return 'local_documentation_build_validation'
  if '/ui-svg/' in path or '/storyboards/' in path:return 'editable_ui_storyboards'
  return 'current_design_model_svg_build'
 if '/arc-upstream/' in '/'+path:return 'public_pinned_ARC_contract_source'
 if path.startswith('catalog/contracts/') or path in ('catalog/API-CONTRACT.md','catalog/bridge-config.sql','catalog/schema.sql'):return 'catalog_schema_api_policy'
 if path.startswith('catalog/tests/'):return 'offline_fixture_tests'
 if path.startswith('catalog/tooling/'):return 'extractor_and_offline_reference_code'
 if path.startswith('reviews/') or 'finding-ledger' in path:return 'prior_completed_findings_regression_evidence'
 return 'Google_derived_catalog_validation_and_project_evidence'
groups=collections.defaultdict(lambda:dict(files=0,bytes=0));rows=[]
for item in manifest['files']:
 path=item['file'];group=category(path);groups[group]['files']+=1;groups[group]['bytes']+=item['size_bytes'];rows.append(dict(**item,category=group))
excluded=['Credential stores/configs/env files and tokens','Personal messages/logs and unrelated repositories','Codex session files or hidden thinking','Phone/device data and settings','Full catalog SQLite/JSONL/raw acquisition corpus','Any file outside this frozen input directory; no symlink traversal']
blocked=dict(at=now,action='Recovery of one combined hostile review with read-only reviewer access to frozen208-file input directory',target=str(D/'input'),status='denied; specific approval requested by parent and pending',reason='Automatic approval review rejected proposed recovery over expanded payload/destination scope',denial_evidence_verbatim='Important: proposed recovery allowing Claude read access to208-file frozen directory was DENIED by approval review over payload/destination scope. Do not enable external reviewer file tools or transmit expanded combined artifact payload pending specific user approval.',operation_executed_here=False,retries_after_denial=0,original_round014_outcome=dict(exit_code=completion['exit_code'],error='Prompt is too long',actual_models=completion['actual_models'],provider_input_tokens=completion['result_metadata']['usage']['input_tokens'],provider_output_tokens=completion['result_metadata']['usage']['output_tokens'],report_complete=False),frozen_manifest_sha256=request['manifest_sha256'],frozen_prompt_sha256=request['prompt_sha256'],files=len(rows),payload_bytes=sum(r['size_bytes'] for r in rows),prompt_bytes=(D/'prompt.txt').stat().st_size,proposed_destination='Installed ClaudeCode configured provider; user approval must cover the exact bounded files and destination. No credential/configuration inspected to infer routing.',categories=dict(groups),exclusions=excluded,files_manifest=rows)
(R/'evidence/combined014-sharing-scope-and-blocker.json').write_text(json.dumps(blocked,indent=2)+'\n')
published=[]
for name in ('markdown','portable'):
 receipt=json.loads((R/'publication-c4'/(name+'.receipt.json')).read_text());published.append(dict(local_path=receipt['local_path'],library_file_id=receipt['result']['library_file_id'],file_id=receipt['result']['file_id'],version=receipt['result']['current_version_number'],status=receipt['result']['status']))
package=R/'design-c4/build/distributions/Android MCP Bridge Design Portable.zip';found=[]
patterns=[r'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----',r'sk-ant-api\d+-[A-Za-z0-9_-]{25,}',r'AKIA[A-Z0-9]{16}']
with zipfile.ZipFile(package) as z:
 for name in z.namelist():
  if name.endswith('/') or name.endswith('.jar'):continue
  text=z.read(name).decode('utf-8',errors='replace')
  for pat in patterns:
   if re.search(pat,text):found.append(dict(file=name,pattern=pat))
if found:raise ValueError('Credential signature found in design archive; investigate without exposing values')
archive=json.loads((R/'evidence/current-design-archive-validation.json').read_text());delivery=dict(at=now,files=published,archive_validation=archive,high_confidence_private_key_token_signatures_found=0,design_only=True,latest_combined_review_complete=False,review_blocker='evidence/combined014-sharing-scope-and-blocker.json',latest_completed_design_review='01391accuracy88completeness; predates final current requirements',latest_completed_full_project_review='01293accuracy80completeness',acceptance_98_98=False)
(R/'publication-c4/delivery-summary.json').write_text(json.dumps(delivery,indent=2)+'\n')
live=json.loads((R/'LIVE-STATUS.json').read_text());live.update(updated_at=now,current_phase='Current design delivered Markdown8/ZIP5. Combined014 failed prompt length; read-tool recovery denied/pending specific approval.',review_pid=None,review_phase='014FAILED before API inference; no current combined verdict. Expanded recovery authorization pending.',review_scope_current=True,current_design_review_accepted=False,combined_review_blocker='evidence/combined014-sharing-scope-and-blocker.json',library_upload='publication-c4/delivery-summary.json',design_library_versions={f['library_file_id']:f['version'] for f in published},new_library_uploads='Current design Markdown8/portableZIP5 saved; historicalDOCX5 unchanged. Designbuild receipts only; no source/catalog review-evidence bundle.',current_build='Gradle wrapper8.14.3 offline validate005 and package001 exit0;29unique vectorSVGs,13Marpslides navigationpass',current_package_sha256=archive['zip_sha256'],latest_available_production_reviewed=False,receipt_audit='Through014 pending updated audit; originals all retained')
(R/'LIVE-STATUS.json').write_text(json.dumps(live,indent=2)+'\n');print(json.dumps(dict(delivered=published,blocked_action=blocked['action'],status=blocked['status'],categories=dict(groups),files=len(rows),no_review_retry=True)))
