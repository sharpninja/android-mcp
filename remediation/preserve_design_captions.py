import pathlib,re,json
D=pathlib.Path(__file__).resolve().parent/'design';p=D/'Android MCP Bridge Design.md';text=p.read_text()
captions={'architecture':'The generated registry exposes the declared binding surface. Every invocation shares policy; ADB supplies generic screen state and taps, and Avalonia runtime is optional.','request-sequence':'The local owner approves immutable Ask requests. Logging is optional and bounded; mandatory admission audit precedes dispatch.'}
for name,caption in captions.items():
 if caption not in text:
  pattern=r'(!\[[^\]]*\]\(assets/'+re.escape(name)+r'\.svg\))'
  text,n=re.subn(pattern,lambda m:m.group(1)+'\n\n'+caption,text,count=1)
  if n!=1:raise ValueError('Missing diagram caption anchor '+name)
text=text.replace('Design proposal  •  26 October 2026  •  For architecture and implementation planning','Design specification  •  27 October 2026  •  Architecture, catalog and reference implementation; Android runtime not deployed',1)
p.write_text(text);print(json.dumps(dict(docx_only_descriptive_captions_preserved=2,rendered_pele_notes_preserved_in_docx=True,markdown=str(p))))
