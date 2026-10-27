"""Recover combined014 through strictly scoped built-in Read; no file modification tools."""
import pathlib,json,hashlib,shutil,subprocess,datetime,sys
R=pathlib.Path(__file__).resolve().parent;D=R/'reviews/round-015';old=R/'reviews/round-014'
if D.exists():raise SystemExit('015 already exists; inspect before retry')
if json.loads((old/'completion.json').read_text())['exit_code']!=1:raise SystemExit('Unexpected014outcome')
for p in (R/'reviews').glob('round-*/process.json'):
 state=json.loads(p.read_text())
 if state.get('status')=='running' and not (p.parent/'completion.json').exists():raise SystemExit('Potential active reviewer '+str(p))
D.mkdir();S=D/'input';S.mkdir();sha=lambda b:hashlib.sha256(b).hexdigest()
manifest=json.loads((old/'input-manifest.json').read_text());rows=[]
for row in manifest['files']:
 relative=pathlib.PurePosixPath(row['file'])
 if relative.is_absolute() or '..' in relative.parts:raise ValueError('Unsafe manifest path')
 source=old/'input'/row['file']
 if source.is_symlink() or not source.is_file() or sha(source.read_bytes())!=row['sha256']:raise ValueError('Unverified frozen input '+row['file'])
 target=S/row['file'];target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(source,target);target.chmod(0o444);rows.append(row)
index=dict(approved_source_manifest_sha256=sha((old/'input-manifest.json').read_bytes()),files=rows,scope='Only frozen combined project artifacts; no credentials, personal logs or unrelated files; publicARC dependency contracts included',no_android_implementation=True,working_source_metadata_changes_after014_not_in_this_baseline=True)
(S/'REVIEW-INDEX.json').write_text(json.dumps(index,indent=2)+'\n');(S/'REVIEW-INDEX.json').chmod(0o444)
rows += [dict(file='REVIEW-INDEX.json',sha256=sha((S/'REVIEW-INDEX.json').read_bytes()),size_bytes=(S/'REVIEW-INDEX.json').stat().st_size)]
now=lambda:datetime.datetime.now(datetime.timezone.utc).isoformat()
(D/'input-manifest.json').write_text(json.dumps(dict(created_at=now(),files=rows),indent=2)+'\n')
settings=dict(permissions=dict(blockReadsOutsideWorkingDirectories=True,allow=['Read(/'+str(S)+'/**)'],deny=['Bash','PowerShell','Edit','Write','NotebookEdit','WebFetch','WebSearch','Agent','Task']))
(D/'read-only-settings.json').write_text(json.dumps(settings,indent=2)+'\n')
instruction=(R/'combined-review-instruction.txt').read_text()
instruction=instruction.replace('You have NO TOOLS.','Only the built-in Read tool is available, confined by restricted mode to this immutable input working directory. No shell, write/edit, browser/network, agent or MCP tools are available.')
instruction=instruction.replace('Do not execute, edit, retrieve unrelated files, operate devices or disclose hidden thinking.','Do not execute code, modify files, read outside the immutable input, operate devices or disclose hidden thinking.')
instruction=instruction.replace('no tools means you do not perform your own visual inspection/test execution.','read-only tools do not execute tests or visually render SVG/browser artifacts; do not claim your own execution.')
prompt=instruction+'\nREAD-ONLY RECOVERY OF FAILED014\nThe entire exact208-file014 baseline is in your working directory. It includes the latest delivered design candidate and completed catalog/reference baseline. Later local schema integration work is intentionally outside this frozen recovery baseline; do not imply it was assessed.014 failed PromptTooLong before inference; this is one holistic successor, not separate category reviews.\nRead REVIEW-INDEX.json first. Then read every relevant supplied artifact in bounded line ranges. For large files use offset/limit paging and account for truncation; do not claim unread ranges inspected. All manifest files remain in scope. Read-only inspection can use multiple Read calls per assistant turn; no code execution, shell/network or edits. Do not read settings, credentials, session files or anything outside cwd.\nReturn final combined JSON once the review is complete. Add coverage:{files_read:[{file,ranges,complete}],files_not_fully_read:[{file,reason}],requirements_assessed,coverage_limits}. Declare any missing coverage and its score impact honestly; no arbitrary scope omission. Preserve all material prior/current findings, exact source evidence, per-area/overall scores and complete verdict. Do not output a final fragment or mark report_complete true until your report covers the whole requested baseline.\n'
(D/'prompt.txt').write_text(prompt)
for directory in sorted((p for p in S.rglob('*') if p.is_dir()),key=lambda p:len(p.parts),reverse=True):directory.chmod(0o555)
S.chmod(0o555)
source=(R/'review_design_round.py').read_text();tail=source[source.index("cli='/home/sharpninja/.local/share/mise/installs/claude/2.1.300/claude'"):]
tail=tail.replace("'--tools','','--permission-mode'","'--restricted','--tools','Read','--allowedTools','Read(/'+str(S)+'/**)','--settings',str(D/'read-only-settings.json'),'--permission-mode'")
tail=tail.replace("'--max-turns','1'","'--max-turns','120','--autocompact','160k'")
tail=tail.replace('cwd=D,stdin=','cwd=S,stdin=').replace('tools=[],prompt_sha256','tools=[\'Read\'],read_scope=str(S),restricted=True,prompt_sha256')
number=15
print(json.dumps(dict(prepared_at=now(),files=len(rows),prompt_bytes=len(prompt.encode()),manifest_sha256=sha((D/'input-manifest.json').read_bytes()),read_scope=str(S),tools=['Read'],restricted=True)),flush=True)
exec(compile(tail,str(R/'review_design_round.py')+'<restricted-recovery>','exec'))

