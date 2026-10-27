# Kotlin Compose screen definitions and branching storyboards

Canonical behavior/state source: [UI blueprint](ui-blueprint.md). SVG exports retain vector text. These are design specifications; no Android UI exists yet. State-specific panels reflect exact enabled controls and health.

## SCR01 — Dashboard

FR-KT-01 / FR-KT-03

![Screen definition](screens/SCR01.svg)

- **SCR01.C01 Start / Stop service** (lifecycle): Service started only after eligible foreground start and listener ready; Stop closes admission and drains verified cleanup within10s Fields: Stopped/Running desired state. Failure: Start eligibility or bind failure shows Error; stop timeout shows unknown cleanup and closed admission Cancel: During Start, explicit Stop closes admission; after Stop begins, cleanup remains bounded and is not rolled back into Running.
- **SCR01.C02 Open subscriptions** (navigation): Open SCR05 owner-filtered subscription list Fields: Subscription states, cleanup phase, route and cursor retention. Failure: Store unavailable shows status-only error; no register/unregister side effect Cancel: Close drawer/Back to prior view. Dirty draft requires Discard or Stay; no save is implied.
- **SCR01.C03 Open navigation** (navigation): Open all local destinations through phone drawer or wide rail; Back returns previous screen Fields: SCR01..SCR08,SCR10,SCR11; protected review only via exact command. Failure: Dirty draft requires Discard/Stay; navigation never commits configuration Cancel: Close drawer/Back to prior view. Dirty draft requires Discard or Stay; no save is implied.
- **SCR01.C04 Open health details** (read): Expand independent health facets and their reasons on SCR01 Fields: MCP, ADB, resolved view, catalog, inbound ARC, snapshot revision. Failure: Missing facet is unknown, never inferred from another ready facet Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.
- **SCR01.C05 Refresh device view** (protected-action): Request supported device/backend fact refresh through SCR09 then stage/activate coherent view Fields: Profile/target/user and current view/capability epochs. Failure: Denied probes retain unknown; failed refresh leaves previous view stale, no guessed availability Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.

Phone: Single-column scrolling data/detail and48dp controls; preserve explicit draft fields on rotation. Modal drawer below840dp; persistent rail at840dp+. Back returns previous detail, with Discard/Stay for dirty drafts. Landscape: Same IDs and authorization; wide layout only when available width>=840dp; never shrink protected actions below48dp.

- [SCR01.S0](screens/states/SCR01.S0.svg) **Ready: service Running; facets independent**. Effect: Listener ready; show actual ADB/view/catalog/ARC facets. Start action now means Stop. Recovery: Stop deliberately or inspect a specific degraded facet; never infer overall readiness. Disabled: none.
- [SCR01.S1](screens/states/SCR01.S1.svg) **Starting: admission closed**. Effect: Desired Running persisted; listener/device resolution not yet ready. Recovery: Wait for bounded start or use Stop; failure transitions Error. Disabled: SCR01.C01.
- [SCR01.S2](screens/states/SCR01.S2.svg) **Start denied: owner action required**. Effect: No new listener/admission; reason includes precise Android eligibility or bind failure. Recovery: Open Action required; fix prerequisite then explicitly Start again. Disabled: none.
- [SCR01.S3](screens/states/SCR01.S3.svg) **Stopped: no listener and no Running badge**. Effect: Desired Stopped; no MCP/ARC socket owned by service. Recovery: Local Start requires eligible foreground intent; boot never overrides manual Stop. Disabled: SCR01.C02, SCR01.C05.
- [SCR01.S4](screens/states/SCR01.S4.svg) **Approval pending: open exact local challenge**. Effect: Request retains original deadline; notification opens review, never approves. Recovery: Open SCR09 or let deadline deny; other queued requests retain FIFO/original deadlines. Disabled: none.
- [SCR01.S5](screens/states/SCR01.S5.svg) **Stopping: admission closed and cleanup bounded**. Effect: Close listeners/delivery; attempt verified cleanup before backend/store close. Recovery: After10s report stopped with unknown obligations where necessary; never fake cleanup success. Disabled: SCR01.C01, SCR01.C05.
- [SCR01.S6](screens/states/SCR01.S6.svg) **Runtime Error: admission closed with exact reason**. Effect: Mandatory audit/store/backend consistency failure closes new device dispatch; listener health remains independently observed. Recovery: Inspect SCR10 precise failure, fix the actual prerequisite, then explicit safe recovery; never replay an uncertain device effect. Disabled: none.
- [SCR01.S7](screens/states/SCR01.S7.svg) **Waiting for unlock: minimal nonsecret status only**. Effect: Boot preference mirror requested startup, but credential-encrypted catalog/config/keys are unavailable. No listener/device admission. Recovery: Unlock normally, reconcile boot preference and request eligible startup; never move secrets to device-protected storage. Disabled: SCR01.C01, SCR01.C02, SCR01.C05.

## SCR02 — Clients and permissions

FR-KT-02 / canonical client grants

![Screen definition](screens/SCR02.svg)

- **SCR02.C01 Edit exact grants** (protected-action): Preview exact grant diff then SCR09; CAS commit selected client rule Fields: Canonical binding/schema hash, target/user/backend, Deny/Ask/Allow and expiry. Failure: 403/cancel leaves old rule;409/412 opens reload/diff before new approval Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.
- **SCR02.C02 Revoke credential** (protected-action): SCR09 approves exact credential revoke; commit epoch and close owned sessions/streams Fields: Selected credential ID/kind and revision; irreversible old verifier invalidation. Failure: Conflict requires fresh review; no automatic replacement token Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.
- **SCR02.C03 Open navigation** (navigation): Open all local destinations through phone drawer or wide rail; Back returns previous screen Fields: SCR01..SCR08,SCR10,SCR11; protected review only via exact command. Failure: Dirty draft requires Discard/Stay; navigation never commits configuration Cancel: Close drawer/Back to prior view. Dirty draft requires Discard or Stay; no save is implied.
- **SCR02.C04 Add client** (navigation): Open SCR11 client enrollment; new clients start Deny Fields: Selected credential kind and enrollment profile. Failure: No proof/owner commit means no principal or grant Cancel: Close drawer/Back to prior view. Dirty draft requires Discard or Stay; no save is implied.
- **SCR02.C05 Select client** (selector): Select immutable client ID and load pinned permissions/session summary Fields: Kind,12hex display fingerprint, last used and current revision. Failure: Deleted/revoked selection shows terminal status; clear draft Cancel: Restore previous selection; selection does not connect, grant, activate or publish.
- **SCR02.C06 Choose exact rule** (editor): Prepare rule draft only; preview transitive contract hash and argument/target bounds Fields: Exact rule key, policy, schema hash, constraints and expiry. Failure: Unknown schema cannot become Allow; choose narrower known rule or cancel Cancel: Discard only unsaved fields; committed configuration/revisions remain unchanged.
- **SCR02.C07 Discard grant draft** (cancel): Discard grant draft and restore committed selected revision Fields: Unsaved draft indicator. Failure: No revoke or grant mutation; stay on selected client Cancel: This action is the explicit cancellation/discard. Report its recorded disposition; do not imply reversal of an already committed external effect.
- **SCR02.C08 Inspect active sessions** (read): List bounded active MCP/ARC sessions owned by selected client Fields: Session ID/kind, connected state, epoch and expiry; no bearer. Failure: Stale cursor reloads session list without recreating a session Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.
- **SCR02.C09 End selected session** (protected-action): End selected session through fresh SCR09 review; close delivery/socket Fields: Exact client/session ID and observed session epoch. Failure: Already closed is idempotent terminal; uncertain transport close never replays device calls Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.
- **SCR02.C10 Reload changed grants** (read): Reload current client/rule revision and show local-vs-server diff Fields: New ETag, changed schema/grant fields. Failure: Draft is preserved separately; never silently overwrites current rule Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.

Phone: Single-column scrolling data/detail and48dp controls; preserve explicit draft fields on rotation. Modal drawer below840dp; persistent rail at840dp+. Back returns previous detail, with Discard/Stay for dirty drafts. Landscape: Same IDs and authorization; wide layout only when available width>=840dp; never shrink protected actions below48dp.

- [SCR02.S0](screens/states/SCR02.S0.svg) **Selected client and exact grant revision**. Effect: Display committed rules and separate unsaved draft; session list is bounded. Recovery: Preview a change or revoke through exact owner review; reads remain available. Disabled: none.
- [SCR02.S1](screens/states/SCR02.S1.svg) **No clients: local enrollment available**. Effect: Client selectors/rule/session actions have no target. Recovery: Add client opens SCR11; inventory and other local navigation stay available. Disabled: SCR02.C01, SCR02.C02, SCR02.C05, SCR02.C06, SCR02.C07, SCR02.C08, SCR02.C09, SCR02.C10.
- [SCR02.S2](screens/states/SCR02.S2.svg) **Denied scope: rule unchanged**. Effect: Show which canonical binding/schema/target constraint denied; no rule broadening. Recovery: Edit narrower known draft then fresh owner review, or cancel. Disabled: none.
- [SCR02.S3](screens/states/SCR02.S3.svg) **CAS conflict: changed grant revision**. Effect: No mutation committed; old local draft and new server rule shown separately. Recovery: Reload changed grants, compare fields, then submit a new exact approval. Disabled: SCR02.C01, SCR02.C02, SCR02.C09.
- [SCR02.S4](screens/states/SCR02.S4.svg) **Revoked credential: sessions closed**. Effect: Verifier/epoch invalid; associated streams/input leases closed; historical rules retained. Recovery: Enroll/rotate a new credential explicitly; old token cannot be restored. Disabled: SCR02.C01, SCR02.C02, SCR02.C06, SCR02.C09.

## SCR03 — Full binding catalog

All bindings / FR-DEV-01

![Screen definition](screens/SCR03.svg)

- **SCR03.C01 Inspect evidence** (read): Open immutable source evidence and effective binding/type revision Fields: Source pin/locator, declared permissions, applicability and unknown reasons. Failure: Missing evidence remains explicitly unknown; read never grants permission Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.
- **SCR03.C02 Edit local override** (editor): Create local override draft without rewriting imported evidence Fields: Field-level diff, types/dependencies, provenance and expected cat1 ETag. Failure: Invalid schema/dependency blocks preview; discard returns immutable effective record Cancel: Discard only unsaved fields; committed configuration/revisions remain unchanged.
- **SCR03.C03 Open navigation** (navigation): Open all local destinations through phone drawer or wide rail; Back returns previous screen Fields: SCR01..SCR08,SCR10,SCR11; protected review only via exact command. Failure: Dirty draft requires Discard/Stay; navigation never commits configuration Cancel: Close drawer/Back to prior view. Dirty draft requires Discard or Stay; no save is implied.
- **SCR03.C04 Search inventory** (query): Search full inventory including unknown/unavailable/unassigned records Fields: Text/type/namespace/version/evidence filters and bounded limit. Failure: Empty results show query controls; malformed filter reports400 without hiding inventory Cancel: Cancel this bounded search; retain query draft and committed view, no remote mutation.
- **SCR03.C05 Next catalog page** (pagination): Fetch next bounded page in pinned inventory view Fields: Opaque cursor, limit and byte budget. Failure: 410 clears cursor and offers restart at current generation Cancel: Cancel this page read; keep previous valid cursor/status and require explicit next read.
- **SCR03.C06 Choose import document** (document-picker): Choose SAF file then private stage with expected hash/size and capacity preflight Fields: Source manifest/hash, exact bytes, free-space estimate and deadline. Failure: Picker cancel releases transient grant; hash/schema/space failure leaves active catalog unchanged Cancel: Picker cancel releases transient URI grant and pending unactivated snapshot/stage. If a write already began, show partial/unknown destination state.
- **SCR03.C07 Preview override diff** (editor): Show exact override/import/admin command preview; no commit yet Fields: Revision/ETag, dependencies, admin principal/window/method/path/digest, hash/size and TTL. Failure: Invalid preview shows precise field errors; owner prearming/commit requires SCR09 Cancel: Discard only unsaved fields; committed configuration/revisions remain unchanged.
- **SCR03.C08 Discard override or import** (cancel): Discard unactivated stage or override draft and release its private files Fields: Draft/stage handle and active catalog identity. Failure: Active imported history is retained; cleanup unknown is surfaced, never publication Cancel: This action is the explicit cancellation/discard. Report its recorded disposition; do not imply reversal of an already committed external effect.
- **SCR03.C09 Activate validated import** (protected-action): SCR09 approves exact validated registry publication; atomic CAS activates it Fields: Sealed stage hash, registry ID, expected generation, conflict/rebase preview. Failure: Failed validation/CAS/approval leaves old active registry; new review required Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.
- **SCR03.C10 Manage admin window** (protected-action): Enable scoped admin window or disable it via exact local SCR09 command Fields: Disabled/ordinary10min/import-only TTL, admin principal and allowed operations. Failure: Expiry/revoke closes admin work; no MCP credential becomes admin; no hidden listener Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.
- **SCR03.C11 Reload catalog revision** (read): Reload changed cat1 revision and recompute preview without applying draft Fields: Current ETag and conflict differences. Failure: Unknown new schema prevents save until owner resolves fields Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.

Phone: Paged inventory then full-screen evidence/diff/import preview; expected hash/size/deadline remain visible. Modal drawer below840dp; persistent rail at840dp+. Back returns previous detail, with Discard/Stay for dirty drafts. Landscape: Same IDs and authorization; wide layout only when available width>=840dp; never shrink protected actions below48dp.

- [SCR03.S0](screens/states/SCR03.S0.svg) **Inventory: selected binding/effective revision**. Effect: Full imported/override inventory visible including unknown applicability; no grant change. Recovery: Search/filter, read evidence or stage a local draft. Disabled: none.
- [SCR03.S1](screens/states/SCR03.S1.svg) **No matches: search remains available**. Effect: No binding selected; empty query result is not an empty source corpus. Recovery: Broaden filters or choose a validated import; no empty-record edit. Disabled: SCR03.C01, SCR03.C02, SCR03.C05, SCR03.C07, SCR03.C09, SCR03.C11.
- [SCR03.S2](screens/states/SCR03.S2.svg) **Schema unknown: evidence retained**. Effect: Record visible with reason; unproven codec/type graph cannot activate invocation. Recovery: Inspect evidence or stage explicit supported metadata; do not invent wire layout. Disabled: SCR03.C02, SCR03.C09.
- [SCR03.S3](screens/states/SCR03.S3.svg) **Device view stale: calls blocked**. Effect: Inventory reads continue; current availability display is stale, not absent. Recovery: Refresh device view through SCR01; old metadata is never silently relabeled. Disabled: SCR03.C09.
- [SCR03.S4](screens/states/SCR03.S4.svg) **Override/import/admin exact preview**. Effect: Show exact fields/dependencies/ETag, stage hash/size, estimate/deadline and admin window. Recovery: Discard or request SCR09 approval for this exact command; preview itself never commits. Disabled: none.
- [SCR03.S5](screens/states/SCR03.S5.svg) **Import invalid: active catalog unchanged**. Effect: Hash/schema/FK/capacity validation failed in quarantine; no trusted imported reads. Recovery: Inspect specific error, discard stage, select a different expected artifact. Disabled: SCR03.C02, SCR03.C07, SCR03.C09.
- [SCR03.S6](screens/states/SCR03.S6.svg) **No catalog: only safe staging available**. Effect: Admission closed; search/evidence/override actions have no active inventory. Recovery: Choose import document and independently verify hash/size before stage. Disabled: SCR03.C01, SCR03.C02, SCR03.C04, SCR03.C05, SCR03.C07, SCR03.C09, SCR03.C11.
- [SCR03.S7](screens/states/SCR03.S7.svg) **Validated sealed import: ready for review**. Effect: Stage bytes sealed and validation report pinned; old active catalog still serves. Recovery: Activate exact import through SCR09 or discard before expiry. Disabled: SCR03.C02.
- [SCR03.S8](screens/states/SCR03.S8.svg) **Admin window disabled or expired**. Effect: No administration request is admitted; MCP/device invocation permissions unchanged. Recovery: Local Manage admin window opens exact scope/lifetime review. Disabled: none.
- [SCR03.S9](screens/states/SCR03.S9.svg) **Scoped admin window open: remaining lifetime visible**. Effect: Only the approved admin principal/artifact/operation scope is reachable until the monotonic deadline; no MCP grant is created. Recovery: Disable via exact local action or let expiry revoke active admin work; each mutation still needs its own exact approval. Disabled: none.

## SCR04 — Groups for a job

FR-GRP-01 / 02 / 03

![Screen definition](screens/SCR04.svg)

- **SCR04.C01 Inspect group summary** (read): Open selected functional group summary without schemas for unrelated groups Fields: Stable group ID/revision/hash, dependencies and permission/applicability facets. Failure: Missing/deleted group stays searchable as historical/unassigned; cannot activate unknown membership Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.
- **SCR04.C02 Save group mapping** (protected-action): SCR09 approves exact local group-mapping create/update with CAS Fields: Small atomic member IDs, paired register/unregister, required types and provenance. Failure: Budget/dependency/conflict error saves no partial group or activation Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.
- **SCR04.C03 Open navigation** (navigation): Open all local destinations through phone drawer or wide rail; Back returns previous screen Fields: SCR01..SCR08,SCR10,SCR11; protected review only via exact command. Failure: Dirty draft requires Discard/Stay; navigation never commits configuration Cancel: Close drawer/Back to prior view. Dirty draft requires Discard or Stay; no save is implied.
- **SCR04.C04 Select client/session** (selector): Choose client/session only for owner preview of interest/permission status Fields: Exact client ID/session epoch; local UI does not impersonate MCP client. Failure: Expired session shows no activation option; client must initialize again Cancel: Restore previous selection; selection does not connect, grant, activate or publish.
- **SCR04.C05 Describe job** (text-field): Edit task description and optional deterministic search constraints Fields: Job text, applicable target, desired max_bytes and pagination limit. Failure: Text is untrusted data, never grants or executable instructions Cancel: Discard edited query text; no search/activation occurs until its explicit action.
- **SCR04.C06 Search group summaries** (query): Rank bounded group summaries from deterministic index; optional semantic ranking is advisory Fields: Job/query and relevance explanation; unassigned inventory search link. Failure: Budget exceeded returns smaller page/cursor, not all schemas Cancel: Cancel this bounded search; retain query draft and committed view, no remote mutation.
- **SCR04.C07 Describe selected group** (pagination): Read one selected group manifest page pinned to revision/hash Fields: Member/type/cleanup IDs and exact next cursor. Failure: Stale group revision invalidates page; reload summary before client activation Cancel: Cancel this page read; keep previous valid cursor/status and require explicit next read.
- **SCR04.C08 Discard mapping draft** (cancel): Discard mapping draft; keep committed mappings and client interests Fields: Unsaved create/edit indicator. Failure: No changes to client grants or loaded group view Cancel: This action is the explicit cancellation/discard. Report its recorded disposition; do not imply reversal of an already committed external effect.
- **SCR04.C09 Edit functional group members** (editor): Create or edit functional member list and required dependency closure as a draft Fields: Stable group ID, member IDs, register/unregister pair, metadata evidence, size preview. Failure: Unassigned entries remain searchable; cycles/oversize show errors before save Cancel: Discard only unsaved fields; committed configuration/revisions remain unchanged.
- **SCR04.C10 Reload group mapping** (read): Reload current mapping revision after CAS conflict Fields: Server revision, local diff and dependency changes. Failure: Preserve local draft separately; fresh approval needed for changed command Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.

Phone: Summary search and selected group details; member editor opens separate full-screen draft. Modal drawer below840dp; persistent rail at840dp+. Back returns previous detail, with Discard/Stay for dirty drafts. Landscape: Same IDs and authorization; wide layout only when available width>=840dp; never shrink protected actions below48dp.

- [SCR04.S0](screens/states/SCR04.S0.svg) **Group summary and local mapping draft**. Effect: Search returns bounded summaries; owner preview is not client activation. Recovery: Describe one group or edit atomic member/dependency draft. Disabled: none.
- [SCR04.S1](screens/states/SCR04.S1.svg) **No group matches: unassigned inventory searchable**. Effect: No selected group; create draft and search remain possible. Recovery: Broaden job/query or select unassigned bindings for a small new group. Disabled: SCR04.C01, SCR04.C02, SCR04.C07, SCR04.C08, SCR04.C10.
- [SCR04.S2](screens/states/SCR04.S2.svg) **Atomic group budget/dependency exceeded**. Effect: No partial group saved or loaded; explain exact member/dependency excess. Recovery: Remove optional scope or split into coherent atomic groups, then re-preview. Disabled: SCR04.C02.
- [SCR04.S3](screens/states/SCR04.S3.svg) **Mapping CAS conflict**. Effect: No changes committed; prior session interests keep pinned revision only while authority valid. Recovery: Reload mapping, merge explicit fields, then fresh owner save. Disabled: SCR04.C02.
- [SCR04.S4](screens/states/SCR04.S4.svg) **Member editor: draft only**. Effect: Register/unregister pairs and necessary types move as one coherent set. Recovery: Save through SCR09 or discard; no grant or dynamic tool activation occurs. Disabled: none.

## SCR05 — Callback subscriptions

FR-CB-01 / 02 / 03 / 04

![Screen definition](screens/SCR05.svg)

- **SCR05.C01 Inspect bounded status** (read): Inspect selected subscription status without opening a backend registration Fields: Principal,phase,lease,delivery route,sequence,cursor bounds and cleanup disposition. Failure: Unknown registration vs cleanup phase remains distinct; no replay Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.
- **SCR05.C02 Cancel via owner review** (protected-action): SCR09 approves administrative cancellation of another client subscription Fields: Exact handle/revision and verified cleanup plan; delivery closes before cleanup. Failure: Cleanup failure records unknown; cannot promise backend unregister from timeout Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.
- **SCR05.C03 Open navigation** (navigation): Open all local destinations through phone drawer or wide rail; Back returns previous screen Fields: SCR01..SCR08,SCR10,SCR11; protected review only via exact command. Failure: Dirty draft requires Discard/Stay; navigation never commits configuration Cancel: Close drawer/Back to prior view. Dirty draft requires Discard or Stay; no save is implied.
- **SCR05.C04 Select subscription** (selector): Select one owned/admin-visible handle and pin its current status revision Fields: subscription_id,stream_epoch,client,backend and route. Failure: Foreign/expired handles deny; no payload leaks into status Cancel: Restore previous selection; selection does not connect, grant, activate or publish.
- **SCR05.C05 Read event page** (pagination): Read finite authorized journal page or sealed frame/tree blob chunk Fields: Application cursor/offset, limits, explicit gap/terminal marker. Failure: 410 shows retention boundary and offers current retained cursor; no reregistration Cancel: Cancel this page read; keep previous valid cursor/status and require explicit next read.
- **SCR05.C06 Refresh status** (read): Refresh status and delivery lease only; never renew or register implicitly Fields: Current subscription/stream lease expiry and cleanup state. Failure: Death/revoke hides payload; uncertainty stays until supported reconciliation Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.
- **SCR05.C07 Review cleanup reconciliation** (protected-action): Review safe reconciliation of an uncertain cleanup; execute only proven idempotent plan Fields: Recorded plan, endpoint generation, uncertain phase and new authority. Failure: No proof means inspect/forget delivery handle only; never repeat uncertain non-idempotent unregister Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.

Phone: Single-column scrolling data/detail and48dp controls; preserve explicit draft fields on rotation. Modal drawer below840dp; persistent rail at840dp+. Back returns previous detail, with Discard/Stay for dirty drafts. Landscape: Same IDs and authorization; wide layout only when available width>=840dp; never shrink protected actions below48dp.

- [SCR05.S0](screens/states/SCR05.S0.svg) **Selected live callback subscription**. Effect: Show registration state, route=journal/direct, stream lease and cleanup phase. Recovery: Read bounded retained events or request administrative cancellation. Disabled: none.
- [SCR05.S1](screens/states/SCR05.S1.svg) **No registrations**. Effect: No selected handle or journal exists; page is empty, not failed. Recovery: Return after an authorized client registers; UI never auto-registers. Disabled: SCR05.C01, SCR05.C02, SCR05.C04, SCR05.C05, SCR05.C06, SCR05.C07.
- [SCR05.S2](screens/states/SCR05.S2.svg) **Cursor expired: retention boundary explicit**. Effect: Lost prefix not replayable; status and retained terminal/gap metadata still readable. Recovery: Refresh status and explicitly restart from earliest retained cursor; no reregistration. Disabled: SCR05.C05.
- [SCR05.S3](screens/states/SCR05.S3.svg) **Backend death/revoke/orphan: payload closed**. Effect: Registration or cleanup uncertainty is shown separately; handle durability is not OS registration. Recovery: Inspect/reconcile only recorded verified cleanup; do not repeat uncertain effects. Disabled: SCR05.C02, SCR05.C05.
- [SCR05.S4](screens/states/SCR05.S4.svg) **Delivery closed: cleanup disposition shown**. Effect: No new payload delivered; confirmed/unknown unregister remains explicit. Recovery: Read retained terminal state; safe reconciliation only with current authority. Disabled: SCR05.C02.
- [SCR05.S5](screens/states/SCR05.S5.svg) **Cleanup reconciliation has no safe retry proof**. Effect: No backend call is issued; unknown non-idempotent cleanup remains unknown. Recovery: Retain evidence or forget only local delivery state after owner review; never claim unregister. Disabled: SCR05.C02, SCR05.C05, SCR05.C07.

## SCR06 — Call logs

Per-client logging / FR-KT-02

![Screen definition](screens/SCR06.svg)

- **SCR06.C01 Inspect record** (read): Open redacted bounded call record and observed/unknown outcome Fields: Principal/rule/schema pins, timing and sanitized error; no secrets. Failure: Missing optional payload says not logged, never reconstructs it Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.
- **SCR06.C02 Export selected scope** (protected-action): Prepare exact redacted export snapshot then SCR09 approve snapshot ID/hash/scope Fields: Selected clients/time/outcome, bytes and redaction policy. Failure: Auth/quota failure creates no document; a changed selection needs new approval Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.
- **SCR06.C03 Open navigation** (navigation): Open all local destinations through phone drawer or wide rail; Back returns previous screen Fields: SCR01..SCR08,SCR10,SCR11; protected review only via exact command. Failure: Dirty draft requires Discard/Stay; navigation never commits configuration Cancel: Close drawer/Back to prior view. Dirty draft requires Discard or Stay; no save is implied.
- **SCR06.C04 Filter logs** (query): Filter bounded redacted records within owner scope Fields: Client/time/outcome, page limit and pinned cursor. Failure: Expired cursor restarts read, not replay of historical calls Cancel: Cancel this bounded search; retain query draft and committed view, no remote mutation.
- **SCR06.C05 Choose export document** (document-picker): Write the approved sealed export through Android create-document Fields: Approved snapshot ID/hash and user-selected destination. Failure: Picker cancel/partial I/O releases snapshot and reports written/unknown bytes; no automatic retry Cancel: Picker cancel releases transient URI grant and pending unactivated snapshot/stage. If a write already began, show partial/unknown destination state.
- **SCR06.C06 Edit retention quotas** (editor): Open the same per-client logging editor; draft then exact SCR09 CAS save for this client only Fields: Exact client_id and client_logging_config.logging_revision; mode/paused/retention/byte quota; global audit reserve is a read-only ceiling in this editor. Failure: Invalid/low capacity rejects; mandatory audit failure closes new dispatch Cancel: Discard only unsaved fields; committed configuration/revisions remain unchanged.
- **SCR06.C07 Cancel export** (cancel): Cancel export and release unconsumed sealed snapshot Fields: Current export state and destination grant. Failure: Already-written document is not silently deleted; partial write is explicit Cancel: This action is the explicit cancellation/discard. Report its recorded disposition; do not imply reversal of an already committed external effect.

Phone: Single-column scrolling data/detail and48dp controls; preserve explicit draft fields on rotation. Modal drawer below840dp; persistent rail at840dp+. Back returns previous detail, with Discard/Stay for dirty drafts. Landscape: Same IDs and authorization; wide layout only when available width>=840dp; never shrink protected actions below48dp.

- [SCR06.S0](screens/states/SCR06.S0.svg) **Redacted log page**. Effect: Selected record is historical observed/unknown outcome, never a runnable action. Recovery: Filter/read or prepare an exact scoped export. Disabled: none.
- [SCR06.S1](screens/states/SCR06.S1.svg) **No matching logged calls**. Effect: No record selected; disabled payload logging is shown explicitly. Recovery: Change filters or return after new admitted calls; do not fabricate records. Disabled: SCR06.C01, SCR06.C02, SCR06.C05, SCR06.C07.
- [SCR06.S2](screens/states/SCR06.S2.svg) **Export approval missing/denied**. Effect: No document or sealed authorized export is released. Recovery: Request a new exact scoped SCR09 approval or cancel. Disabled: SCR06.C05.
- [SCR06.S3](screens/states/SCR06.S3.svg) **Audit reserve/quota failure**. Effect: Mandatory durable audit capacity exhausted; new dispatch fail-closed, optional logs evict first. Recovery: Review retention under owner auth; inspect write error before retrying safe startup. Disabled: none.
- [SCR06.S4](screens/states/SCR06.S4.svg) **Approved export: create-document picker**. Effect: Pinned redacted snapshot approved; only this selection may be written. Recovery: Choose destination or Cancel; selection change discards approval and needs new review. Disabled: SCR06.C02, SCR06.C04, SCR06.C06.
- [SCR06.S5](screens/states/SCR06.S5.svg) **Export partial/unknown write**. Effect: Destination may contain a partial document; no automatic rewrite or deletion. Recovery: Show bytes/hash when known; owner inspects/discards destination before any new export. Disabled: SCR06.C02, SCR06.C05.

## SCR07 — ARC session and control

FR-ARC-01 / 02 / 03 / 04

![Screen definition](screens/SCR07.svg)

- **SCR07.C01 Connect selected target** (session-command): Open selected outbound ARC socket and validate ID/version/status/capabilities before enabling control Fields: Exact target profile/user, loopback endpoint and protected bearer reference. Failure: Wrong token/protocol/EOF shows disconnected state; never sends uncertain input again Cancel: Cancel closes the owned pending transport/session action; report observed/unknown effects. Never resend uncertain input.
- **SCR07.C02 Disconnect / cancel** (session-command): Close owned watch socket and release local controller resources Fields: Selected session/connection epoch; Cancel8 never sent. Failure: No final frame is assumed; old frame is visibly stale and input disabled Cancel: Cancel closes the owned pending transport/session action; report observed/unknown effects. Never resend uncertain input.
- **SCR07.C03 Open navigation** (navigation): Open all local destinations through phone drawer or wide rail; Back returns previous screen Fields: SCR01..SCR08,SCR10,SCR11; protected review only via exact command. Failure: Dirty draft requires Discard/Stay; navigation never commits configuration Cancel: Close drawer/Back to prior view. Dirty draft requires Discard or Stay; no save is implied.
- **SCR07.C04 Send typed input; disabled unless connected capability/policy permits** (session-command): Send one validated typed key/text command after current shared policy/gate check Fields: Avalonia key name/text, exact target and current connection/root observation. Failure: Unknown key/protected/root/policy failure shows CommandResult; no implicit grant or retry Cancel: Cancel closes the owned pending transport/session action; report observed/unknown effects. Never resend uncertain input.
- **SCR07.C05 Select target profile** (selector): Choose saved outbound profile without connecting Fields: Target/package/user, endpoint and nonsecret credential alias. Failure: Missing secret/profile requires explicit owner profile edit; no default remote target Cancel: Restore previous selection; selection does not connect, grant, activate or publish.
- **SCR07.C06 Inspect tree or refresh frame** (read): Open bounded tree/frame observation after exact session authorization Fields: Frame sequence, pixel/logical size, scale, timestamp and node generation evidence. Failure: Disconnected/oversize/protected data denies; stale frame never becomes proof of intended coordinate Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.
- **SCR07.C07 Pointer or wheel input** (viewport): Dispatch ordered mouse/wheel batch for current target under service input lease Fields: One pointer,dp coordinates/buttons/wheel axes; no multitouch or remote root-generation claim. Failure: Lease conflict/protected root rejects; pressed state released on revoke/owner transition Cancel: Stop submitting new input; release owned pressed state on explicit lease/session close. A delivered click/key cannot be undone.
- **SCR07.C08 Inspect target details** (read): Show profile, wire and current connection facts without bearer Fields: arc-protobuf-v1, envelope1.0, package0.8.0, logical scale and local epoch. Failure: Unknown remote root generation remains unknown Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.
- **SCR07.C09 Edit outbound target profile** (protected-action): Create/edit outbound target profile and owner-provision bearer via SCR09 Fields: Loopback endpoint, target/user, wire profile and encrypted secret reference. Failure: Secret fields are protected/not ARC roots; never put bearer in logs/exports/discovery Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.

Phone: Viewport followed by tree/input tabs; zoom frame without changing logical coordinates; controls disabled until Connected. Modal drawer below840dp; persistent rail at840dp+. Back returns previous detail, with Discard/Stay for dirty drafts. Landscape: Same IDs and authorization; wide layout only when available width>=840dp; never shrink protected actions below48dp.

- [SCR07.S0](screens/states/SCR07.S0.svg) **Disconnected: selected profile only**. Effect: No target frame/tree/input; previous image, if retained, is labeled stale. Recovery: Edit/select profile then Connect; no input or Disconnect action with no connection. Disabled: SCR07.C02, SCR07.C04, SCR07.C06, SCR07.C07.
- [SCR07.S1](screens/states/SCR07.S1.svg) **Wrong target token: disconnected**. Effect: UNAUTHENTICATED response; no target content exposed. Recovery: Owner edits protected credential reference before a new explicit connection. Disabled: SCR07.C02, SCR07.C04, SCR07.C06, SCR07.C07.
- [SCR07.S2](screens/states/SCR07.S2.svg) **Unsupported wire/protocol: disconnected**. Effect: Version/ID/profile validation failed; no target method dispatch. Recovery: Inspect exact supported profile; never guess or downgrade protocol. Disabled: SCR07.C02, SCR07.C04, SCR07.C06, SCR07.C07.
- [SCR07.S3](screens/states/SCR07.S3.svg) **Protected root: frame/input suppressed**. Effect: Connected transport remains known but sensitive target observation/control is denied. Recovery: Wait for approved ordinary root or disconnect; never bypass protected capture gate. Disabled: SCR07.C01, SCR07.C04, SCR07.C06, SCR07.C07.
- [SCR07.S4](screens/states/SCR07.S4.svg) **Connected: bounded target observation**. Effect: Validated capabilities/target and policy gate control each command; no stale-frame proof. Recovery: Refresh tree/frame or send one allowed input; close socket to cancel watch. Disabled: SCR07.C01, SCR07.C05, SCR07.C09.
- [SCR07.S5](screens/states/SCR07.S5.svg) **Connecting: input disabled**. Effect: One authenticated connection attempt in progress; no ready viewport. Recovery: Cancel closes socket; timeout/error returns disconnected without input replay. Disabled: SCR07.C01, SCR07.C04, SCR07.C05, SCR07.C06, SCR07.C07, SCR07.C09.
- [SCR07.S6](screens/states/SCR07.S6.svg) **Input lease conflict: command rejected**. Effect: No new pointer/key mutation accepted for conflicting principal. Recovery: Inspect lease owner/reason, wait for release or disconnect; do not steal lease. Disabled: SCR07.C01, SCR07.C04, SCR07.C07.

## SCR08 — Settings and backends

FR-KT-03 / persistent service / ADB

![Screen definition](screens/SCR08.svg)

- **SCR08.C01 Save with revision** (protected-action): SCR09 approves exact settings draft; CAS save desired config and applicable lifecycle changes Fields: Boot/listeners/roots/logging fields and expected config revision. Failure: Conflict rejects entire draft; bind failure keeps desired vs actual separate Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.
- **SCR08.C02 Review pairing** (navigation): Open explicit ADB/MCP pairing review with exact selected profile Fields: ADB owner-enabled debugging prerequisites or separate MCP SCR11 enrollment. Failure: Self-ADB pairing grants no MCP permission; never changes device settings automatically Cancel: Close drawer/Back to prior view. Dirty draft requires Discard or Stay; no save is implied.
- **SCR08.C03 Open navigation** (navigation): Open all local destinations through phone drawer or wide rail; Back returns previous screen Fields: SCR01..SCR08,SCR10,SCR11; protected review only via exact command. Failure: Dirty draft requires Discard/Stay; navigation never commits configuration Cancel: Close drawer/Back to prior view. Dirty draft requires Discard or Stay; no save is implied.
- **SCR08.C04 Discard settings draft** (cancel): Discard settings draft and restore committed configuration Fields: Draft dirty fields and last config revision. Failure: No boot/listener/policy changes; active sockets remain until explicit lifecycle command Cancel: This action is the explicit cancellation/discard. Report its recorded disposition; do not imply reversal of an already committed external effect.
- **SCR08.C05 Edit startup and MCP listener** (editor): Edit boot opt-in and authenticated loopback MCP listener draft Fields: Address/port,TLS identity,Origin allowlist,boot preference; bridge_config row_version for global audit reserve/journal capacity; never aliases per-client logging_revision. Failure: Unsupported FGS/TLS profile shows failure; Save needs exact SCR09 approval Cancel: Discard only unsaved fields; committed configuration/revisions remain unchanged.
- **SCR08.C06 Edit same-device ADB profile** (editor): Edit owner-paired same-device ADB access profile and initiate explicit pair/connect Fields: Pair endpoint/code from OS UI, separate discovered connect endpoint,device/user identity. Failure: Background/split-screen OS pairing constraints visible; wrong key/port never reports Connected Cancel: Discard only unsaved fields; committed configuration/revisions remain unchanged.
- **SCR08.C07 Edit inbound ARC listener** (editor): Edit inbound ARC listener and exact root/entry-point grants; management roots expose read-only redacted observation, never input/actions Fields: Loopback port and root handle/security class; redacted management-readonly roots or explicitly registered ordinary_unprivileged roots; SCR09/SCR11/secret overlays excluded. Failure: Protected roots/secret surfaces not selectable; remote self-approval impossible Cancel: Discard only unsaved fields; committed configuration/revisions remain unchanged.
- **SCR08.C08 Rotate inbound ARC credential** (protected-action): SCR09 approves exact inbound ARC credential rotation Fields: Principal/kind/revision and affected stream/input lease list. Failure: Commit revokes old verifier/epoch; bind/session failures do not restore old bearer Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.
- **SCR08.C09 Inspect inbound controllers** (read): Inspect inbound ARC listener and connected controllers only Fields: Bound endpoint,principals,root,denied reasons and active stream/lease state. Failure: No ADB or outbound status inferred from this page Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.
- **SCR08.C10 Edit log quotas** (editor): Open the same per-client logging editor; draft then exact SCR09 CAS save for this client only Fields: Exact client_id and client_logging_config.logging_revision; mode/paused/retention/byte quota; global audit reserve is a read-only ceiling in this editor. Failure: Validation/CAS failure leaves both screens on prior committed policy Cancel: Discard only unsaved fields; committed configuration/revisions remain unchanged.
- **SCR08.C11 Inspect ADB backend** (read): Inspect ADB backend connection/identity and observed device facts Fields: Disconnected/pairing/connecting/connected/key-rejected; actual paired endpoint. Failure: No socket proves MCP authority; reconnect is bounded and cannot retry uncertain device input Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.
- **SCR08.C12 Reload settings** (read): Reload current config revision after save conflict Fields: Server values/local diff and required fresh approval. Failure: Never silently rebases a credential/listener/root change Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.

Phone: Single-column scrolling data/detail and48dp controls; preserve explicit draft fields on rotation. Modal drawer below840dp; persistent rail at840dp+. Back returns previous detail, with Discard/Stay for dirty drafts. Landscape: Same IDs and authorization; wide layout only when available width>=840dp; never shrink protected actions below48dp.

- [SCR08.S0](screens/states/SCR08.S0.svg) **Settings draft: committed revision visible**. Effect: Editor changes are not live until exact protected CAS save. Recovery: Save through SCR09 or Discard; inspect actual listener/backend status separately. Disabled: none.
- [SCR08.S1](screens/states/SCR08.S1.svg) **Waiting for unlock**. Effect: Credential-encrypted config/keys unavailable; minimal nonsecret boot flag only. Recovery: Unlock normally then eligible local Start; no secret migration to device-protected storage. Disabled: SCR08.C01, SCR08.C02, SCR08.C05, SCR08.C06, SCR08.C07, SCR08.C08, SCR08.C09, SCR08.C10, SCR08.C11.
- [SCR08.S2](screens/states/SCR08.S2.svg) **Foreground start ineligible**. Effect: Desired Running may be retained but listener is not ready; show Android reason. Recovery: Owner fixes prerequisite then explicit eligible foreground Start; no automatic settings change. Disabled: none.
- [SCR08.S3](screens/states/SCR08.S3.svg) **Settings revision conflict**. Effect: Exact CAS rejected all proposed fields; active listeners/config unchanged. Recovery: Reload settings and compare draft before new SCR09 proof. Disabled: SCR08.C01, SCR08.C08.
- [SCR08.S4](screens/states/SCR08.S4.svg) **ADB pairing/peer verification failed**. Effect: No Connected state; stored keys/profile are not replaced without owner review. Recovery: Inspect ADB reason and OS pairing endpoint; correct code/key/port then explicitly retry. Disabled: none.
- [SCR08.S5](screens/states/SCR08.S5.svg) **Inbound ARC disabled**. Effect: No ARC socket, controllers or exposed root exists. Recovery: Edit inbound listener then protected Save; MCP/ADB states remain independent. Disabled: SCR08.C08, SCR08.C09.
- [SCR08.S6](screens/states/SCR08.S6.svg) **Inbound ARC ready**. Effect: Bound127.0.0.1 endpoint, ordinary root and authenticated controllers shown. Recovery: Review controllers or disable/rotate via fresh exact owner Save; never implies ADB connected. Disabled: none.
- [SCR08.S7](screens/states/SCR08.S7.svg) **ADB pairing in progress**. Effect: Owner-enabled Wireless Debugging pairing endpoint and code are transient; connect port is separate. Recovery: Cancel pair or return to OS UI; split-screen/notification-assisted code retrieval is device-dependent. Disabled: SCR08.C01, SCR08.C08.
- [SCR08.S8](screens/states/SCR08.S8.svg) **ADB connecting**. Effect: Paired identity exists; discovered connect endpoint is being verified. Recovery: Timeout/key rejection returns disconnected; no device input is retried. Disabled: SCR08.C01, SCR08.C08.
- [SCR08.S9](screens/states/SCR08.S9.svg) **ADB connected**. Effect: Paired endpoint/identity verified; metadata facts remain separately permission-qualified. Recovery: Inspect health or intentionally disconnect profile; no MCP rule changed. Disabled: none.
- [SCR08.S10](screens/states/SCR08.S10.svg) **ADB key rejected**. Effect: TLS pairing identity not accepted; no fallback to unauthenticated5555. Recovery: Owner explicitly re-pairs after checking device/profile; old identity retained until confirmed replacement. Disabled: none.

## SCR09 — Protected owner approval

FR-ARC-04 / canonical Ask approval

![Screen definition](screens/SCR09.svg)

- **SCR09.C01 Authenticate and approve** (protected-action): Invoke fresh platform strong biometric/device credential prompt after input/capture quiescence Fields: Exact command hash/scope/revisions,nonce,original deadline and queue position. Failure: Expiry/CAS change/lockout/no enrollment/drain failure denies; only original exact approved command may commit Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.
- **SCR09.C02 Deny / dismiss** (cancel): Deny selected challenge and return its request with structured denial Fields: Current challenge ID; FIFO queue excluding expired entries. Failure: Back/system dismiss is the same denial; no side effect, no hidden next approval Cancel: This action is the explicit cancellation/discard. Report its recorded disposition; do not imply reversal of an already committed external effect.
- **SCR09.C03 Locked navigation** (disabled): Navigation drawer is absent/disabled while protected review is active Fields: Only Deny/Back or completed challenge exits. Failure: No remote input,deep link or notification action may invoke Approve Cancel: No action is available; Deny/Back on the enclosing protected review is the only exit.
- **SCR09.C04 Inspect exact challenge** (read): Show exact immutable challenge and current monotonic remaining time Fields: Principal/operation/hash/revisions/phase,queued count; secrets redacted. Failure: Changed plan terminates review; owner must open a newly issued challenge Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.

Phone: Single protected exact-command sheet; no navigation; Approve, Deny and immutable challenge only. No drawer or destination route on protected SCR09; Back denies current challenge. Landscape: Same IDs and authorization; wide layout only when available width>=840dp; never shrink protected actions below48dp.

- [SCR09.S0](screens/states/SCR09.S0.svg) **Exact local challenge: quiesced and unexpired**. Effect: No remote input/capture; one immutable command waits for platform proof. Recovery: Authenticate once or Deny/Back; queue is FIFO by issued time excluding expired requests. Disabled: SCR09.C03.
- [SCR09.S1](screens/states/SCR09.S1.svg) **Expired challenge: no execution**. Effect: Capability unusable; original request receives denial/expiry. Recovery: Dismiss; changed intent requires a newly prepared challenge, never renewal in place. Disabled: SCR09.C01, SCR09.C03.
- [SCR09.S2](screens/states/SCR09.S2.svg) **Plan/revision changed: no execution**. Effect: Exact command/CAS pins no longer match; old proof is inert. Recovery: Dismiss then inspect updated diff and request fresh approval. Disabled: SCR09.C01, SCR09.C03.
- [SCR09.S3](screens/states/SCR09.S3.svg) **Quiescence not proven: approval blocked**. Effect: Input/capture drain did not finish within2s; no OS prompt is opened. Recovery: Deny/dismiss and inspect pending bridge work; do not widen isolation claim. Disabled: SCR09.C01, SCR09.C03.
- [SCR09.S4](screens/states/SCR09.S4.svg) **Authentication unavailable/locked out**. Effect: No enrollment, strong authenticator unavailable or platform lockout; no capability issued. Recovery: Deny; owner may fix OS prerequisite explicitly outside bridge, then prepare a new challenge. Disabled: SCR09.C01, SCR09.C03.
- [SCR09.S5](screens/states/SCR09.S5.svg) **Authentication cancelled/failed**. Effect: No capability issued; selected request receives denial without side effect. Recovery: Dismiss; no automatic retry or queue-wide approval. Disabled: SCR09.C01, SCR09.C03.

## SCR10 — Action required

FR-KT-03 / no fabricated readiness

![Screen definition](screens/SCR10.svg)

- **SCR10.C01 Review prerequisites** (navigation): Open the precise Android prerequisite page or local help named by sanitized failure Fields: Notification permission/channel,FGS eligibility,debugging enrollment or store repair. Failure: Returning does not retry or grant; owner must choose explicit eligible Start/Refresh Cancel: Close drawer/Back to prior view. Dirty draft requires Discard or Stay; no save is implied.
- **SCR10.C02 Return to dashboard** (navigation): Return SCR01 preserving desired state and failed health tuple Fields: Actual listener/backend readiness and last reason. Failure: Error cannot be replaced by Running merely by navigation Cancel: Close drawer/Back to prior view. Dirty draft requires Discard or Stay; no save is implied.
- **SCR10.C03 Open navigation** (navigation): Open all local destinations through phone drawer or wide rail; Back returns previous screen Fields: SCR01..SCR08,SCR10,SCR11; protected review only via exact command. Failure: Dirty draft requires Discard/Stay; navigation never commits configuration Cancel: Close drawer/Back to prior view. Dirty draft requires Discard or Stay; no save is implied.
- **SCR10.C04 Open catalog import** (navigation): Open SCR03 import flow when catalog missing/quarantined Fields: Current catalog identity/reason and safe expected manifest selection. Failure: Quarantined imported bytes are never queried as trusted data Cancel: Close drawer/Back to prior view. Dirty draft requires Discard or Stay; no save is implied.
- **SCR10.C05 Inspect failure reason** (read): Inspect exact failure facet and permitted next action Fields: Android15/API35 reference,build/profile/eligibility and sanitized reason. Failure: No universal permission fix or blind retry; unavailable evidence stays unknown Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.
- **SCR10.C06 Retry safe observation or start** (session-command): Retry the explicitly selected safe startup/observation after prerequisite correction Fields: Current desired state/profile and original failure ID. Failure: Reevaluates eligibility/authority; cannot repeat uncertain device mutations Cancel: Cancel closes the owned pending transport/session action; report observed/unknown effects. Never resend uncertain input.

Phone: Single-column scrolling data/detail and48dp controls; preserve explicit draft fields on rotation. Modal drawer below840dp; persistent rail at840dp+. Back returns previous detail, with Discard/Stay for dirty drafts. Landscape: Same IDs and authorization; wide layout only when available width>=840dp; never shrink protected actions below48dp.

- [SCR10.S0](screens/states/SCR10.S0.svg) **Action required: failed health facet**. Effect: Service Error, MCP error, catalog quarantine and stale view shown independently. Recovery: Inspect exact reason; permitted recovery is explicit and scoped. Disabled: none.
- [SCR10.S1](screens/states/SCR10.S1.svg) **Catalog absent/quarantined**. Effect: No device admission; old imported bytes are not trusted. Recovery: Open catalog import; only independently verified replacement can activate. Disabled: SCR10.C06.
- [SCR10.S2](screens/states/SCR10.S2.svg) **Backend disconnected: facts unknown**. Effect: Backend not connected; unavailable observation is not proof of removed API. Recovery: Inspect paired profile and safe reconnect; never replay uncertain invocation. Disabled: none.
- [SCR10.S3](screens/states/SCR10.S3.svg) **Cancelled recovery: desired Stopped**. Effect: No auto-restart or retry; previously committed config remains. Recovery: Return dashboard then explicitly Start when eligible. Disabled: SCR10.C06.
- [SCR10.S4](screens/states/SCR10.S4.svg) **Prerequisite fixed: safe retry available**. Effect: Show exact safe observation/start that will be attempted, not a device mutation. Recovery: Retry reevaluates all gates; failure returns to the reason page. Disabled: none.

## SCR11 — Client pairing

FR-KT-02 / canonical client identity

![Screen definition](screens/SCR11.svg)

- **SCR11.C01 Generate pairing challenge** (protected-action): Local SCR09 authorizes opening a pairing window; separate action creates60s attempt Fields: Flow,server TLS fingerprint,attempt nonce/expiry and exact requested client label. Failure: Window/attempt expiry closes it; no principal exists until proof and separate exact owner commit Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.
- **SCR11.C02 Cancel pairing** (cancel): Cancel only an uncommitted attempt and erase bootstrap secret Fields: Current attempt ID,state and original expiry. Failure: Committed client cannot be unpaired here; revoke it explicitly on SCR02 Cancel: This action is the explicit cancellation/discard. Report its recorded disposition; do not imply reversal of an already committed external effect.
- **SCR11.C03 Open navigation** (navigation): Open all local destinations through phone drawer or wide rail; Back returns previous screen Fields: SCR01..SCR08,SCR10,SCR11; protected review only via exact command. Failure: Dirty draft requires Discard/Stay; navigation never commits configuration Cancel: Close drawer/Back to prior view. Dirty draft requires Discard or Stay; no save is implied.
- **SCR11.C04 Choose pairing flow** (selector): Choose manual_bearer_setup or pairing_helper_v1 before creating attempt Fields: Helper requires paired_tls; manual credential generation/reveal is local-only. Failure: Reduced HTTP cannot enable pairing; no implicit OAuth endpoint Cancel: Restore previous selection; selection does not connect, grant, activate or publish.
- **SCR11.C05 Inspect submitted client proof** (read): Inspect authenticated submitted proof and pinned enrollment transcript Fields: Nonce,Origin,endpoint,requested label and server-certificate proof; no fabricated client key. Failure: Wrong proof/origin rejects attempt without creating client Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.
- **SCR11.C06 Review exact client commit** (protected-action): Open fresh SCR09 exact enrollment commit before nonce expires Fields: Attempt/principal kind/client label and all initial rules Deny. Failure: Expiry/changed proof requires new attempt; approval never directly grants device calls Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.
- **SCR11.C07 Reveal/share manual bearer once** (protected-action): Generate/rotate manual bearer under fresh exact SCR09 review and reveal once Fields: Selected client kind; app stores verifier and Keystore-protected secret reference only when required. Failure: Protected reveal is not an ARC root; copy uses explicit selected-recipient share, never logs; cancel before reveal erases pending secret Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.

Phone: Enrollment flow and protected single-use reveal are separate pages; never show secret on overview. Modal drawer below840dp; persistent rail at840dp+. Back returns previous detail, with Discard/Stay for dirty drafts. Landscape: Same IDs and authorization; wide layout only when available width>=840dp; never shrink protected actions below48dp.

- [SCR11.S0](screens/states/SCR11.S0.svg) **Enrollment flow selected: no principal**. Effect: Window/attempt not yet created; choose helper or manual profile. Recovery: Generate local challenge; all future device rules begin Deny. Disabled: SCR11.C02, SCR11.C05, SCR11.C06, SCR11.C07.
- [SCR11.S1](screens/states/SCR11.S1.svg) **Attempt expired**. Effect: Bootstrap secret erased; no principal/grant committed. Recovery: Create new attempt only under still-valid local pairing window. Disabled: SCR11.C02, SCR11.C05, SCR11.C06, SCR11.C07.
- [SCR11.S2](screens/states/SCR11.S2.svg) **Proof/origin rejected**. Effect: Wrong proof or endpoint pin creates no client and closes attempt. Recovery: Inspect sanitized reason then create a new attempt; never approve rejected proof. Disabled: SCR11.C02, SCR11.C05, SCR11.C06, SCR11.C07.
- [SCR11.S3](screens/states/SCR11.S3.svg) **Cancelled uncommitted attempt**. Effect: Secret erased; no client exists from this attempt. Recovery: Choose a new flow or return; cannot cancel a previously committed client. Disabled: SCR11.C02, SCR11.C05, SCR11.C06, SCR11.C07.
- [SCR11.S4](screens/states/SCR11.S4.svg) **Proof submitted: exact owner commit pending**. Effect: Nonce/Origin/endpoint proof visible; still no principal. Recovery: Open SCR09 fresh exact commit before60s expiry, or cancel. Disabled: SCR11.C01, SCR11.C04, SCR11.C07.
- [SCR11.S5](screens/states/SCR11.S5.svg) **Paired: all rules Deny**. Effect: Principal committed; token disclosure does not approve Android capability. Recovery: Return SCR02 to separately review exact grants; revoke there to remove client. Disabled: SCR11.C01, SCR11.C02, SCR11.C04, SCR11.C05, SCR11.C06.
- [SCR11.S6](screens/states/SCR11.S6.svg) **Manual bearer sealed: protected one-time reveal**. Effect: Owner-issued verifier stored; secret view/share excluded from ARC/capture/logs. Recovery: Reveal/share once to explicit recipient; cancel erases pending reveal, not a committed principal. Disabled: SCR11.C01, SCR11.C02, SCR11.C04, SCR11.C05, SCR11.C06.

## STB01 — Job discovery, exact Ask and callback delivery

![Storyboard panels](screens/STB01.svg)

External events use UI observation anchors only, not remote button presses. Exact local owner approval remains separate. Self-ADB/interest activation does not grant any MCP capability. Branch labels are alternatives, not automatic side-effect retries.

### STB01-1 — main

External MCP client; SCR04.S0; SCR04.C06 Search group summaries. Agent calls android.groups.discover for job; UI is observation anchor

Success: Ranked bounded summaries with permission/applicability facets; no authority granted

Error: Budget exceeded returns smaller page/cursor, not all schemas

Cancel: Cancel this bounded search; retain query draft and committed view, no remote mutation.

Transitions: success STB01-2; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB01-2 — main

External MCP client; SCR04.S0; SCR04.C07 Describe selected group. Agent calls android.groups.describe then android.groups.set_active with CAS

Success: Small coherent interest view committed; generic binding.invoke remains; activation grants nothing

Error: Stale group revision invalidates page; reload summary before client activation

Cancel: Cancel this page read; keep previous valid cursor/status and require explicit next read.

Transitions: success STB01-8; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB01-3 — deny-rule alternative

External MCP client; SCR02.S2; SCR02.C05 Select client. Deny branch: agent invokes denied binding

Success: Structured invocation denial; no backend registration/input

Error: Deleted/revoked selection shows terminal status; clear draft

Cancel: Restore previous selection; selection does not connect, grant, activate or publish.

Transitions: success END_WITH_RECORDED_ALTERNATIVE; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB01-4 — Ask-rule alternative

External MCP client; SCR01.S4; SCR01.C04 Open health details. Ask branch: agent submits one prepared exact invocation

Success: Pending local notification opens SCR09; original request deadline unchanged

Error: Missing facet is unknown, never inferred from another ready facet

Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.

Transitions: success END_WITH_RECORDED_ALTERNATIVE; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB01-5 — Ask approved

Owner; SCR09.S0; SCR09.C01 Authenticate and approve. Owner approves this one Ask after quiescence

Success: Reserved proof consumed at beginDispatch; finite invocation response

Error: Expiry/CAS change/lockout/no enrollment/drain failure denies; only original exact approved command may commit

Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.

Transitions: success STB01-8; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB01-6 — Ask expires

Owner; SCR09.S1; SCR09.C02 Deny / dismiss. Ask timeout branch

Success: Original request denied as expired; no side effect

Error: Back/system dismiss is the same denial; no side effect, no hidden next approval

Cancel: This action is the explicit cancellation/discard. Report its recorded disposition; do not imply reversal of an already committed external effect.

Transitions: success END_WITH_RECORDED_ALTERNATIVE; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB01-7 — Ask denied

Owner; SCR09.S0; SCR09.C02 Deny / dismiss. Ask denied/cancelled branch

Success: Only this prepared execution denied; no persistent grant change

Error: Back/system dismiss is the same denial; no side effect, no hidden next approval

Cancel: This action is the explicit cancellation/discard. Report its recorded disposition; do not imply reversal of an already committed external effect.

Transitions: success END_WITH_RECORDED_ALTERNATIVE; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB01-8 — main

External MCP client; SCR05.S0; SCR05.C06 Refresh status. Agent binding.invoke sets callback; then resources/subscribe and resources/read

Success: Finite handle; URI-only update hints; finite journal pages and explicit route/lease

Error: Death/revoke hides payload; uncertainty stays until supported reconciliation

Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.

Transitions: success STB01-9; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB01-9 — main

Owner; SCR05.S0; SCR05.C01 Inspect bounded status. Owner reads exact live subscription status

Success: Inspect selected subscription status without opening a backend registration

Error: Unknown registration vs cleanup phase remains distinct; no replay

Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.

Transitions: success STB01-11; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB01-10 — cursor expired

Owner; SCR05.S2; SCR05.C06 Refresh status. Retention branch: application cursor expired

Success: Show earliest retained cursor and gap; no automatic re-registration

Error: Death/revoke hides payload; uncertainty stays until supported reconciliation

Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.

Transitions: success END_WITH_RECORDED_ALTERNATIVE; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB01-11 — main

Owner; SCR05.S0; SCR05.C02 Cancel via owner review. Owner requests administrative cancellation

Success: SCR09 binds exact handle/revision and cleanup plan

Error: Cleanup failure records unknown; cannot promise backend unregister from timeout

Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.

Transitions: success STB01-12; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB01-12 — main

Owner; SCR09.S0; SCR09.C01 Authenticate and approve. Approve only this subscription cancel

Success: Delivery closes; verified cleanup attempted within deadline

Error: Expiry/CAS change/lockout/no enrollment/drain failure denies; only original exact approved command may commit

Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.

Transitions: success STB01-13; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB01-13 — main

External MCP client; SCR05.S4; SCR05.C06 Refresh status. Agent observes retained terminal status

Success: callback.status returns closed with confirmed/unknown cleanup

Error: Death/revoke hides payload; uncertainty stays until supported reconciliation

Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.

Transitions: success END; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB01-14 — cleanup unknown

Owner; SCR05.S3; SCR05.C07 Review cleanup reconciliation. Uncertain cleanup branch: review available evidence

Success: No replay without proven idempotence/current authority; phase remains cleanup

Error: No proof means inspect/forget delivery handle only; never repeat uncertain non-idempotent unregister

Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.

Transitions: success END_WITH_RECORDED_ALTERNATIVE; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB01-15 — unsafe retry rejected

Owner; SCR05.S5; SCR05.C01 Inspect bounded status. No safe retry proof

Success: Payload closed; unknown cleanup explicitly retained

Error: Unknown registration vs cleanup phase remains distinct; no replay

Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.

Transitions: success END_WITH_RECORDED_ALTERNATIVE; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

## STB02 — Outbound ARC profile, observation and control

![Storyboard panels](screens/STB02.svg)

External events use UI observation anchors only, not remote button presses. Exact local owner approval remains separate. Self-ADB/interest activation does not grant any MCP capability. Branch labels are alternatives, not automatic side-effect retries.

### STB02-1 — main

Owner; SCR07.S0; SCR07.C09 Edit outbound target profile. Owner creates exact target profile and secret reference

Success: SCR09 reviews endpoint/target/user; secret never becomes remote root

Error: Secret fields are protected/not ARC roots; never put bearer in logs/exports/discovery

Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.

Transitions: success STB02-2; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB02-2 — main

Owner; SCR09.S0; SCR09.C01 Authenticate and approve. Approve outbound profile save

Success: Encrypted secret reference and exact target profile committed

Error: Expiry/CAS change/lockout/no enrollment/drain failure denies; only original exact approved command may commit

Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.

Transitions: success STB02-3; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB02-3 — main

Owner; SCR07.S0; SCR07.C05 Select target profile. Select saved target profile without connecting

Success: Choose saved outbound profile without connecting

Error: Missing secret/profile requires explicit owner profile edit; no default remote target

Cancel: Restore previous selection; selection does not connect, grant, activate or publish.

Transitions: success STB02-4; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB02-4 — main

Owner; SCR07.S0; SCR07.C01 Connect selected target. Explicit Connect requests capabilities-first convention

Success: Connecting state; no target input yet

Error: Wrong token/protocol/EOF shows disconnected state; never sends uncertain input again

Cancel: Cancel closes the owned pending transport/session action; report observed/unknown effects. Never resend uncertain input.

Transitions: success STB02-5; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB02-5 — main

Owner; SCR07.S5; SCR07.C08 Inspect target details. Inspect connection attempt

Success: UI displays connecting, exact target and wire profile

Error: Unknown remote root generation remains unknown

Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.

Transitions: success STB02-6; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB02-6 — main

Owner; SCR07.S4; SCR07.C06 Inspect tree or refresh frame. Connected: inspect bounded tree/frame

Success: Validate envelope version/request ID/status and payload; current frame geometry shown

Error: Disconnected/oversize/protected data denies; stale frame never becomes proof of intended coordinate

Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.

Transitions: success STB02-7; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB02-7 — main

Owner; SCR07.S4; SCR07.C04 Send typed input; disabled unless connected capability/policy permits. Send one allowed key/text operation

Success: Shared gate and local reply/input policy apply; inspect CommandResult.succeeded

Error: Unknown key/protected/root/policy failure shows CommandResult; no implicit grant or retry

Cancel: Cancel closes the owned pending transport/session action; report observed/unknown effects. Never resend uncertain input.

Transitions: success STB02-8; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB02-8 — main

Owner; SCR07.S4; SCR07.C07 Pointer or wheel input. Pointer/wheel operation under current input lease

Success: One pointer in logical coordinates; no remote generation or multitouch invented

Error: Lease conflict/protected root rejects; pressed state released on revoke/owner transition

Cancel: Stop submitting new input; release owned pressed state on explicit lease/session close. A delivered click/key cannot be undone.

Transitions: success STB02-13; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB02-9 — wrong credential

Owner; SCR07.S1; SCR07.C08 Inspect target details. Wrong-token branch

Success: UNAUTHENTICATED; content/input remain disabled

Error: Unknown remote root generation remains unknown

Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.

Transitions: success END_WITH_RECORDED_ALTERNATIVE; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB02-10 — unsupported wire

Owner; SCR07.S2; SCR07.C08 Inspect target details. Wrong-version branch

Success: Unsupported wire rejected; no downgrade or target action

Error: Unknown remote root generation remains unknown

Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.

Transitions: success END_WITH_RECORDED_ALTERNATIVE; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB02-11 — protected root

Owner; SCR07.S3; SCR07.C08 Inspect target details. Protected-root branch

Success: Frame/tree/input suspended before protected capture

Error: Unknown remote root generation remains unknown

Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.

Transitions: success END_WITH_RECORDED_ALTERNATIVE; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB02-12 — input denied

Owner; SCR07.S6; SCR07.C08 Inspect target details. Lease-conflict branch

Success: Failed command; no competing input; inspect owner/reason

Error: Unknown remote root generation remains unknown

Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.

Transitions: success END_WITH_RECORDED_ALTERNATIVE; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB02-13 — main

Owner; SCR07.S4; SCR07.C02 Disconnect / cancel. Explicit disconnect/cancel watch

Success: Close owned socket, no Cancel8; release local resources

Error: No final frame is assumed; old frame is visibly stale and input disabled

Cancel: Cancel closes the owned pending transport/session action; report observed/unknown effects. Never resend uncertain input.

Transitions: success STB02-14; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB02-14 — main

Owner; SCR07.S0; SCR07.C08 Inspect target details. Disconnected result

Success: Last frame labeled stale; no input or phantom Connected state

Error: Unknown remote root generation remains unknown

Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.

Transitions: success END; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

## STB03 — Local startup, same-device ADB and health

![Storyboard panels](screens/STB03.svg)

External events use UI observation anchors only, not remote button presses. Exact local owner approval remains separate. Self-ADB/interest activation does not grant any MCP capability. Branch labels are alternatives, not automatic side-effect retries.

### STB03-1 — main

Owner; SCR08.S0; SCR08.C05 Edit startup and MCP listener. Edit boot/listener draft

Success: No committed config change; desired/actual status separate

Error: Unsupported FGS/TLS profile shows failure; Save needs exact SCR09 approval

Cancel: Discard only unsaved fields; committed configuration/revisions remain unchanged.

Transitions: success STB03-3; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB03-2 — cancel before save

Owner; SCR08.S0; SCR08.C04 Discard settings draft. Discard-settings alternative

Success: Previous boot/listener configuration retained

Error: No boot/listener/policy changes; active sockets remain until explicit lifecycle command

Cancel: This action is the explicit cancellation/discard. Report its recorded disposition; do not imply reversal of an already committed external effect.

Transitions: success END_WITH_RECORDED_ALTERNATIVE; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB03-3 — main

Owner; SCR08.S0; SCR08.C01 Save with revision. Save exact configuration draft

Success: SCR09 shows fields/revisions and original expiry

Error: Conflict rejects entire draft; bind failure keeps desired vs actual separate

Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.

Transitions: success STB03-4; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB03-4 — main

Owner; SCR09.S0; SCR09.C01 Authenticate and approve. Authenticate exact settings CAS

Success: Commit revision; return SCR08 without implying listener ready

Error: Expiry/CAS change/lockout/no enrollment/drain failure denies; only original exact approved command may commit

Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.

Transitions: success STB03-5; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB03-5 — main

Owner; SCR01.S3; SCR01.C01 Start / Stop service. Local eligible foreground Start

Success: Starting; no device admission until listener/view readiness

Error: Start eligibility or bind failure shows Error; stop timeout shows unknown cleanup and closed admission

Cancel: During Start, explicit Stop closes admission; after Stop begins, cleanup remains bounded and is not rolled back into Running.

Transitions: success STB03-6; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB03-6 — main

Owner; SCR01.S1; SCR01.C04 Open health details. Observe Starting health tuple

Success: Expand independent health facets and their reasons on SCR01

Error: Missing facet is unknown, never inferred from another ready facet

Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.

Transitions: success STB03-7; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB03-7 — main

Owner; SCR01.S0; SCR01.C04 Open health details. Observe actual Running tuple

Success: MCP ready and ADB offline are separate facts

Error: Missing facet is unknown, never inferred from another ready facet

Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.

Transitions: success STB03-8; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB03-8 — main

Owner; SCR08.S0; SCR08.C06 Edit same-device ADB profile. Owner-enabled OS Wireless Debugging pairing profile

Success: Pair endpoint/code separate from discovered connect endpoint; no device setting toggled

Error: Background/split-screen OS pairing constraints visible; wrong key/port never reports Connected

Cancel: Discard only unsaved fields; committed configuration/revisions remain unchanged.

Transitions: success STB03-9; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB03-9 — main

Owner; SCR08.S7; SCR08.C11 Inspect ADB backend. Inspect pairing progress

Success: OS UI/split-screen availability disclosed; secrets not logged

Error: No socket proves MCP authority; reconnect is bounded and cannot retry uncertain device input

Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.

Transitions: success STB03-10; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB03-10 — main

Owner; SCR08.S8; SCR08.C11 Inspect ADB backend. Inspect verified identity connection attempt

Success: Inspect ADB backend connection/identity and observed device facts

Error: No socket proves MCP authority; reconnect is bounded and cannot retry uncertain device input

Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.

Transitions: success STB03-11; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB03-11 — main

Owner; SCR08.S9; SCR08.C11 Inspect ADB backend. ADB connected result

Success: Paired verified endpoint only; no MCP grant created

Error: No socket proves MCP authority; reconnect is bounded and cannot retry uncertain device input

Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.

Transitions: success STB03-12; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB03-12 — main

External MCP client; SCR01.S0; SCR01.C04 Open health details. Agent invokes approved ADB-backed screen.state/input.tap

Success: Single bounded observation/action through generic binding.invoke; no workflow scheduler

Error: Missing facet is unknown, never inferred from another ready facet

Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.

Transitions: success STB03-16; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB03-13 — ADB auth failure

Owner; SCR08.S10; SCR08.C11 Inspect ADB backend. Key-rejected branch

Success: No unauthenticated5555 fallback; explicit owner re-pair required

Error: No socket proves MCP authority; reconnect is bounded and cannot retry uncertain device input

Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.

Transitions: success END_WITH_RECORDED_ALTERNATIVE; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB03-14 — start failed

Owner; SCR10.S0; SCR10.C05 Inspect failure reason. Foreground/bind failure branch

Success: Error facet and no Running chip; exact safe next action

Error: No universal permission fix or blind retry; unavailable evidence stays unknown

Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.

Transitions: success END_WITH_RECORDED_ALTERNATIVE; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB03-15 — safe recovery

Owner; SCR10.S4; SCR10.C06 Retry safe observation or start. Prerequisite corrected: explicit safe retry

Success: Reevaluate eligibility/profile; never retry uncertain device mutation

Error: Reevaluates eligibility/authority; cannot repeat uncertain device mutations

Cancel: Cancel closes the owned pending transport/session action; report observed/unknown effects. Never resend uncertain input.

Transitions: success STB03-16; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB03-16 — main

Owner; SCR01.S0; SCR01.C01 Start / Stop service. Explicit Stop

Success: Admission/listeners close, bounded cleanup before store/backend close

Error: Start eligibility or bind failure shows Error; stop timeout shows unknown cleanup and closed admission

Cancel: During Start, explicit Stop closes admission; after Stop begins, cleanup remains bounded and is not rolled back into Running.

Transitions: success STB03-17; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB03-17 — main

Owner; SCR01.S5; SCR01.C04 Open health details. Observe Stopping then Stopped

Success: Unknown cleanup retained; manual Stop suppresses boot restart

Error: Missing facet is unknown, never inferred from another ready facet

Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.

Transitions: success END; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB03-18 — pre-unlock

Owner; SCR08.S1; SCR08.C03 Open navigation. Boot before unlock branch

Success: Nonsecret boot flag only; no credentials copied to device-protected storage

Error: Dirty draft requires Discard/Stay; navigation never commits configuration

Cancel: Close drawer/Back to prior view. Dirty draft requires Discard or Stay; no save is implied.

Transitions: success END_WITH_RECORDED_ALTERNATIVE; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

## STB04 — Client enrollment, grants, sessions and revocation

![Storyboard panels](screens/STB04.svg)

External events use UI observation anchors only, not remote button presses. Exact local owner approval remains separate. Self-ADB/interest activation does not grant any MCP capability. Branch labels are alternatives, not automatic side-effect retries.

### STB04-1 — main

Owner; SCR02.S1; SCR02.C04 Add client. No clients: Add client opens enrollment

Success: Open SCR11 client enrollment; new clients start Deny

Error: No proof/owner commit means no principal or grant

Cancel: Close drawer/Back to prior view. Dirty draft requires Discard or Stay; no save is implied.

Transitions: success STB04-2; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB04-2 — main

Owner; SCR11.S0; SCR11.C04 Choose pairing flow. Choose helper or manual profile

Success: Helper requires pinned TLS; no OAuth token endpoint implied

Error: Reduced HTTP cannot enable pairing; no implicit OAuth endpoint

Cancel: Restore previous selection; selection does not connect, grant, activate or publish.

Transitions: success STB04-3; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB04-3 — main

Owner; SCR11.S0; SCR11.C01 Generate pairing challenge. Local action creates approved window/one60s attempt

Success: QR or explicit selected-recipient share; server fingerprint and nonce exact

Error: Window/attempt expiry closes it; no principal exists until proof and separate exact owner commit

Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.

Transitions: success STB04-4; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB04-4 — main

External MCP client; SCR11.S4; SCR11.C05 Inspect submitted client proof. Client submits enrollment proof

Success: No principal yet; inspect nonce/Origin/endpoint and expiry

Error: Wrong proof/origin rejects attempt without creating client

Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.

Transitions: success STB04-5; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB04-5 — main

Owner; SCR11.S4; SCR11.C06 Review exact client commit. Owner requests exact enrollment commit

Success: Fresh SCR09 review distinct from opening window

Error: Expiry/changed proof requires new attempt; approval never directly grants device calls

Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.

Transitions: success STB04-6; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB04-6 — main

Owner; SCR09.S0; SCR09.C01 Authenticate and approve. Approve exact principal commit

Success: Client committed with every device rule Deny

Error: Expiry/CAS change/lockout/no enrollment/drain failure denies; only original exact approved command may commit

Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.

Transitions: success STB04-7; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB04-7 — main

Owner; SCR11.S5; SCR11.C03 Open navigation. Paired result; cancellation no longer available

Success: Return SCR02; explicit revoke is needed to remove principal

Error: Dirty draft requires Discard/Stay; navigation never commits configuration

Cancel: Close drawer/Back to prior view. Dirty draft requires Discard or Stay; no save is implied.

Transitions: success STB04-8; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB04-8 — main

Owner; SCR02.S0; SCR02.C06 Choose exact rule. Draft exact canonical binding/schema/target rule

Success: Deny/Ask/Allow draft only; unknown schema cannot become Allow

Error: Unknown schema cannot become Allow; choose narrower known rule or cancel

Cancel: Discard only unsaved fields; committed configuration/revisions remain unchanged.

Transitions: success STB04-9; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB04-9 — main

Owner; SCR02.S0; SCR02.C01 Edit exact grants. Open exact grant commit review

Success: Preview exact grant diff then SCR09; CAS commit selected client rule

Error: 403/cancel leaves old rule;409/412 opens reload/diff before new approval

Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.

Transitions: success STB04-10; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB04-10 — main

Owner; SCR09.S0; SCR09.C01 Authenticate and approve. Approve grant CAS

Success: Rule epoch committed; one exact rule gains stated policy only

Error: Expiry/CAS change/lockout/no enrollment/drain failure denies; only original exact approved command may commit

Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.

Transitions: success STB04-11; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB04-11 — main

Owner; SCR02.S0; SCR02.C08 Inspect active sessions. Inspect active sessions

Success: List bounded active MCP/ARC sessions owned by selected client

Error: Stale cursor reloads session list without recreating a session

Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.

Transitions: success STB04-12; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB04-12 — main

Owner; SCR02.S0; SCR02.C09 End selected session. End one selected session through exact review

Success: SCR09 approves session close, not credential revoke

Error: Already closed is idempotent terminal; uncertain transport close never replays device calls

Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.

Transitions: success STB04-13; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB04-13 — main

Owner; SCR02.S0; SCR02.C02 Revoke credential. Revoke credential through separate exact review

Success: SCR09 approves exact credential revoke; commit epoch and close owned sessions/streams

Error: Conflict requires fresh review; no automatic replacement token

Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.

Transitions: success STB04-14; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB04-14 — main

Owner; SCR09.S0; SCR09.C01 Authenticate and approve. Approve exact revoke

Success: Verifier/epoch invalid; session/stream/input delivery closes

Error: Expiry/CAS change/lockout/no enrollment/drain failure denies; only original exact approved command may commit

Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.

Transitions: success STB04-15; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB04-15 — main

Owner; SCR02.S4; SCR02.C08 Inspect active sessions. Revoked result

Success: Historical record retained; old bearer cannot reconnect

Error: Stale cursor reloads session list without recreating a session

Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.

Transitions: success END; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB04-16 — attempt expired

Owner; SCR11.S1; SCR11.C01 Generate pairing challenge. Expired-attempt branch

Success: Old bootstrap secret erased; new attempt has new nonce

Error: Window/attempt expiry closes it; no principal exists until proof and separate exact owner commit

Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.

Transitions: success END_WITH_RECORDED_ALTERNATIVE; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB04-17 — proof rejected

Owner; SCR11.S2; SCR11.C03 Open navigation. Wrong proof/origin branch

Success: No principal or grant committed

Error: Dirty draft requires Discard/Stay; navigation never commits configuration

Cancel: Close drawer/Back to prior view. Dirty draft requires Discard or Stay; no save is implied.

Transitions: success END_WITH_RECORDED_ALTERNATIVE; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB04-18 — auth unavailable

Owner; SCR09.S4; SCR09.C02 Deny / dismiss. Authenticator unavailable/lockout branch

Success: No owner capability; fix prerequisite outside bridge if desired

Error: Back/system dismiss is the same denial; no side effect, no hidden next approval

Cancel: This action is the explicit cancellation/discard. Report its recorded disposition; do not imply reversal of an already committed external effect.

Transitions: success END_WITH_RECORDED_ALTERNATIVE; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB04-19 — stale plan

Owner; SCR09.S2; SCR09.C02 Deny / dismiss. Changed plan/CAS branch

Success: Old approval inert; inspect new exact diff

Error: Back/system dismiss is the same denial; no side effect, no hidden next approval

Cancel: This action is the explicit cancellation/discard. Report its recorded disposition; do not imply reversal of an already committed external effect.

Transitions: success END_WITH_RECORDED_ALTERNATIVE; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB04-20 — auth cancelled

Owner; SCR09.S5; SCR09.C02 Deny / dismiss. Biometric cancelled/failed branch

Success: Only selected request denied; FIFO queue next request needs own proof

Error: Back/system dismiss is the same denial; no side effect, no hidden next approval

Cancel: This action is the explicit cancellation/discard. Report its recorded disposition; do not imply reversal of an already committed external effect.

Transitions: success END_WITH_RECORDED_ALTERNATIVE; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB04-21 — manual enrollment

Owner; SCR11.S6; SCR11.C07 Reveal/share manual bearer once. Manual bearer alternative: protected one-time reveal

Success: Owner-approved share to selected recipient; no log/capture/remote root

Error: Protected reveal is not an ARC root; copy uses explicit selected-recipient share, never logs; cancel before reveal erases pending secret

Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.

Transitions: success END_WITH_RECORDED_ALTERNATIVE; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

## STB05 — Catalog, functional mapping, export and admin staging

![Storyboard panels](screens/STB05.svg)

External events use UI observation anchors only, not remote button presses. Exact local owner approval remains separate. Self-ADB/interest activation does not grant any MCP capability. Branch labels are alternatives, not automatic side-effect retries.

### STB05-1 — main

Owner; SCR10.S1; SCR10.C04 Open catalog import. Catalog absent: open safe import flow

Success: Open SCR03 import flow when catalog missing/quarantined

Error: Quarantined imported bytes are never queried as trusted data

Cancel: Close drawer/Back to prior view. Dirty draft requires Discard or Stay; no save is implied.

Transitions: success STB05-2; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB05-2 — main

Owner; SCR03.S6; SCR03.C06 Choose import document. Choose expected validated release via SAF

Success: Hash/size/capacity/deadline preview; active catalog unchanged

Error: Picker cancel releases transient grant; hash/schema/space failure leaves active catalog unchanged

Cancel: Picker cancel releases transient URI grant and pending unactivated snapshot/stage. If a write already began, show partial/unknown destination state.

Transitions: success STB05-4; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB05-3 — invalid import

Owner; SCR03.S5; SCR03.C01 Inspect evidence. Import validation failure branch

Success: Hash/schema/FK/space reason; never query quarantined bytes as trusted

Error: Missing evidence remains explicitly unknown; read never grants permission

Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.

Transitions: success END_WITH_RECORDED_ALTERNATIVE; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB05-4 — main

Owner; SCR03.S7; SCR03.C07 Preview override diff. Validated sealed import preview

Success: Show registry/hash/size/revision/conflicts and stage expiry

Error: Invalid preview shows precise field errors; owner prearming/commit requires SCR09

Cancel: Discard only unsaved fields; committed configuration/revisions remain unchanged.

Transitions: success STB05-5; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB05-5 — main

Owner; SCR03.S7; SCR03.C09 Activate validated import. Request exact registry activation

Success: SCR09 approves exact validated registry publication; atomic CAS activates it

Error: Failed validation/CAS/approval leaves old active registry; new review required

Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.

Transitions: success STB05-6; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB05-6 — main

Owner; SCR09.S0; SCR09.C01 Authenticate and approve. Approve exact import publication CAS

Success: Atomic registry/overlay generation switch or explicit conflict

Error: Expiry/CAS change/lockout/no enrollment/drain failure denies; only original exact approved command may commit

Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.

Transitions: success STB05-7; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB05-7 — main

Owner; SCR03.S0; SCR03.C01 Inspect evidence. Inspect active imported evidence

Success: Open immutable source evidence and effective binding/type revision

Error: Missing evidence remains explicitly unknown; read never grants permission

Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.

Transitions: success STB05-8; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB05-8 — main

Owner; SCR03.S0; SCR03.C02 Edit local override. Draft local override

Success: Immutable import retained; changed dependency/hash preview

Error: Invalid schema/dependency blocks preview; discard returns immutable effective record

Cancel: Discard only unsaved fields; committed configuration/revisions remain unchanged.

Transitions: success STB05-9; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB05-9 — main

Owner; SCR03.S4; SCR03.C07 Preview override diff. Preview exact override/admin command fields

Success: Show exact override/import/admin command preview; no commit yet

Error: Invalid preview shows precise field errors; owner prearming/commit requires SCR09

Cancel: Discard only unsaved fields; committed configuration/revisions remain unchanged.

Transitions: success STB05-10; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB05-10 — main

Owner; SCR09.S0; SCR09.C01 Authenticate and approve. Approve exact override commit

Success: CAS commit one new overlay revision; old approval invalidated on conflict

Error: Expiry/CAS change/lockout/no enrollment/drain failure denies; only original exact approved command may commit

Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.

Transitions: success STB05-12; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB05-11 — cancel draft

Owner; SCR03.S4; SCR03.C08 Discard override or import. Discard alternative before commit

Success: Draft/private unactivated stage removed; active import retained

Error: Active imported history is retained; cleanup unknown is surfaced, never publication

Cancel: This action is the explicit cancellation/discard. Report its recorded disposition; do not imply reversal of an already committed external effect.

Transitions: success END_WITH_RECORDED_ALTERNATIVE; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB05-12 — main

Owner; SCR04.S1; SCR04.C09 Edit functional group members. Create small functional mapping draft from searchable unassigned entries

Success: Create or edit functional member list and required dependency closure as a draft

Error: Unassigned entries remain searchable; cycles/oversize show errors before save

Cancel: Discard only unsaved fields; committed configuration/revisions remain unchanged.

Transitions: success STB05-13; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB05-13 — main

Owner; SCR04.S4; SCR04.C02 Save group mapping. Save coherent mapping through exact owner review

Success: SCR09 approves exact local group-mapping create/update with CAS

Error: Budget/dependency/conflict error saves no partial group or activation

Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.

Transitions: success STB05-14; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB05-14 — main

Owner; SCR09.S0; SCR09.C01 Authenticate and approve. Approve functional mapping CAS

Success: One group mapping revision; no client interest/permission change

Error: Expiry/CAS change/lockout/no enrollment/drain failure denies; only original exact approved command may commit

Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.

Transitions: success STB05-17; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB05-15 — budget exceeded

Owner; SCR04.S2; SCR04.C09 Edit functional group members. Oversized/dependency failure branch

Success: Edit member closure; no partial mapping saved

Error: Unassigned entries remain searchable; cycles/oversize show errors before save

Cancel: Discard only unsaved fields; committed configuration/revisions remain unchanged.

Transitions: success END_WITH_RECORDED_ALTERNATIVE; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB05-16 — CAS conflict

Owner; SCR04.S3; SCR04.C10 Reload group mapping. Mapping conflict branch

Success: Reload diff then new exact review

Error: Preserve local draft separately; fresh approval needed for changed command

Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.

Transitions: success END_WITH_RECORDED_ALTERNATIVE; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB05-17 — main

Owner; SCR03.S8; SCR03.C10 Manage admin window. Local enable import-only HTTP admin window

Success: SCR09 binds admin principal, artifact and configured lifetime

Error: Expiry/revoke closes admin work; no MCP credential becomes admin; no hidden listener

Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.

Transitions: success STB05-18; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB05-18 — main

Owner; SCR03.S4; SCR03.C07 Preview override diff. Prearm exact external HTTP upload/import request

Success: Transfer nonsecret challenge reference; HTTP cannot open approval prompt

Error: Invalid preview shows precise field errors; owner prearming/commit requires SCR09

Cancel: Discard only unsaved fields; committed configuration/revisions remain unchanged.

Transitions: success STB05-19; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB05-19 — main

Owner; SCR06.S0; SCR06.C04 Filter logs. Choose redacted call-log export scope

Success: Filter bounded redacted records within owner scope

Error: Expired cursor restarts read, not replay of historical calls

Cancel: Cancel this bounded search; retain query draft and committed view, no remote mutation.

Transitions: success STB05-20; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB05-20 — main

Owner; SCR06.S0; SCR06.C02 Export selected scope. Request exact snapshot export approval

Success: Prepare exact redacted export snapshot then SCR09 approve snapshot ID/hash/scope

Error: Auth/quota failure creates no document; a changed selection needs new approval

Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.

Transitions: success STB05-21; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB05-21 — main

Owner; SCR09.S0; SCR09.C01 Authenticate and approve. Approve only sealed export snapshot

Success: Invoke fresh platform strong biometric/device credential prompt after input/capture quiescence

Error: Expiry/CAS change/lockout/no enrollment/drain failure denies; only original exact approved command may commit

Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.

Transitions: success STB05-22; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB05-22 — main

Owner; SCR06.S4; SCR06.C05 Choose export document. Create-document picker writes approved snapshot

Success: Write the approved sealed export through Android create-document

Error: Picker cancel/partial I/O releases snapshot and reports written/unknown bytes; no automatic retry

Cancel: Picker cancel releases transient URI grant and pending unactivated snapshot/stage. If a write already began, show partial/unknown destination state.

Transitions: success STB05-25; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB05-23 — cancel export

Owner; SCR06.S4; SCR06.C07 Cancel export. Picker-cancel alternative

Success: Release snapshot; no automatic reuse of approval

Error: Already-written document is not silently deleted; partial write is explicit

Cancel: This action is the explicit cancellation/discard. Report its recorded disposition; do not imply reversal of an already committed external effect.

Transitions: success END_WITH_RECORDED_ALTERNATIVE; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB05-24 — export I/O failure

Owner; SCR06.S5; SCR06.C01 Inspect record. Partial-write branch

Success: Show partial/unknown bytes; never silently delete destination

Error: Missing optional payload says not logged, never reconstructs it

Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.

Transitions: success END_WITH_RECORDED_ALTERNATIVE; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB05-25 — main

Owner; SCR06.S0; SCR06.C06 Edit retention quotas. Edit logging/retention quotas

Success: Same per-client client_id/logging_revision editor; global capacity separately uses bridge_config row_version

Error: Invalid/low capacity rejects; mandatory audit failure closes new dispatch

Cancel: Discard only unsaved fields; committed configuration/revisions remain unchanged.

Transitions: success END; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB05-26 — audit durability failure

Owner; SCR06.S3; SCR06.C01 Inspect record. Mandatory audit reserve exhausted branch

Success: New dispatch fail-closed; dashboard SCR01.S6 shows runtime Error with separate actual MCP health

Error: Missing optional payload says not logged, never reconstructs it

Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.

Transitions: success END_WITH_RECORDED_ALTERNATIVE; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB05-27 — runtime error alternative

Owner; SCR01.S6; SCR01.C04 Open health details. Inspect runtime admission failure on dashboard

Success: Mandatory audit/store/backend consistency failure closes new device dispatch; listener health remains independently observed.

Error: Missing facet is unknown, never inferred from another ready facet

Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.

Transitions: success END_WITH_RECORDED_ALTERNATIVE; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

## STB06 — Inbound ARC listener, guarded roots and owner isolation

![Storyboard panels](screens/STB06.svg)

External events use UI observation anchors only, not remote button presses. Exact local owner approval remains separate. Self-ADB/interest activation does not grant any MCP capability. Branch labels are alternatives, not automatic side-effect retries.

### STB06-1 — main

Owner; SCR08.S5; SCR08.C07 Edit inbound ARC listener. Draft inbound listener and allowed ordinary root

Success: Edit inbound ARC listener/root and per-client entry-point action grants

Error: Protected roots/secret surfaces not selectable; remote self-approval impossible

Cancel: Discard only unsaved fields; committed configuration/revisions remain unchanged.

Transitions: success STB06-2; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB06-2 — main

Owner; SCR08.S0; SCR08.C01 Save with revision. Request protected enable/save

Success: SCR09 approves exact settings draft; CAS save desired config and applicable lifecycle changes

Error: Conflict rejects entire draft; bind failure keeps desired vs actual separate

Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.

Transitions: success STB06-3; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB06-3 — main

Owner; SCR09.S0; SCR09.C01 Authenticate and approve. Approve exact ARC configuration

Success: Bind only after commit; listener actual state independent from desired enabled

Error: Expiry/CAS change/lockout/no enrollment/drain failure denies; only original exact approved command may commit

Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.

Transitions: success STB06-4; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB06-4 — main

Compatible ARC client; SCR08.S6; SCR08.C09 Inspect inbound controllers. Compatible client authenticates to inbound listener

Success: expected_kind=arc; bounded controller/session list; retained wire1.0

Error: No ADB or outbound status inferred from this page

Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.

Transitions: success STB06-5; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB06-5 — main

Compatible ARC client; SCR08.S6; SCR08.C09 Inspect inbound controllers. Inbound authorized redacted watch on management_readonly root

Success: Finite authorized observation/watch only; management click/focus/property/input returns failed CommandResult

Error: Denied root/policy/auth closes or rejects with the documented retained ARC status; no management action is dispatched

Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.

Transitions: success STB06-7; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB06-6 — quiescence failed

Owner; SCR09.S3; SCR09.C02 Deny / dismiss. Owner challenge cannot isolate bridge input branch

Success: No OS prompt; bridge capture/input stays blocked

Error: Back/system dismiss is the same denial; no side effect, no hidden next approval

Cancel: This action is the explicit cancellation/discard. Report its recorded disposition; do not imply reversal of an already committed external effect.

Transitions: success END_WITH_RECORDED_ALTERNATIVE; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB06-7 — main

Owner; SCR09.S0; SCR09.C04 Inspect exact challenge. Normal protected challenge: capture/input suspended

Success: No secret/approval surface enters any frame/tree/log queue

Error: Changed plan terminates review; owner must open a newly issued challenge

Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.

Transitions: success STB06-8; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB06-8 — main

Owner; SCR09.S0; SCR09.C02 Deny / dismiss. Deny challenge and resume ordinary admission safely

Success: No configuration mutation or remote approval

Error: Back/system dismiss is the same denial; no side effect, no hidden next approval

Cancel: This action is the explicit cancellation/discard. Report its recorded disposition; do not imply reversal of an already committed external effect.

Transitions: success STB06-9; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB06-9 — main

Owner; SCR08.S6; SCR08.C08 Rotate inbound ARC credential. Rotate inbound ARC credential through exact review

Success: SCR09 approves exact inbound ARC credential rotation

Error: Commit revokes old verifier/epoch; bind/session failures do not restore old bearer

Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.

Transitions: success STB06-10; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB06-10 — main

Owner; SCR09.S0; SCR09.C01 Authenticate and approve. Approve rotation

Success: Old streams/input leases close at epoch barrier

Error: Expiry/CAS change/lockout/no enrollment/drain failure denies; only original exact approved command may commit

Cancel: Before exact commit, Deny/Back closes this challenge without applying it. After commit, no undo is implied; any reversal is a new exact owner command.

Transitions: success STB06-11; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB06-11 — main

Compatible ARC client; SCR08.S6; SCR08.C09 Inspect inbound controllers. Client closes watch socket or receives revoke

Success: No Cancel8 command and no guaranteed terminal on peer close

Error: No ADB or outbound status inferred from this page

Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.

Transitions: success END; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

### STB06-12 — listener disabled

System/backend event; SCR08.S5; SCR08.C09 Inspect inbound controllers. Disabled-result observation

Success: No listener/socket/controller root; no phantom Ready

Error: No ADB or outbound status inferred from this page

Cancel: Close the read/detail. Cancelled read does not alter the represented resource or backend registration.

Transitions: success END_WITH_RECORDED_ALTERNATIVE; error END_WITH_TYPED_ERROR; cancel END_WITH_RECORDED_DISPOSITION.

