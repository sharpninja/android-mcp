"""Offline release chunking/reassembly; no GitHub, network, credential or source mutation."""
from pathlib import Path
import argparse,hashlib,json,os,re

BLOCK=65536
DEFAULT_PART_BYTES=1024**3
MAX_PART_BYTES=2*1024**3-1

def digest(path):
    h=hashlib.sha256()
    with Path(path).open('rb') as f:
        for block in iter(lambda:f.read(BLOCK),b''):h.update(block)
    return h.hexdigest()

def split(source,destination,expected_sha256,part_bytes=DEFAULT_PART_BYTES):
    source=Path(source);destination=Path(destination)
    if not re.fullmatch('[a-f0-9]{64}',expected_sha256):raise ValueError('Require trusted whole-file SHA256')
    if source.is_symlink() or not source.is_file():raise ValueError('Require a sealed regular artifact, no symlink')
    if not 1<=part_bytes<=MAX_PART_BYTES:raise ValueError('Every part must be strictly below2GiB')
    destination.mkdir(exist_ok=False,parents=True)
    before=source.stat();whole=hashlib.sha256();rows=[];index=0
    with source.open('rb') as f:
        while True:
            head=f.read(min(BLOCK,part_bytes))
            if not head:break
            index+=1
            if index>1000:raise ValueError('More than1000parts requires an explicitly partitioned release plan')
            name=f'artifact.part-{index:04d}';p=destination/(name+'.unverified');h=hashlib.sha256();size=0
            with p.open('xb') as out:
                block=head
                while block:
                    out.write(block);h.update(block);whole.update(block);size+=len(block)
                    if size==part_bytes:break
                    block=f.read(min(BLOCK,part_bytes-size))
                out.flush();os.fsync(out.fileno())
            rows.append(dict(name=name,bytes=size,sha256=h.hexdigest()))
    after=source.stat()
    identity=lambda s:(s.st_dev,s.st_ino,s.st_size,s.st_mtime_ns)
    if identity(before)!=identity(after) or whole.hexdigest()!=expected_sha256:
        raise ValueError('Input changed or differs from trusted sealed hash; .unverified parts retained, no manifest')
    for row in rows:(destination/(row['name']+'.unverified')).rename(destination/row['name'])
    manifest=dict(format='android-mcp-release-parts-v1',artifact_name=source.name,bytes=before.st_size,sha256=whole.hexdigest(),part_bytes=part_bytes,parts=rows)
    data=(json.dumps(manifest,sort_keys=True,indent=2)+'\n').encode()
    with (destination/'manifest.json').open('xb') as f:f.write(data);f.flush();os.fsync(f.fileno())
    return manifest

def reassemble(manifest_file,destination,expected_manifest_sha256):
    manifest_file=Path(manifest_file);destination=Path(destination)
    if digest(manifest_file)!=expected_manifest_sha256:raise ValueError('Manifest hash mismatch')
    m=json.loads(manifest_file.read_bytes())
    if m.get('format')!='android-mcp-release-parts-v1':raise ValueError('Unknown manifest format')
    if not isinstance(m.get('parts'),list) or len(m['parts'])>1000:raise ValueError('Invalid part inventory')
    names=set();total=0
    for i,row in enumerate(m['parts'],1):
        if row['name']!=f'artifact.part-{i:04d}' or row['name'] in names:raise ValueError('Invalid/duplicate part path or order')
        names.add(row['name'])
        if not isinstance(row['bytes'],int) or not 1<=row['bytes']<=MAX_PART_BYTES:raise ValueError('Invalid part size')
        if not re.fullmatch('[a-f0-9]{64}',row['sha256']):raise ValueError('Invalid part hash')
        total+=row['bytes']
    if total!=m['bytes'] or not re.fullmatch('[a-f0-9]{64}',m['sha256']):raise ValueError('Invalid whole-file identity')
    if destination.exists():raise FileExistsError('Never overwrite an existing artifact')
    unverified=destination.with_name(destination.name+'.unverified');whole=hashlib.sha256();written=0
    with unverified.open('xb') as out:
        for row in m['parts']:
            part=manifest_file.parent/row['name']
            if part.is_symlink() or not part.is_file() or part.stat().st_size!=row['bytes']:raise ValueError('Missing/invalid regular part')
            ph=hashlib.sha256();count=0
            with part.open('rb') as f:
                for block in iter(lambda:f.read(BLOCK),b''):
                    count+=len(block)
                    if count>row['bytes']:raise ValueError('Part grew during read')
                    out.write(block);ph.update(block);whole.update(block);written+=len(block)
            if count!=row['bytes'] or ph.hexdigest()!=row['sha256']:raise ValueError('Part hash mismatch; output remains unverified')
        out.flush();os.fsync(out.fileno())
    if written!=m['bytes'] or whole.hexdigest()!=m['sha256']:raise ValueError('Whole artifact mismatch')
    # link is an atomic no-replace publication, then remove only our own temporary link.
    os.link(unverified,destination);unverified.unlink()
    return dict(bytes=written,sha256=whole.hexdigest())

if __name__=='__main__':
    p=argparse.ArgumentParser();sub=p.add_subparsers(dest='operation',required=True)
    a=sub.add_parser('split');a.add_argument('source');a.add_argument('destination');a.add_argument('--sha256',required=True);a.add_argument('--part-bytes',type=int,default=DEFAULT_PART_BYTES)
    a=sub.add_parser('reassemble');a.add_argument('manifest');a.add_argument('destination');a.add_argument('--manifest-sha256',required=True)
    args=p.parse_args()
    if args.operation=='split':r=split(args.source,args.destination,args.sha256,args.part_bytes)
    else:r=reassemble(args.manifest,args.destination,args.manifest_sha256)
    print(json.dumps(r,indent=2))
