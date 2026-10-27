import pathlib,json
R=pathlib.Path(__file__).resolve().parent/'reviews/round-009';events=[json.loads(l) for l in (R/'response.visible.jsonl').read_text().splitlines()];texts=[b['text'] for e in events if e.get('type')=='assistant' for b in e.get('message',{}).get('content',[]) if b.get('type')=='text']
for i,text in enumerate(texts):print(json.dumps(dict(index=i,length=len(text),start=text[:220],end=text[-120:])))
for label,text in [('joined',''.join(texts))]+[(str(i),t) for i,t in enumerate(texts)]:
 try:
  start=text.index('{');decoded=json.JSONDecoder().raw_decode(text[start:])[0];print(json.dumps(dict(parsed=label,report_complete=decoded.get('report_complete'))))
 except (ValueError,IndexError) as error:print(label,str(error))
