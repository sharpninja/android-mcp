#!/usr/bin/env python3
"""Verify committed document links, asset checksums and offline presentation assets."""
import argparse
import hashlib
from html.parser import HTMLParser
import json
from pathlib import Path
import re
import urllib.parse
import xml.etree.ElementTree as ET


def check(root):
    design=root/'remediation/design-c4'; errors=[];checksums=links=0
    for line in (design/'docs/checksums.sha256').read_text().splitlines():
        expected,relative=line.split('  ',1);checksums+=1;target=design/relative
        if not target.is_file() or hashlib.sha256(target.read_bytes()).hexdigest()!=expected:
            errors.append('Missing or changed design checksum: '+relative)
    for path in root.rglob('*.md'):
        if '.git' in path.relative_to(root).parts:continue
        for target in re.findall(r'\]\(([^)]+)\)',path.read_text()):
            target=target.strip('<>').split('#')[0]
            if not target or urllib.parse.urlsplit(target).scheme:continue
            links+=1
            if not (path.parent/urllib.parse.unquote(target)).exists():
                errors.append('Missing link in '+str(path.relative_to(root))+': '+target)
    vectors=list(design.glob('docs/**/*.svg'))
    for path in vectors:ET.parse(path)
    editable=list((design/'ui-svg').glob('*.svg'))
    for path in editable:
        copy=design/'docs/screens'/path.name
        if not copy.is_file() or path.read_bytes()!=copy.read_bytes():errors.append('Different UI copy: '+path.name)
    class Assets(HTMLParser):
        def handle_starttag(self,tag,attrs):
            if tag not in ('script','img','link','iframe','video','audio'):return
            for key,value in attrs:
                if key in ('src','href') and value and value.startswith(('https:','http:','//')):
                    errors.append('Remote presentation asset: '+tag+': '+value)
    Assets().feed((design/'docs/storyboards.html').read_text())
    return dict(checksums_checked=checksums,markdown_links_checked=links,rendered_svg_count=len(vectors),editable_ui_copies=len(editable),errors=errors)


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root',type=Path,default=Path(__file__).resolve().parents[1])
    result=check(parser.parse_args().root.resolve())
    print(json.dumps(result,indent=2));raise SystemExit(bool(result['errors']))


if __name__=='__main__':main()
