"""Inventory privacy-sensitive receipt fields; values never printed or exported."""
from pathlib import Path
import json,hashlib,datetime,collections,re
R=Path(__file__).resolve().parent.parent;rows=[];signatures=[]
patterns={'signed_transfer_url':re.compile(r'https?://[^\s"<>]+[?&](?:X-Amz-Signature|X-Goog-Signature|sig|signature|token)=[^\s"<>]+',re.I),'bearer_secret':re.compile(r'Bearer\s+(?:ghp_|sk-ant-)[A-Za-z0-9_-]{20,}')}
for p in sorted((R/'reviews').rglob('*.jsonl')):
    if 'round-020' in p.parts:continue # active receipt: never pretend a partial stream is sealed
    if not any(x in p.name for x in ('request','response')):continue
    before=p.stat();counts=collections.Counter();events=0;invalid=0
    def walk(value):
        if isinstance(value,dict):
            if value.get('type') in ('thinking','redacted_thinking'):counts['hidden_reasoning_block']+=1
            if value.get('type')=='rate_limit_event':counts['account_quota_event']+=1
            if value.get('subtype')=='init':
                for key in ('plugins','slash_commands','terminal_slash_commands','skills','agents','messaging_socket_path'):
                    if key in value:counts['unrelated_runtime_configuration_field']+=1
            for v in value.values():walk(v)
        elif isinstance(value,list):
            for v in value:walk(v)
    with p.open() as stream:
        for line in stream:
            events+=1
            for label,pattern in patterns.items():
                if pattern.search(line):counts[label]+=1
            try:walk(json.loads(line))
            except ValueError:invalid+=1
    after=p.stat()
    if (before.st_size,before.st_mtime_ns)!=(after.st_size,after.st_mtime_ns):
        rows.append(dict(file=str(p.relative_to(R)),status='changed_during_scan_not_sealed'));continue
    h=hashlib.sha256()
    with p.open('rb') as stream:
        for block in iter(lambda:stream.read(65536),b''):h.update(block)
    rows.append(dict(file=str(p.relative_to(R)),bytes=after.st_size,sha256=h.hexdigest(),events=events,invalid_json_lines=invalid,privacy_categories=dict(counts),status='original_preserved; publication filtering required' if counts else 'no flagged structured fields; still subject to final scope review'))
totals=collections.Counter()
for r in rows:totals.update(r.get('privacy_categories',{}))
out=Path(__file__).resolve().parent/'review-receipt-audit-001.json'
with out.open('x') as f:json.dump(dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),scope='Completed own-task review JSONL only; active020/source-owner receipts excluded',receipts=rows,flagged_categories=dict(totals),secret_values_exported=False,originals_modified=False,publication_rule='Preserve all project request and visible model response content; exclude account quota and unrelated runtime/plugin configuration in explicitly audited publication copies. Strip any hidden reasoning blocks, never expose them. Keep exact original hashes and declared redaction paths; original local receipts remain immutable. Do not publish delivery-session URLs or auth caches.'),f,indent=2)
print(json.dumps(dict(audit=str(out),receipts=len(rows),flagged_categories=dict(totals),originals_modified=False,github_write=False)))
