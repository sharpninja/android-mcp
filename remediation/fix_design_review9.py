import pathlib,json,datetime
R=pathlib.Path(__file__).resolve().parent;D=R/'design';p=D/'Android MCP Bridge Design.md';text=p.read_text()
text=text.replace('The current completed release006 SQLite catalog is','The measured release006 reference SQLite catalog is')
text=text.replace('This is the entire selected corpus, not global coverage.','It contains all39,194 downloaded sources of that frozen inventory;3,285 selected paths remain not retrieved. This historical engineering footprint is not a claim of current/global source completeness; preflight uses actual active and incoming bytes.')
needle='default100 entries per page, hard maximum1000'
if needle in text:text=text.replace(needle,needle+' through application query interfaces over local SQLite; the administration API remains capped at500 per request')
else:
 text+='\nThe Avalonia inventory browser uses application query interfaces over local SQLite (default100, maximum1000 per page); view models never issue SQL. The administration API remains capped at500 per request.\n'
text+='\nDerived device views have a configured byte quota and bounded history. Pin snapshots used by admitted calls and live cursors; collect only unreferenced expired derived snapshots through trusted maintenance. Never delete immutable catalog/source or audit evidence for view-cache pressure. If a complete view cannot be staged within quota, retain the old snapshot as stale and report capacity failure; no partial activation. Package extension_level metadata is the SDK package\'s base extension metadata, not an observation of the running device.\n'
p.write_text(text)
for p in (R/'catalog/README.md',R/'catalog/API-CONTRACT.md'):
 t=p.read_text().replace('A complete selected corpus is not global coverage:','All39,194 downloaded sources of the frozen006inventory were validated;3,285 selected paths remain not retrieved. Global coverage is not established:').replace('Canonicalizationv2','Canonicalization scheme2').replace('Identity scheme 2 reserves','Canonicalization scheme2 reserves')
 p.write_text(t)
print(json.dumps(dict(updated_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),fixed='R9documentation precision; newstartup references stillpendingfreshHV')))
