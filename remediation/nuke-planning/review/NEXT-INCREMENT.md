# Review continuation and prerequisite increment

Checkpoint freeze: current Review app/test files are frozen at root's request. No production changes for this increment have begun. Resume only after root releases the checkpoint freeze and supplies the chronological mock gate receipts.

## Persisted continuation

The six ReviewRecoveryCases (24 correct/individual negative-control checks) must pass first. Add the native bindings using the identical cases. Refactor LaunchAsync through a single private execution method with a resume flag; expose ResumeAsync on the same service. Retain one run lease across replay and continuation.

Resume checks the immutable run intent and reviewed head, live ready PR observation, prepared prompt/manifest hashes, capability receipt, and every request/completion/visible-response prefix in numbered call order. A request without a completion is uncertain, even if no process is currently visible; it cannot cause a new call. A completion without a request or a response hash mismatch also blocks. Returned terminal process metadata must be explicit in the completion receipt. Preserve every original byte and rederive the same stage/partition flow by replaying completed responses without invoking the process runner. The first absent request after a verified terminal prefix is a known unstarted continuation. Use a fresh call directory and the complete accumulated visible prior output. Recompute prompt/assigned range hashes while replaying; a mismatch blocks.

An existing final result is adopted without another provider invocation. A provider denial remains paused under replay. A malformed or generic error terminal response pauses as uncertain instead of causing an immediate retry loop. Actual output/context boundaries continue. Context replay reconstructs every completed part and produces one final unified synthesis. The same canonical/hash services and capture seams remain authoritative. Add a deliberate artifact-store crash before the next prompt/request write to prove terminal continuation; a process exception after its request write proves the uncertain-prefix no-duplicate case.

Malformed fragments cannot be certified as visible when their JSON boundaries are broken. Retain original raw captures privately and export only fragment byte count/hash/reason for an unparseable line; otherwise a cut hidden-reasoning block could leak through the fallback. Add correct and individual negative-control fixtures before this correction. Parsed visible events must retain all other fields.

## Mandatory trusted prerequisites

Parent decision: Prepare must require IReviewPrerequisiteSource. It supplies head-bound repository unit/integration inventories and a verified requirements receipt. Caller booleans and caller-selected project lists cannot establish completeness. The constructor has no permissive fallback. A source failure blocks before provider effects.

Production discovery uses only the shared IProcessRunner and IProcessCaptureReader with approved git identity: exact-head verification, read-only git ls-tree of the reviewed head and git show of each pinned project definition. Never infer test status or unit/integration kind from a filename. Unresolved metadata, imports, conditions or dynamic values require a trusted native evaluation receipt bound to the same head, project definition hash, native tool identity and complete project denominator. Missing or stale evaluation rows block. Explicit scope integration inventory must include every required integration lane.

The official MCP plugin capture supplies active functional, technical and test records, their acceptance criteria, mappings and per-criterion test coverage. Validate IDs, mapping targets and coverage, and bind the complete receipt bytes/hash to the head and workspace. A pending-HV requirement status is allowed because the user requires Ready before HV; it does not bypass missing records, criteria or mappings. A raw caller complete=true does not satisfy this contract. Keep this official-plugin runtime boundary blocked until the root-owned typed exception for the verified plugin process is itself tested; no raw REST or generic pwsh bypass.

The per-round self-review evidence is also mandatory: full holistic document review, what-if pass, propagation audit, phase boundaries, search-before-adding, no-new-mechanism check, mapping lint, exact user scope in the brief and standing rules in delegated briefs. Pin every checklist evidence artifact to the reviewed head. Missing entries block Launch. Add named mock and native missing-entry controls before wiring this preflight.

Before production, add named mock fixtures for complete inventory, omitted unit project, omitted scoped integration project, wrong head, missing dynamic evaluation, omitted project definition, stale definition hash, missing AC, missing FR/TR/TEST mapping or criterion coverage, forged caller completion and valid pending-HV requirements. For each fixture, prove correct response and individual wrong status, interaction and detail controls. Then bind the identical fixtures to the real source/services, followed by the full applicable NUKE suite. Preserve fresh TRX receipts with zero failed/skipped required cases.

## Runtime audit boundary

IReviewAudit remains mandatory. A test fake proves dependency interaction only. Production reviewer-own-MCP linkage, full visible result persistence and read-back proof require the actual authorized reviewer mechanism after the PR is ready. No adapter may substitute a null audit, claim live parity from unit results, send private profile/credential data, or invoke the reviewer before readiness. No requirement or plan is marked complete here.
