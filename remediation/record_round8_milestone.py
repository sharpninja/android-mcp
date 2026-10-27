import datetime,json,pathlib
R=pathlib.Path(__file__).resolve().parent
D=R/'reviews/round-008'
state=json.loads((R/'LIVE-STATUS.json').read_text())
state.update(updated_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),review_events=sum(1 for _ in (D/'response.visible.jsonl').open()),
 receipt_audit='evidence/review-receipt-audit.json:8rounds hash-valid at23:07:55',
 source_cache_recomputation='evidence/source007-cache-recomputation.json:16893files530212declarations0parse-errors,newcontract2fingerprint,identicalbytesafteractualrecomputation',
 source_candidate_difference='Onlybuild_catalog.pydiffers:sourcecandidate adds per-archive unmatched/mapping-status fields and marks missing-index partial even when fallback resolves all; owncandidate marks unresolvedmissing-index partial afterlinkage.',
 aidl_current_corpus_audit='evidence/aidl-multideclaration-audit-release006.json:19781files19863top-level declarations,41multi71dotted; no exception count; no production rebuild')
(R/'LIVE-STATUS.json').write_text(json.dumps(state,indent=2)+'\n')
print(json.dumps(dict(updated_at=state['updated_at'],review_events=state['review_events'],review='008active',new_audit=state['aidl_current_corpus_audit'])))
