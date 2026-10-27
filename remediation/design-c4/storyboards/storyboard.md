---
marp: true
theme: default
paginate: true
size: 16:9
style: |
  section { font-size:22px; color:#20354a; }
  h1 { font-size:30px; }
---

# Kotlin Compose UI storyboards

Owner, external MCP client and Android backend are explicit actors. External event control IDs are observation anchors, never remote presses. Happy, error and cancellation branches are distinct steps.

---

# STB01 · 1. External android.groups.discover; owner preview updates only

![bg right:58% contain](../docs/screens/SCR04.svg)

**External MCP client** · `SCR04.S0`

Observation: SCR04.C06 Search group summaries

**Success:** Ranked summaries bounded by max_bytes; no authority granted

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB01 · 2. External describe then set_active CAS

![bg right:58% contain](../docs/screens/SCR04.svg)

**External MCP client** · `SCR04.S0`

Observation: SCR04.C07 Describe selected group

**Success:** Atomic client group view; portable binding.invoke remains

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB01 · 3. External binding.invoke with callback, resources/subscribe, bounded read

![bg right:58% contain](../docs/screens/SCR05.svg)

**External MCP client** · `SCR05.S0`

Observation: SCR05.C06 Refresh status

**Success:** Finite handle then URI-only hint and journal read; callback list updates

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB01 · 4. Owner inspects exact subscription and cleanup phase

![bg right:58% contain](../docs/screens/SCR05.svg)

**Owner** · `SCR05.S0`

Action: SCR05.C01 Inspect bounded status

**Success:** Show exact resulting state; no implicit permission change

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB01 · 5. Open exact administrative cancellation review

![bg right:58% contain](../docs/screens/SCR05.svg)

**Owner** · `SCR05.S0`

Action: SCR05.C02 Cancel via owner review

**Success:** SCR09 challenge binds selected subscription/revision

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB01 · 6. Confirm this administrative cancellation

![bg right:58% contain](../docs/screens/SCR09.svg)

**Owner** · `SCR09.S0`

Action: SCR09.C01 Authenticate and approve

**Success:** Delivery closed and recorded cleanup issued once

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB01 · 7. Read terminal event/status

![bg right:58% contain](../docs/screens/SCR05.svg)

**Owner** · `SCR05.S4`

Action: SCR05.C06 Refresh status

**Success:** Client learns cancellation through callback.status or retained terminal journal; cleanup confirmed or unknown

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB01 · 8. Inspect cleanup uncertainty without replay

![bg right:58% contain](../docs/screens/SCR05.svg)

**Owner** · `SCR05.S3`

Action: SCR05.C06 Refresh status

**Success:** Show exact resulting state; no implicit permission change

**Error:** Uncertain cleanup keeps phase=cleanup and delivery closed

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB01 · 9. Cancellation alternative before commit

![bg right:58% contain](../docs/screens/SCR09.svg)

**Owner** · `SCR09.S0`

Action: SCR09.C02 Deny / dismiss

**Success:** Return SCR05; registration unchanged

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB02 · 1. Choose exact outbound compatible target profile

![bg right:58% contain](../docs/screens/SCR07.svg)

**Owner** · `SCR07.S0`

Action: SCR07.C05 Select target profile

**Success:** Show exact resulting state; no implicit permission change

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB02 · 2. Connect and request capabilities

![bg right:58% contain](../docs/screens/SCR07.svg)

**Owner** · `SCR07.S0`

Action: SCR07.C01 Connect selected target

**Success:** First response ID/version/status checked; no handshake

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB02 · 3. Inspect frame viewport and selected tree node

![bg right:58% contain](../docs/screens/SCR07.svg)

**Owner** · `SCR07.S4`

Action: SCR07.C06 Inspect tree or refresh frame

**Success:** Current-root dimensions/density visible; stale node rejected

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB02 · 4. Press, move and release across unary requests

![bg right:58% contain](../docs/screens/SCR07.svg)

**Owner** · `SCR07.S4`

Action: SCR07.C07 Pointer or wheel input

**Success:** Principal/root lease serializes current-coordinate input; no wire generation invented

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB02 · 5. Send key/text using approved named mapping

![bg right:58% contain](../docs/screens/SCR07.svg)

**Owner** · `SCR07.S4`

Action: SCR07.C04 Send typed input; disabled unless connected capability/policy permits

**Success:** Show exact resulting state; no implicit permission change

**Error:** Unknown key or failed CommandResult is displayed even with OK envelope

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB02 · 6. Disconnect controller

![bg right:58% contain](../docs/screens/SCR07.svg)

**Owner** · `SCR07.S4`

Action: SCR07.C02 Disconnect / cancel

**Success:** Best-effort releases then watch socket close; idle lease bound covers network failure

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB02 · 7. Inspect wrong-token failure

![bg right:58% contain](../docs/screens/SCR07.svg)

**Owner** · `SCR07.S2`

Action: SCR07.C01 Connect selected target

**Success:** Show exact resulting state; no implicit permission change

**Error:** No tree/frame data or enabled input controls

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB02 · 8. Inspect inbound listener separately from outbound controller

![bg right:58% contain](../docs/screens/SCR08.svg)

**Owner** · `SCR08.S6`

Action: SCR08.C09 Inspect inbound controllers

**Success:** Bound loopback endpoint, exposed ordinary root and incoming principals shown

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB03 · 1. Edit exact boot/listener draft

![bg right:58% contain](../docs/screens/SCR08.svg)

**Owner** · `SCR08.S0`

Action: SCR08.C05 Edit startup and MCP listener

**Success:** Show exact resulting state; no implicit permission change

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB03 · 2. Discard alternative

![bg right:58% contain](../docs/screens/SCR08.svg)

**Owner** · `SCR08.S0`

Action: SCR08.C04 Discard settings draft

**Success:** No committed setting changes

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB03 · 3. Save reviewed exact draft

![bg right:58% contain](../docs/screens/SCR08.svg)

**Owner** · `SCR08.S0`

Action: SCR08.C01 Save with revision

**Success:** SCR09 shows exact values/revisions

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB03 · 4. Fresh protected proof and CAS

![bg right:58% contain](../docs/screens/SCR09.svg)

**Owner** · `SCR09.S0`

Action: SCR09.C01 Authenticate and approve

**Success:** Return SCR08 committed revision; queue next pending challenge

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB03 · 5. Navigate to dashboard

![bg right:58% contain](../docs/screens/SCR08.svg)

**Owner** · `SCR08.S0`

Action: SCR08.C03 Open navigation

**Success:** Show exact resulting state; no implicit permission change

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB03 · 6. Request foreground start

![bg right:58% contain](../docs/screens/SCR01.svg)

**Owner** · `SCR01.S3`

Action: SCR01.C01 Start / Stop service

**Success:** Starting, admission closed, then actual ready

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB03 · 7. Inspect healthy tuple

![bg right:58% contain](../docs/screens/SCR01.svg)

**Owner** · `SCR01.S0`

Action: SCR01.C04 Open health details

**Success:** MCP ready is separate from ADB paired-offline

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB03 · 8. Failure branch after denied/failed start

![bg right:58% contain](../docs/screens/SCR10.svg)

**Owner** · `SCR10.S0`

Action: SCR10.C05 Inspect failure reason

**Success:** Show exact resulting state; no implicit permission change

**Error:** Error badge, no Running; exact recovery reason

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB03 · 9. Explicit retry after prerequisite fixed

![bg right:58% contain](../docs/screens/SCR10.svg)

**Owner** · `SCR10.S0`

Action: SCR10.C01 Review prerequisites

**Success:** No permission or settings bypass; Starting reevaluated

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB03 · 10. Deny/cancel configuration review alternative

![bg right:58% contain](../docs/screens/SCR09.svg)

**Owner** · `SCR09.S0`

Action: SCR09.C02 Deny / dismiss

**Success:** Return SCR08 previous committed revision

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB04 · 1. Open Add client, including empty-client state

![bg right:58% contain](../docs/screens/SCR02.svg)

**Owner** · `SCR02.S1`

Action: SCR02.C04 Add client

**Success:** Show exact resulting state; no implicit permission change

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB04 · 2. Select helper flow and explicit TLS trust

![bg right:58% contain](../docs/screens/SCR11.svg)

**Owner** · `SCR11.S0`

Action: SCR11.C04 Choose pairing flow

**Success:** Show exact resulting state; no implicit permission change

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB04 · 3. Request one-time scoped challenge

![bg right:58% contain](../docs/screens/SCR11.svg)

**Owner** · `SCR11.S0`

Action: SCR11.C01 Generate pairing challenge

**Success:** Show exact resulting state; no implicit permission change

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB04 · 4. Approve pairing window/attempt

![bg right:58% contain](../docs/screens/SCR09.svg)

**Owner** · `SCR09.S0`

Action: SCR09.C01 Authenticate and approve

**Success:** Nonce available for exact client; no principal yet

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB04 · 5. Client presents pinned nonce/key/Origin proof

![bg right:58% contain](../docs/screens/SCR11.svg)

**External MCP client** · `SCR11.S4`

Observation: SCR11.C05 Inspect submitted client proof

**Success:** Service validates proof; owner sees exact pending fingerprint

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB04 · 6. Review exact submitted client commit

![bg right:58% contain](../docs/screens/SCR11.svg)

**Owner** · `SCR11.S4`

Action: SCR11.C06 Review exact client commit

**Success:** Show exact resulting state; no implicit permission change

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB04 · 7. Confirm this client identity/revisions

![bg right:58% contain](../docs/screens/SCR09.svg)

**Owner** · `SCR09.S0`

Action: SCR09.C01 Authenticate and approve

**Success:** Return SCR11 Paired, default Deny

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB04 · 8. Navigate to clients

![bg right:58% contain](../docs/screens/SCR11.svg)

**Owner** · `SCR11.S5`

Action: SCR11.C03 Open navigation

**Success:** Show exact resulting state; no implicit permission change

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB04 · 9. Edit exact binding/schema/target rule draft

![bg right:58% contain](../docs/screens/SCR02.svg)

**Owner** · `SCR02.S0`

Action: SCR02.C01 Edit exact grants

**Success:** Show exact resulting state; no implicit permission change

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB04 · 10. Approve grant CAS

![bg right:58% contain](../docs/screens/SCR09.svg)

**Owner** · `SCR09.S0`

Action: SCR09.C01 Authenticate and approve

**Success:** Return SCR02 Allow/Ask/Deny committed as reviewed

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB04 · 11. Request credential revoke

![bg right:58% contain](../docs/screens/SCR02.svg)

**Owner** · `SCR02.S0`

Action: SCR02.C02 Revoke credential

**Success:** Show exact resulting state; no implicit permission change

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB04 · 12. Confirm exact revoke

![bg right:58% contain](../docs/screens/SCR09.svg)

**Owner** · `SCR09.S0`

Action: SCR09.C01 Authenticate and approve

**Success:** Epoch increments, streams/input close, pending calls recheck

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB04 · 13. Cancel attempt alternative

![bg right:58% contain](../docs/screens/SCR11.svg)

**Owner** · `SCR11.S3`

Action: SCR11.C02 Cancel pairing

**Success:** No client committed

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB04 · 14. Expired challenge alternative

![bg right:58% contain](../docs/screens/SCR11.svg)

**Owner** · `SCR11.S1`

Action: SCR11.C01 Generate pairing challenge

**Success:** Show exact resulting state; no implicit permission change

**Error:** New nonce required; no reuse

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB05 · 1. Search binding/type evidence including unknown

![bg right:58% contain](../docs/screens/SCR03.svg)

**Owner** · `SCR03.S0`

Action: SCR03.C04 Search inventory

**Success:** Show exact resulting state; no implicit permission change

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB05 · 2. Inspect selected evidence and runtime-versus-grant distinction

![bg right:58% contain](../docs/screens/SCR03.svg)

**Owner** · `SCR03.S0`

Action: SCR03.C01 Inspect evidence

**Success:** Show exact resulting state; no implicit permission change

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB05 · 3. Edit local override draft

![bg right:58% contain](../docs/screens/SCR03.svg)

**Owner** · `SCR03.S0`

Action: SCR03.C02 Edit local override

**Success:** Show exact resulting state; no implicit permission change

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB05 · 4. Preview exact diff and dependency/CAS effects

![bg right:58% contain](../docs/screens/SCR03.svg)

**Owner** · `SCR03.S4`

Action: SCR03.C07 Preview override diff

**Success:** Show exact resulting state; no implicit permission change

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB05 · 5. Approve exact override publication

![bg right:58% contain](../docs/screens/SCR09.svg)

**Owner** · `SCR09.S0`

Action: SCR09.C01 Authenticate and approve

**Success:** Catalog epoch invalidates affected device/group views

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB05 · 6. Select clients/time/outcome and redacted snapshot

![bg right:58% contain](../docs/screens/SCR06.svg)

**Owner** · `SCR06.S0`

Action: SCR06.C04 Filter logs

**Success:** Show exact resulting state; no implicit permission change

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB05 · 7. Request selected-scope export

![bg right:58% contain](../docs/screens/SCR06.svg)

**Owner** · `SCR06.S0`

Action: SCR06.C02 Export selected scope

**Success:** SCR09 shows immutable redacted snapshot and limits

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB05 · 8. Approve exact export snapshot

![bg right:58% contain](../docs/screens/SCR09.svg)

**Owner** · `SCR09.S0`

Action: SCR09.C01 Authenticate and approve

**Success:** Return SCR06 ready for create-document picker

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB05 · 9. Choose Android SAF destination

![bg right:58% contain](../docs/screens/SCR06.svg)

**Owner** · `SCR06.S4`

Action: SCR06.C05 Choose export document

**Success:** Write only approved redacted snapshot

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB05 · 10. Cancel picker alternative

![bg right:58% contain](../docs/screens/SCR06.svg)

**Owner** · `SCR06.S4`

Action: SCR06.C07 Cancel export

**Success:** No document written

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB05 · 11. Quota/audit failure branch

![bg right:58% contain](../docs/screens/SCR06.svg)

**Owner** · `SCR06.S3`

Action: SCR06.C01 Inspect record

**Success:** Show exact resulting state; no implicit permission change

**Error:** Mandatory audit failure closes admission; optional logs evicted first

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB05 · 12. Absent-catalog import branch

![bg right:58% contain](../docs/screens/SCR03.svg)

**Owner** · `SCR03.S0`

Action: SCR03.C06 Choose import document

**Success:** SAF copy/validation stage; protected activation review required

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB06 · 1. Enter device/user and Android-provided pairing host/port/code

![bg right:58% contain](../docs/screens/SCR08.svg)

**Owner** · `SCR08.S0`

Action: SCR08.C06 Edit same-device ADB profile

**Success:** Show exact resulting state; no implicit permission change

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB06 · 2. Request exact pair/connect command

![bg right:58% contain](../docs/screens/SCR08.svg)

**Owner** · `SCR08.S0`

Action: SCR08.C02 Review pairing

**Success:** SCR09 review; no Android debugging toggle

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB06 · 3. Authenticate exact profile and peer request

![bg right:58% contain](../docs/screens/SCR09.svg)

**Owner** · `SCR09.S0`

Action: SCR09.C01 Authenticate and approve

**Success:** Bounded native pairing/connect starts

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB06 · 4. Observe paired peer/backend status

![bg right:58% contain](../docs/screens/SCR08.svg)

**Android backend** · `SCR08.S0`

Observation: SCR08.C09 Inspect inbound controllers

**Success:** Status change is evidence, never an MCP grant

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB06 · 5. Navigate dashboard

![bg right:58% contain](../docs/screens/SCR08.svg)

**Owner** · `SCR08.S0`

Action: SCR08.C03 Open navigation

**Success:** Show exact resulting state; no implicit permission change

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB06 · 6. Inspect MCP ready versus ADB Paired offline/Connected

![bg right:58% contain](../docs/screens/SCR01.svg)

**Owner** · `SCR01.S0`

Action: SCR01.C04 Open health details

**Success:** Show exact resulting state; no implicit permission change

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB06 · 7. Wrong peer/code branch

![bg right:58% contain](../docs/screens/SCR08.svg)

**Owner** · `SCR08.S4`

Action: SCR08.C02 Review pairing

**Success:** Show exact resulting state; no implicit permission change

**Error:** Unverified peer never becomes Connected

**Cancel:** Cancel/back returns without the uncommitted effect

---

# STB06 · 8. Cancel pairing alternative

![bg right:58% contain](../docs/screens/SCR09.svg)

**Owner** · `SCR09.S0`

Action: SCR09.C02 Deny / dismiss

**Success:** No profile/key change

**Error:** Show sanitized denial/failure and retain unchanged revisions

**Cancel:** Cancel/back returns without the uncommitted effect
