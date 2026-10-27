import urllib.request,pathlib,hashlib,json,datetime,re,html
R=pathlib.Path(__file__).resolve().parent;url='https://developer.android.com/reference/android/provider/Settings.System'
with urllib.request.urlopen(urllib.request.Request(url,headers={'Accept-Encoding':'identity'}),timeout=30) as response:data=response.read();final=response.geturl();status=response.status
p=R/'evidence/brightness-reference-page.html';p.write_bytes(data);text=html.unescape(re.sub('<[^>]+>',' ',data.decode()));text=re.sub(r'\s+',' ',text)
needle='between 1 (minimum) and 255 (maximum)';found=needle in text
receipt=dict(retrieved_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),request_url=url,final_url=final,http_status=status,snapshot_sha256=hashlib.sha256(data).hexdigest(),snapshot_bytes=len(data),snapshot_file=str(p),expected_exact_quote_found=found,exact_short_quote=needle if found else None,device_operations=False)
(R/'evidence/brightness-reference-page.json').write_text(json.dumps(receipt,indent=2));print(json.dumps(receipt))
if not found:raise SystemExit('Expected reference quote not found; inspect snapshot without inventing range')
