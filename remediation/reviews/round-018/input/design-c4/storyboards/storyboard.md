---
marp: true
theme: default
paginate: true
size: 16:9
style: |
  section { font-size: 25px; color: #20354a; }
  h1 { font-size: 36px; }
---

# Android MCP UI storyboards

Kotlin Compose screen/state specifications.

Design only — no application or device operations.

---

# STB01 · 1. Describe the job

![bg right:65% contain](../docs/screens/SCR04.svg)

SCR04 · Read bounded group summaries

**Alternate:** Denied permission stays visible

---

# STB01 · 2. Select coherent group

![bg right:65% contain](../docs/screens/SCR04.svg)

SCR04 · CAS activate within byte budget

**Alternate:** Conflict/budget error: nothing partial

---

# STB01 · 3. Register and consume

![bg right:65% contain](../docs/screens/SCR05.svg)

SCR05 · One final response, then journal pages

**Alternate:** Expired cursor: show retention boundary

---

# STB01 · 4. Cancel explicitly

![bg right:65% contain](../docs/screens/SCR05.svg)

SCR05 · Stop delivery; verify unregister

**Alternate:** Cleanup unknown remains visible

---

# STB02 · 1. Pick target profile

![bg right:65% contain](../docs/screens/SCR07.svg)

SCR07 · Check arc-protobuf-v1 and private token

**Alternate:** Unknown protocol: no connect

---

# STB02 · 2. Authenticate and probe

![bg right:65% contain](../docs/screens/SCR07.svg)

SCR07 · Version 1.0 and matching request ID

**Alternate:** Wrong token: no tree/frame disclosure

---

# STB02 · 3. Inspect ordinary root

![bg right:65% contain](../docs/screens/SCR07.svg)

SCR07 · Generation-pinned frame/DIP actions

**Alternate:** Protected owner root: refuse input

---

# STB02 · 4. Disconnect

![bg right:65% contain](../docs/screens/SCR07.svg)

SCR07 · Close socket, release pressed keys

**Alternate:** Never retry an uncertain click

---

# STB03 · 1. Edit draft settings

![bg right:65% contain](../docs/screens/SCR08.svg)

SCR08 · Preview exact change and revision

**Alternate:** Cancel: no committed mutation

---

# STB03 · 2. Authenticate locally

![bg right:65% contain](../docs/screens/SCR09.svg)

SCR09 · Review principal/scope/expiry

**Alternate:** Dismiss/timeout: deny

---

# STB03 · 3. Commit with CAS

![bg right:65% contain](../docs/screens/SCR08.svg)

SCR08 · Service transaction then state update

**Alternate:** Stale revision: reload, no overwrite

---

# STB03 · 4. Observe actual status

![bg right:65% contain](../docs/screens/SCR10.svg)

SCR10 · Running only when listener is ready

**Alternate:** Start denied: action required, no bypass
