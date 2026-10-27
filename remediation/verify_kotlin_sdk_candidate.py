import urllib.request,pathlib,json,hashlib,datetime
D=pathlib.Path(__file__).resolve().parent/'evidence';api='https://api.github.com/repos/modelcontextprotocol/kotlin-sdk'
def get(url):
 with urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'Android-MCP-design-source-verification'}),timeout=45) as r:return r.read()
release=json.loads(get(api+'/releases/latest'));tag=release['tag_name'];commit=json.loads(get(api+'/commits/'+tag));pin=commit['sha']
readme=get('https://raw.githubusercontent.com/modelcontextprotocol/kotlin-sdk/'+pin+'/README.md');(D/'kotlin-sdk-candidate-readme.md').write_bytes(readme)
result=dict(verified_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),tag=tag,commit=pin,published_at=release['published_at'],url=release['html_url'],readme_sha256=hashlib.sha256(readme).hexdigest(),scope='Named evaluation candidate, not Android compatibility or pinned2025-11-25 conformance proof')
(D/'kotlin-sdk-candidate.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result))
