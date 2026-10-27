"""Validate every saved PNG and preserve the separate contact-sheet timeout."""
from pathlib import Path
import struct,zlib,hashlib,json,datetime
R=Path(__file__).resolve().parent;P=R/'design-c4';D=R/'design-c4-preview/all-slides-v8';E=P/'build/receipts'
m=json.loads((P/'docs/ui-asset-manifest.json').read_text());expected=1+sum(len(x['steps']) for x in m['storyboards'])
rows=[]
for i in range(1,expected+1):
    p=D/f'{i:03}.png';raw=p.read_bytes();assert raw[:8]==b'\x89PNG\r\n\x1a\n';pos=8;data=[];width=height=color=None;end=False
    while pos<len(raw):
        size=struct.unpack('>I',raw[pos:pos+4])[0];kind=raw[pos+4:pos+8];body=raw[pos+8:pos+8+size];crc=struct.unpack('>I',raw[pos+8+size:pos+12+size])[0]
        if zlib.crc32(kind+body)&0xffffffff!=crc:raise ValueError('PNG CRC mismatch '+p.name)
        if kind==b'IHDR':width,height,depth,color,comp,filt,interlace=struct.unpack('>IIBBBBB',body)
        if kind==b'IDAT':data.append(body)
        if kind==b'IEND':end=True
        pos+=12+size
    pixels=zlib.decompress(b''.join(data));channels={2:3,6:4}[color]
    if (width,height)!=(1440,960) or depth!=8 or interlace!=0 or len(pixels)!=height*(1+width*channels) or not end or pos!=len(raw):raise ValueError('Incomplete screenshot '+p.name)
    rows.append(dict(slide=i,file=str(p),size_bytes=len(raw),sha256=hashlib.sha256(raw).hexdigest(),width=width,height=height,png_crc_and_complete_pixel_stream=True))
base=json.loads((E/'storyboard-browser-validation-v7.json').read_text())
if not base['pass'] or base['html_sha256']!=hashlib.sha256((P/'docs/storyboards.html').read_bytes()).hexdigest():raise ValueError('Browser image/render input changed')
result=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),pass_=True,all_slide_screenshots=rows,expected_count=expected,actual_count=len(rows),actual_image_rendering_and_navigation_receipt='storyboard-browser-validation-v7.json',same_html_sha256=base['html_sha256'],contact_sheet_failure=dict(step='v8 Page.captureScreenshot of optional full-page62-image contact sheet',outcome='known terminal ProtocolError default CDP capture timeout; all62individual slide screenshots and6screen/class captures completed first',effect='No individual screenshot or design output lost. Failed v8 did not write a success receipt. All saved PNGs independently CRC/decode validated here.'),implementation_claim=False)
out=E/'all-slide-capture-v8.json'
with out.open('x') as f:f.write(json.dumps(result,indent=2)+'\n')
print(json.dumps(dict(all_slide_screenshots=len(rows),expected=expected,crc_and_pixels='passed',base_browser_receipt='v7 passed',contact_sheet='failed separately, retained')))
