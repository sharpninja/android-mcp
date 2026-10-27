"""Read-only reproduction of pinned Google AIDL hash tool's source algorithm.
No downloaded build script/compiler is executed. Frozen version != Android API.
"""
import pathlib,hashlib,re
def compute(files,version):
 if not isinstance(version,str) or not re.fullmatch('[1-9][0-9]*',version):raise ValueError('Positive frozen version required; current is not a version')
 if not files:raise ValueError('No source files')
 h=hashlib.sha1()
 for path,data in sorted(files.items()):
  pure=pathlib.PurePosixPath(path)
  if pure.is_absolute() or '..' in pure.parts or str(pure)!=path or '\\' in path or '\n' in path or '\r' in path or not path.endswith('.aidl'):raise ValueError('Unsafe or noncanonical frozen dump path')
  h.update((hashlib.sha1(data).hexdigest()+'  ./'+path+'\n').encode('utf-8'))
 h.update(('latest-version' if version=='1' else str(int(version)-1)).encode()+b'\n')
 return h.hexdigest()
def verify(files,expected_paths,version,published):
 if set(files)!=set(expected_paths) or len(expected_paths)!=len(set(expected_paths)):
  return dict(verified=False,unknown_reason='Full pinned-tree frozen dump file membership is not reconciled',missing=sorted(set(expected_paths)-set(files)),extra=sorted(set(files)-set(expected_paths)))
 lines=[line.strip() for line in published.splitlines() if line.strip()]
 selected=lines[-1] if lines else None
 if not isinstance(selected,str) or not re.fullmatch('[0-9a-f]{40}',selected):return dict(verified=False,unknown_reason='Last nonempty published hash line is not lowercase hex40')
 computed=compute(files,version)
 return dict(verified=computed==selected,computed_hash=computed,selected_published_hash=selected,selected_line_policy='last-nonempty-line',files=len(files),version_literal=version,hash_seed='latest-version' if version=='1' else str(int(version)-1),runtime_observation=False)
