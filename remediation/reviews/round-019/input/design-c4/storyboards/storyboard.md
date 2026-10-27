---
marp: true
theme: default
paginate: true
size: 16:9
style: |
  section { font-size: 24px; color: #20354a; }
  h1 { font-size: 34px; }
---

# Kotlin Compose UI storyboards

Owner controls and explicit external-agent triggers. SVG screen/state/control IDs are authoritative.

Design only; no app or device operations.

---

# STB01 · 1. Inspect group summary

![bg right:62% contain](../docs/screens/SCR04.svg)

**Owner** · `SCR04.S0` · `SCR04.C01`

**Result:** Selected client/session shown

**Error/cancel:** Denied grant remains visible; no UI activation

---

# STB01 · 2. Inspect subscription status

![bg right:62% contain](../docs/screens/SCR05.svg)

**Owner** · `SCR05.S0` · `SCR05.C01`

**Result:** Agent registered via its own MCP session

**Error/cancel:** No registration: SCR05.S1; never impersonate client

---

# STB01 · 3. Approve exact admin cancellation

![bg right:62% contain](../docs/screens/SCR09.svg)

**Owner** · `SCR09.S0` · `SCR09.C01`

**Result:** Nonce/revision-bound cancellation admitted

**Error/cancel:** Dismiss/expiry: no cancellation command

---

# STB01 · 4. Inspect closed delivery

![bg right:62% contain](../docs/screens/SCR05.svg)

**Owner** · `SCR05.S4` · `SCR05.C01`

**Result:** Cleanup confirmed or explicitly unknown

**Error/cancel:** Backend death: payload hidden; retain cleanup status

---

# STB02 · 1. Connect selected target

![bg right:62% contain](../docs/screens/SCR07.svg)

**Owner** · `SCR07.S0` · `SCR07.C01`

**Result:** First envelope checked; no handshake

**Error/cancel:** Wrong token/profile: no target data

---

# STB02 · 2. Observe capabilities/frame

![bg right:62% contain](../docs/screens/SCR07.svg)

**Owner** · `SCR07.S4` · `SCR07.C01`

**Result:** Verified target generation and DIP scale

**Error/cancel:** Protected/unknown root: no action

---

# STB02 · 3. Request admitted control

![bg right:62% contain](../docs/screens/SCR07.svg)

**Owner** · `SCR07.S4` · `SCR07.C04`

**Result:** Exact target policy checked before dispatch

**Error/cancel:** OK envelope plus failed CommandResult is still denied

---

# STB02 · 4. Disconnect/cancel

![bg right:62% contain](../docs/screens/SCR07.svg)

**Owner** · `SCR07.S4` · `SCR07.C02`

**Result:** Watch socket closes; pressed state released

**Error/cancel:** Uncertain click is not blindly replayed

---

# STB03 · 1. Save exact draft revision

![bg right:62% contain](../docs/screens/SCR08.svg)

**Owner** · `SCR08.S0` · `SCR08.C01`

**Result:** Protected change opens SCR09

**Error/cancel:** Cancel: no committed mutation

---

# STB03 · 2. Authenticate exact command

![bg right:62% contain](../docs/screens/SCR09.svg)

**Owner** · `SCR09.S0` · `SCR09.C01`

**Result:** Service commits CAS then publishes state

**Error/cancel:** Dismiss/timeout: deny; conflict: reload SCR08

---

# STB03 · 3. Observe listener readiness

![bg right:62% contain](../docs/screens/SCR01.svg)

**Owner** · `SCR01.S0` · `SCR01.C01`

**Result:** Starting becomes Running only when ready

**Error/cancel:** Start denied: navigate to SCR10

---

# STB03 · 4. Return to dashboard

![bg right:62% contain](../docs/screens/SCR10.svg)

**Owner** · `SCR10.S0` · `SCR10.C02`

**Result:** Show actual MCP/ADB/view states

**Error/cancel:** No permitted start: remain action-required

---

# STB04 · 1. Request pairing challenge

![bg right:62% contain](../docs/screens/SCR11.svg)

**Owner** · `SCR11.S0` · `SCR11.C01`

**Result:** Review exact identity/origin in SCR09

**Error/cancel:** Cancel/wrong proof: no principal committed

---

# STB04 · 2. Approve scoped pairing

![bg right:62% contain](../docs/screens/SCR09.svg)

**Owner** · `SCR09.S0` · `SCR09.C01`

**Result:** One-time challenge follows pinned pairing contract

**Error/cancel:** Expired nonce or changed origin: reject

---

# STB04 · 3. Edit exact client grants

![bg right:62% contain](../docs/screens/SCR02.svg)

**Owner** · `SCR02.S0` · `SCR02.C01`

**Result:** Draft rule/scope opens protected review

**Error/cancel:** Denied scope stays denied until approved commit

---

# STB04 · 4. Request credential revocation

![bg right:62% contain](../docs/screens/SCR02.svg)

**Owner** · `SCR02.S0` · `SCR02.C02`

**Result:** Protected review then epoch invalidation

**Error/cancel:** Changed revision: no silent overwrite

---

# STB05 · 1. Draft local override

![bg right:62% contain](../docs/screens/SCR03.svg)

**Owner** · `SCR03.S0` · `SCR03.C02`

**Result:** Immutable source evidence stays unchanged

**Error/cancel:** Schema/identity failure: show reason

---

# STB05 · 2. Approve pinned override

![bg right:62% contain](../docs/screens/SCR09.svg)

**Owner** · `SCR09.S0` · `SCR09.C01`

**Result:** CAS commits local overlay and history

**Error/cancel:** Stale base/revision: conflict, reload

---

# STB05 · 3. Preview selected export scope

![bg right:62% contain](../docs/screens/SCR06.svg)

**Owner** · `SCR06.S0` · `SCR06.C02`

**Result:** Fresh protected review before export

**Error/cancel:** Denied auth: no export data

---

# STB05 · 4. Inspect export audit record

![bg right:62% contain](../docs/screens/SCR06.svg)

**Owner** · `SCR06.S0` · `SCR06.C01`

**Result:** Redacted selected scope or explicit error

**Error/cancel:** Cancel/disk failure: no false success
