import pathlib,zipfile,json,re,unicodedata,hashlib,xml.etree.ElementTree as ET
R=pathlib.Path(__file__).resolve().parent;D=R/'design';B=D/'library-baseline'
def norm(value):
 value=re.sub(r'(?m)^\s*\d+[.)]\s+','',value)
 value=re.sub(r'\[([^\]]+)\]\([^\n]*?\)',r'\1',value)
 return re.sub(r'\W+','',unicodedata.normalize('NFKC',value).lower())
md=(B/'Android MCP Bridge Design.md').read_text();raw=norm(md)
with zipfile.ZipFile(B/'Android MCP Bridge Design.docx') as z:
 root=ET.fromstring(z.read('word/document.xml'));ns={'w':'http://schemas.openxmlformats.org/wordprocessingml/2006/main'}
 paragraphs=[''.join(p.itertext()) for p in []] # populated only from Word text runs
 paragraphs=[''.join(p.itertext()) for p in root.findall('.//w:p',ns)]
 paragraphs=[''.join(t.text or '' for t in p.findall('.//w:t',ns)) for p in root.findall('.//w:p',ns)]
 unmatched=[p for p in paragraphs if len(norm(p))>35 and norm(p) not in raw]
 tracked=len(root.findall('.//w:ins',ns))+len(root.findall('.//w:del',ns));comments=any('comment' in n.lower() for n in z.namelist())
with zipfile.ZipFile(B/'Android MCP Bridge Design Portable.zip') as z:
 portable_md=z.read('Android MCP Bridge Design.md');portable_matches=portable_md==(B/'Android MCP Bridge Design.md').read_bytes()
out=dict(current_library_versions=json.loads((D/'library-baseline-identities.json').read_text()),markdown_matches_source_task_baseline=hashlib.sha256((B/'Android MCP Bridge Design.md').read_bytes()).hexdigest()==hashlib.sha256(pathlib.Path('/home/sharpninja/github/2026-10-26/task/android-catalog-20261026/design-input/Android MCP Bridge Design.md').read_bytes()).hexdigest(),portable_markdown_matches_library=portable_matches,docx_tracked_changes=tracked,docx_comments=comments,docx_unmatched_paragraphs=unmatched,limitations='Comparison is evidence for preserving content; any unmatched paragraphs require inspection before regeneration. No Library write performed.')
(R/'evidence/design-baseline-comparison.json').write_text(json.dumps(out,indent=2)+'\n');print(json.dumps(dict(markdown_matches=out['markdown_matches_source_task_baseline'],portable_matches=portable_matches,unmatched_docx_paragraphs=len(unmatched),examples=unmatched[:8],tracked=tracked,comments=comments)))
