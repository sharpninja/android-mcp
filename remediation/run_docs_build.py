"""Record actual bounded local Gradle execution; no Android application deployment."""
import pathlib,subprocess,json,datetime,time,hashlib,sys
R=pathlib.Path(__file__).resolve().parent;P=R/'design-c4';mode=sys.argv[1] if len(sys.argv)>1 else 'validate';index=sys.argv[2] if len(sys.argv)>2 else '001'
E=P/'build/receipts';E.mkdir(parents=True,exist_ok=True);name='gradle-'+mode+'-'+index
for suffix in ['.request.json','.stdout.txt','.stderr.txt','.completion.json']:
 if (E/(name+suffix)).exists():raise SystemExit('Immutable build receipt already exists: '+name)
goals=['verifyDesignDocs'] if mode=='validate' else ['packageDesignDocs']
args=['docker','run','--rm','--network','none','--cap-drop','ALL','--security-opt','no-new-privileges','--hostname','android-mcp-docs','--add-host','android-mcp-docs:127.0.0.1','--user','1000:1000','--tmpfs','/tmp:rw,size=512m','-v',str(P)+':/workspace','-v','/home/sharpninja/.local/share/mise/installs/node/26.8.2/bin/node:/tool-node/node:ro','-v','/usr/lib/libatomic.so.1:/usr/lib/x86_64-linux-gnu/libatomic.so.1:ro','-e','NODE_BIN=/tool-node/node','-w','/workspace','--entrypoint','sh','structurizr/cli@sha256:0399ac8e24c16e41277cfcec22caa29e4775013e5395c0720016828426d62749','-c','./gradlew --offline --no-daemon -g .gradle-home '+' '.join(goals)]
now=lambda:datetime.datetime.now(datetime.timezone.utc).isoformat();sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
inputs=[P/'architecture/workspace.dsl',P/'architecture/code-diagrams.md',P/'build.gradle.kts',P/'toolchain-lock.json',P/'storyboards/storyboard.md',P/'docs/Android MCP Bridge Design.md']+sorted((P/'ui-svg').glob('*.svg'))
request=dict(started_at=now(),invocation=args,input_sha256={str(p.relative_to(P)):sha(p) for p in inputs});(E/(name+'.request.json')).write_text(json.dumps(request,indent=2)+'\n');t=time.monotonic()
with (E/(name+'.stdout.txt')).open('x') as out,(E/(name+'.stderr.txt')).open('x') as err:p=subprocess.Popen(args,stdout=out,stderr=err);code=p.wait()
text=(E/(name+'.stdout.txt')).read_text();result=dict(completed_at=now(),exit_code=code,elapsed_seconds=time.monotonic()-t,build_successful=code==0 and 'BUILD SUCCESSFUL' in text,stdout_sha256=sha(E/(name+'.stdout.txt')),stderr_sha256=sha(E/(name+'.stderr.txt')),svg_outputs=len(list((P/'docs').rglob('*.svg'))),actual_app_implemented=False)
(E/(name+'.completion.json')).write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result));print(text[-3000:]);raise SystemExit(code)
