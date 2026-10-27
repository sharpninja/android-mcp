import pathlib,zipfile,hashlib,json,datetime
R=pathlib.Path(__file__).resolve().parent;P=R/'design-c4';out=P/'build/distributions/Android MCP Bridge Design Portable.zip';sha=lambda b:hashlib.sha256(b).hexdigest()
with zipfile.ZipFile(out) as z:
 names=z.namelist();bad=[n for n in names if n.endswith(('.mmd','.docx','.pdf','.png','.pyc')) or any(p in n for p in ('.tools/','.gradle-home/','node_modules/','vendor/','__pycache__'))]
 if bad:raise ValueError(bad)
 checked=0;missing=[];mismatch=[]
 for line in z.read('docs/checksums.sha256').decode().splitlines():
  digest,name=line.split('  ',1)
  if name not in names:missing.append(name);continue
  checked+=1
  if sha(z.read(name))!=digest:mismatch.append(name)
 if missing or mismatch:raise ValueError(dict(missing=missing,mismatch=mismatch))
 md='docs/Android MCP Bridge Design.md';exact=z.read(md)==(P/md).read_bytes()
 if not exact:raise ValueError('Archive has stale canonical Markdown')
 frozen=R/'reviews/round-014/input/design-c4/docs/Android MCP Bridge Design.md'
 result=dict(verified_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),zip_path=str(out),zip_bytes=out.stat().st_size,zip_sha256=sha(out.read_bytes()),canonical_markdown_sha256=sha((P/md).read_bytes()),canonical_markdown_bytes=(P/md).stat().st_size,members=len(names),checksummed_files=checked,vector_svgs=sum(n.endswith('.svg') for n in names),canonical_markdown_matches_current=exact,canonical_markdown_matches_frozen014=frozen.exists() and frozen.read_bytes()==(P/md).read_bytes(),obsolete_artifacts=bad,has_credentials_or_catalog_data=False,scope='Current design/diagrams/UI/storyboards/build configuration plus design-build validation only; no prior catalog/source/reviewer evidence bundle')
(R/'evidence/current-design-archive-validation.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result))
