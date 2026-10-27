import pathlib,json,math
R=pathlib.Path(__file__).resolve().parent;receipt=json.loads((R/'evidence/catalog-size-release006.json').read_text());size=receipt['size_bytes'];gib=size/(1024**3)
path=R/'design/Android MCP Bridge Design.md';text=path.read_text()
old="Release005's complete SQLite catalog is7,841,099,776 bytes (about7.30GiB); raw source archives and JSON audit exports stay off-device."
new=f"The current completed release006 SQLite catalog is{size:,} bytes (about{gib:.2f}GiB), pinned by evidence/catalog-size-release006.json with its SHA-256 and stable size/mtime capture. This is the entire selected corpus, not global coverage. Raw source archives and JSON audit exports stay off-device."
if old not in text:raise ValueError('Inspect capacity text before replacing')
text=text.replace(old,new)
old='Before accepting an atomic catalog replacement, require free space >= active_catalog_bytes + incoming_catalog_bytes + bounded_staging_scratch_bytes + configured_log_and_config_quota +1GiB safety reserve. Conservatively budget scratch at incoming_catalog_bytes; the current full-catalog profile therefore requires roughly24GiB available during replacement.'
new=f'Before accepting an atomic catalog replacement, require additional free space >= incoming_catalog_bytes + bounded_staging_scratch_bytes + remaining_configured_log_and_config_quota +1GiB safety reserve. Conservatively budget scratch at incoming_catalog_bytes. With an existing active catalog of the same current size, this requires at least{math.ceil(2*gib+1)}GiB additional free space plus the configured quota; the total peak catalog footprint, including that already allocated active copy, rounds up to{math.ceil(3*gib+1)}GiB plus the quota. Do not count the existing active file twice when comparing against reported free space.'
if old not in text:raise ValueError('Inspect preflight text before replacing')
text=text.replace(old,new);path.write_text(text)
print(json.dumps(dict(size_bytes=size,gib=gib,minimum_additional_free_gib_excluding_quota=math.ceil(2*gib+1),peak_total_gib_excluding_quota=math.ceil(3*gib+1),hash_receipt='evidence/catalog-size-release006.json')))
