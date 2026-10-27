# Android MCP bridge design portable edition

Open **Android MCP Bridge Design.md** in a Markdown viewer with local SVG support. Keep this folder structure intact: its two diagram links are relative to `assets/`. If a viewer blocks SVG, open the SVG directly in a browser. The Word edition embeds rasterized versions of the same diagrams for office compatibility.

This is a design document, not an installed server, device connection or application implementation.

## Edit and render diagrams

Editable Mermaid sources are in `sources/`. Both diagrams were rendered with the official npm package **pele 1.10.0**. The lock file pins the distribution and integrity hash. With Node.js 20 or newer:

```sh
npm ci --ignore-scripts
npm run render
```

These commands regenerate local SVGs only. No hosted service, CDN, external images or private URLs are needed to view the included diagrams. Font choice is DejaVu Sans; install that font for the closest editable regeneration. CSS theme variables are resolved to standalone values. Links and images are disabled in the renderer and the output is checked for active/external content.

The document also retains Mermaid source blocks after each SVG image, so editing does not depend on a specific Markdown plugin. If you edit a source file, synchronize its fenced block in the Markdown after rendering. `render-manifest.json` records the verified renderer and options; `checksums.sha256` identifies the shipped files.

Official renderer documentation: [API](https://pele.run/api), [compatibility](https://pele.run/compatibility), [security](https://pele.run/security). Pele licensing is included in `PELE-LICENSE.txt`.


##27October2026revision

Startup device-resolved bindings and types, coherent type dependency resolution, generic Binder transaction engine/codecs and atomic invalidation/refresh are now specified. Four Mermaid sources and their Pele1.10.0 SVG/PNG renders are included. DOCX/PDF are synchronized from the editable Markdown. The offline resolver reference passes13fixtures; Androidruntime is not deployed and this requirement awaits a fresh independent review. Full inventory and unknown/unavailable reasons remain; availability never grants permission.

Renderer remains locked toPele1.10.0. The supplied package/lock files describe that input. No package installer or existing software modification was performed in producing this revision. SVGs and office documents can be viewed without running the renderer.


## Current reviewed-remediation revision (27October2026, round011candidate)

Earlier revision text above is retained history. The current Markdown/DOCX/PDF specifies the full producer-to-Entry-to-inventory-to-resolver-to-SQLite chain, epoch-bound facts, graph digests, strict source/version/type evidence, complete atomic views and separate wire/policy authority. DeviceResolution2.0.0 explicitly supersedes the unimplemented1.2draft; OpenAPI1.3.0. The accompanying catalog/reference candidate passes190tests; no Android/HTTP deployment or device observation is claimed. Round010 was NOT_READY overall92/80; the corrected frozen011candidate is under independent review. No98/98agreement is claimed. Embedded Mermaid now matches its editable source files; four original-versioned sources and Pele1.10.0renders remain. The archive contains only design/render assets and QA metadata, not the separate pending validation-evidence ZIP.


## Current corrected revision (27October2026, round012candidate)

Earlier revision descriptions above are retained history. Current Markdown/DOCX/PDF specify inventory2.1 point evidence, resolved3 transitive schema hashes, digest-pinned complete activation, effective registry+local overrides and current document pins, semantic contract3, canonical policy and separate OS/client/wire authority.245normal and245optimized offline tests pass. The full historical008 scale checkpoint and its memory/time limits are disclosed; no Android performance or deployment is claimed. Latest complete Claude011 verdict was NOT_READY overall93/84; corrected012 is running. No98/98agreement is claimed. Four editable Mermaid/Pele1.10.0 assets are retained. This archive includes design/render assets and design QA metadata only; it excludes catalog/source/review evidence and the separate pending evidence ZIP.


## Callback and functional tool-group design update (27 October 2026)

This revision adds exhaustive evidence-qualified callback roles, finite registration with bounded resource-journal streaming, cancellation and synchronous local reply policies, plus small functional groups, job discovery, session activation, byte budgets and a static-host generic-invocation fallback. Seven editable Mermaid sources, Pele1.10.0 SVGs and rendered PNG assets are included. Existing startup resolution, Binder/policy, Avalonia12, SQLite, ADB, RemoteControl and lifecycle requirements are retained. Current offline regression suite passes271 normal and271 optimized tests; callback/group lifecycle reference fixtures and new independent review are pending. Latest complete Claude012 is NOT_READY overall93/80, design94/95 contracts95/94; no98/98agreement is claimed. Only design/render assets and design QA are packaged; no source/catalog/review-evidence bundle.
