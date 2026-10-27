import pathlib,sys,json
R=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(R/'catalog/tooling'))
import java_source
original=java_source.annotations
def capture(text):
 try:return original(text)
 except ValueError:
  (R/'sdk37.2/annotation-failure-input.json').write_text(json.dumps(text));raise
java_source.annotations=capture
java_source.parse((R/'snapshots/blobs/1cbce7e2847c78089149f953cbc29295f0e306e2').read_bytes(),'src/android/content/Context.java')
