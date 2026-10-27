"""Stage a coherent privacy-filtered project checkpoint; never modifies owners' files."""
from pathlib import Path
import os,json,re,hashlib,datetime,shutil,collections
R=Path(__file__).resolve().parents[1];SOURCE=Path('/home/sharpninja/github/2026-10-26/task/android-catalog-20261026');OUT=R.parent/'publication/android-mcp'
OUT.mkdir(parents=True,exist_ok=True)
if (OUT/'publication-manifest.json').exists():raise SystemExit('Existing checkpoint manifest; inspect before restaging')
now=lambda:datetime.datetime.now(datetime.timezone.utc).isoformat();sha=lambda b:hashlib.sha256(b).hexdigest()
excluded_dirs={'.git','.venv','node_modules','.tools','.gradle','.gradle-home','__pycache__','.aws','.codex','.agents','.claude','.ssh','.azure','.npm'}
private_names={'AGENTS.md','AGENTS-README-FIRST.yaml','CLAUDE.md','MEMORY.md','NOTES.md','session-state.yaml','current-turn.yaml'}
patterns={'private_key':re.compile(rb'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----'),'github_token':re.compile(rb'(?:ghp_|github_pat_)[A-Za-z0-9_]{25,}'),'anthropic_token':re.compile(rb'sk-ant-[A-Za-z0-9_-]{25,}'),'openai_token':re.compile(rb'sk-(?:proj-|svcacct-)[A-Za-z0-9_-]{25,}'),'signed_transfer_url':re.compile(rb'https?://[^\s"<>]+[?&](?:X-Amz-Signature|X-Goog-Signature|sig|signature|token)=[^\s"<>]+',re.I)}
metadata_keys={'plugins','slash_commands','terminal_slash_commands','skills','agents','messaging_socket_path','apiKeySource','rateLimitInfo','rate_limit_info','account_id','organization_id','organizationUuid','accountUuid'}
rows=[];exclusions=[];redactions=[];totals=collections.Counter();LIMIT=50*1024*1024
def clean(value,path,changes):
 if isinstance(value,dict):
  if value.get('type') in ('thinking','redacted_thinking'):changes.append(path+':hidden_reasoning');return dict(type='publication_redaction',category='hidden_reasoning',original_sha256=sha(json.dumps(value,sort_keys=True).encode()))
  if value.get('type')=='rate_limit_event':changes.append(path+':account_quota');return dict(type='publication_redaction',category='account_quota',original_sha256=sha(json.dumps(value,sort_keys=True).encode()))
  result={}
  for k,v in value.items():
   if k in metadata_keys:changes.append(path+'.'+k);continue
   result[k]=clean(v,path+'.'+k,changes)
  return result
 if isinstance(value,list):return [clean(v,path+'['+str(i)+']',changes) for i,v in enumerate(value)]
 return value
for label,base in [('remediation',R),('source-project',SOURCE)]:
 for current,dirs,names in os.walk(base,followlinks=False):
  path=Path(current);rel=path.relative_to(base)
  for name in list(dirs):
   p=path/name
   if name in excluded_dirs or p.is_symlink():
    dirs.remove(name);exclusions.append(dict(path=str(Path(label)/rel/name),reason='dependency/auth/operator cache or symlink; not project publication content'))
  for name in sorted(names):
   p=path/name;relative=Path(label)/rel/name;item=dict(path=str(relative))
   if p.is_symlink() or name in private_names or re.search(r'(^\.env(?:\.|$)|^adbkey|\.(?:p12|pfx|jks|keystore)$|credential|upload.session|private.note|^\.secret)',name,re.I):
    exclusions.append(dict(**item,reason='credential/private operator material or symlink; not read or published'));continue
   before=p.stat();item.update(size_bytes=before.st_size,mtime_ns=before.st_mtime_ns)
   receipt=any(part.startswith(('hv-','round-')) for part in rel.parts) or 'reviews' in rel.parts
   source_data=label=='source-project' and (any(part.startswith(('cache','snapshots','sdk','outside-corpus-audit')) for part in rel.parts) or ('output' in rel.parts and not re.search('validation|manifest|receipt|status|coverage',name,re.I)))
   archive=p.suffix.lower() in ('.zip','.gz','.zst','.xz','.tgz','.tar','.7z','.nupkg')
   approved_design_zip=label=='remediation' and rel==Path('design-c4/build/distributions') and name=='Android MCP Bridge Design Portable.zip'
   if before.st_size>=LIMIT or p.suffix.lower() in ('.sqlite','.sqlite3','.db') or name.endswith(('-wal','-shm')) or source_data or (p.suffix=='.jsonl' and not receipt) or (archive and not approved_design_zip):
    rows.append(dict(**item,storage='release_asset_pending',sha256=None,reason='Dataset/large/history archive: requires sealed hash-verified compressed release assets; not silently omitted'));totals['deferred_bytes']+=before.st_size;totals['deferred_files']+=1;continue
   raw=p.read_bytes();after=p.stat()
   if (before.st_size,before.st_mtime_ns)!=(after.st_size,after.st_mtime_ns) and not (receipt and p.suffix=='.jsonl'):
    rows.append(dict(**item,storage='release_asset_pending',sha256=None,reason='Owner file changed during copy; pending coherent seal'));totals['deferred_files']+=1;totals['deferred_bytes']+=after.st_size;continue
   changes=[];published=raw;prefix=False
   if receipt and p.suffix in ('.json','.jsonl'):
    try:
     if p.suffix=='.jsonl':
      lines=raw.splitlines(keepends=True)
      if lines and not lines[-1].endswith(b'\n'):lines.pop();prefix=True
      published=(''.join(json.dumps(clean(json.loads(line),'event'+str(i),changes),ensure_ascii=False)+'\n' for i,line in enumerate(lines))).encode()
     else:published=(json.dumps(clean(json.loads(raw),'$',changes),ensure_ascii=False,indent=2)+'\n').encode()
    except (ValueError,UnicodeError):
     rows.append(dict(**item,storage='release_asset_pending',sha256=sha(raw),reason='Receipt is not complete valid JSON; preserve local original, pending safe publication reconstruction'));totals['deferred_files']+=1;totals['deferred_bytes']+=len(raw);continue
   hits=[k for k,v in patterns.items() if v.search(published)]
   if hits:
    rows.append(dict(**item,storage='privacy_review_pending',sha256=sha(raw),reason='Potential secret/transfer signature; values never exported',categories=hits));totals['privacy_pending_files']+=1;continue
   target=OUT/relative;target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(published)
   if p.stat().st_mode&0o111:target.chmod(0o755)
   item.update(storage='git',source_sha256=sha(raw),published_sha256=sha(published),published_bytes=len(published),captured_complete_jsonl_prefix=prefix)
   if changes: redactions.append(dict(path=str(relative),original_sha256=sha(raw),published_sha256=sha(published),removed_structured_metadata_paths=changes,visible_project_request_and_response_text_preserved=True))
   rows.append(item);totals['git_files']+=1;totals['git_bytes']+=len(published)
manifest=dict(created_at=now(),status='NOT_ACCEPTED; checkpoint publication explicitly requested regardless of HV',latest_completed_design_review=dict(round='022',accuracy=92,completeness=89),active_review='023; immutable inputs and running processes not modified',roots=['remediation','source-project'],files=rows,exclusions=exclusions,receipt_redactions=redactions,totals=dict(totals),all_large_artifacts_synced=False,privacy_pending_values_never_exported=True,originals_modified=False)
(OUT/'publication-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
(OUT/'README.md').write_text('''# Android MCP bridge — checkpoint, acceptance pending

All-Kotlin server and Compose management UI design, internal Kotlin ARC compatibility port plan, catalog contracts and offline reference tooling. No Android app implementation or deployment is claimed.

Latest complete independent design review: **round022, accuracy92/100, completeness89/100, NOT_ACCEPTED**. Corrected round023 is running; this repository is a requested checkpoint, not98/98 acceptance.

- [Current Markdown design](<remediation/design-c4/docs/Android MCP Bridge Design.md>)
- [C4/Structurizr and Gradle docs project](remediation/design-c4/README.md)
- [SVG screens and storyboards](remediation/design-c4/docs/ui-screens-and-storyboards.md)
- [Marp slideshow](remediation/design-c4/docs/storyboards.html)
- [Catalog and reference contracts](remediation/catalog/README.md)
- [Complete visible round022 verdict](remediation/reviews/round-022/supervisor/unified-review-report.txt)
- [Checkpoint inventory and pending large assets](publication-manifest.json)

The manifest records every in-scope project file, publication transforms and pending release assets. Large datasets/databases/history archives are pending compressed, chunked release transfer with per-part/whole hashes and reconstruction tooling. A normal Git push does not mean those assets are synchronized. No paid LFS/storage commitment is made.

Published review receipts retain project requests and visible responses. Account-quota/plugin/operator metadata and hidden reasoning are explicitly removed in audited publication copies; immutable local originals remain preserved. Active JSONL may be a declared captured prefix and will be updated after completion. Credentials, personal/unrelated logs and private operator notes are excluded.
''')
(OUT/'.gitignore').write_text('**/__pycache__/\n**/.tools/\n**/.gradle-home/\n**/.gradle/\n**/node_modules/\n**/.venv/\n*.part.unverified\n')
print(json.dumps(dict(checkout=str(OUT),totals=dict(totals),manifest=str(OUT/'publication-manifest.json'),originals_modified=False,repo_mutation=False)))
