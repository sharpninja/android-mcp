import pathlib,re,json,hashlib,datetime,shutil
R=pathlib.Path(__file__).resolve().parent;P=R/'design-c4';D=P/'docs';D.mkdir(exist_ok=True)
s=(R/'design/Android MCP Bridge Design.md').read_text()
# Retain the previous delivered/frozen revisions separately; canonical current package contains only current modeling sources.
s=re.sub(r'```mermaid\n.*?```\n?', '',s,flags=re.S)
s=s.replace('The authoritative editable Mermaid sources for all four diagrams are in design/sources and the portable bundle. The two inline source blocks later in this document follow the same files.','The authoritative C4 model is architecture/workspace.dsl. Code-level detail uses the linked local Structurizr image views and inline PlantUML definitions in architecture/code-diagrams.md.')
s=s.replace('[[S25] Pele renderer API](https://pele.run/api)','[[S25] Structurizr code-level image views](https://docs.structurizr.com/example/code-diagram)')
s=s.replace('Diagrams are rendered locally with Pele 1.10.0, pinned to the verified npm distribution. The portable bundle includes SVGs and editable Mermaid sources. Links and external images are disabled; colors are resolved into standalone SVG values, so no CDN or external stylesheet is needed.','Diagrams are generated locally through Gradle from one Structurizr C4 workspace. The portable package includes editable DSL, inline PlantUML code-level definitions, SVG exports and build pins. No public diagram renderer or cloud upload is used.')
s=s.replace('Architecture diagram rendered with Pele','C4 container diagram').replace('Request and approval sequence rendered with Pele','C4 invocation dynamic view')
s=s.replace('Existing section diagrams remain editable Mermaid; Pele 1.10.0 rendering is a separate packaging check.','Current diagrams use the shared C4 model and local Structurizr/PlantUML SVG export workflow with Gradle validation.')
mapping={'architecture':'structurizr-Containers','request-sequence':'structurizr-InvocationFlow','startup-resolution':'structurizr-Startup','device-binding-sequence':'structurizr-Startup','callback-architecture':'structurizr-ServerComponents','callback-sequence':'structurizr-CallbackFlow','tool-group-flow':'structurizr-GroupFlow','kotlin-service-ui':'structurizr-Containers'}
for old,new in mapping.items():s=s.replace('assets/'+old+'.svg','diagrams/'+new+'.svg')
# The new internal Kotlin port supersedes the original external-only integration section, retaining the source/authority boundaries in the expanded contract.
a=s.index('# 17 Optional Avalonia RemoteControl integration');b=s.index('# 18',a)
s=s[:a]+'# 17 Internal Kotlin ARC engine and compatible external targets\n\nPort the ARC protocol/runtime behavior internally to Kotlin as specified in the detailed port contract below. The Compose target adapter and controller session UI use the same authorized services as MCP. Generic phone/app control remains the separate owner-paired ADB backend. Keep the retained arc-protobuf-v1 framing, protobuf tags and version1.0; do not embed a managed Avalonia server in the bridge. Protected local owner grants/approvals are excluded from every remote tree/frame/input path. The original external-only integration proposal is superseded; compatible external ARC targets remain supported by the planned Kotlin client adapter.\n\n'+s[b:]
s=s.rstrip()+'\n\n'+(R/'design/internal-kotlin-arc-port.md').read_text()
s=s.replace('assets/server-core-classes.svg','diagrams/server-core-classes.svg').replace('assets/server-execution-classes.svg','diagrams/server-execution-classes.svg').replace('assets/arc-ui-classes.svg','diagrams/arc-ui-classes.svg')
s+='\n\n![ARC controller class detail](diagrams/arc-controller-classes.svg)\n\n'
s+='''## C4 architecture source and documentation build

Markdown is canonical. SVG exports remain editable vector text with relative links; no raster replacement, Word dependency or public publishing is required. Current architecture uses C4 context, container, component, dynamic and deployment views from one Structurizr DSL model. A C4 container here represents the one Android APK/process and its datastore, not an invented separately deployed UI service. UI and server modules appear as components inside that APK. Code-level class detail uses Structurizr's documented image-view mechanism: editable inline PlantUML definitions in architecture/code-diagrams.md are rendered locally, then image views scope them to the corresponding C4 components. UML composition/multiplicity notation is explicit; these are proposed classes, not native Structurizr class objects or implemented code.

The full view set, source and reproducible build instructions are in [the architecture workspace](../architecture/workspace.dsl), [code-level definitions](../architecture/code-diagrams.md) and [build README](../README.md). Gradle8.14.3, existing Structurizr CLI2025.11.09 image digest, Java21 and PlantUML1.2025.10 are pinned. The local image is reused; no host software/settings are changed. The task-local bootstrap verifies the Gradle distribution SHA and records all tooling hashes. Once dependencies are cached, rendering runs with no network and no open listener. Native C4 definitions are exported by Structurizr to PlantUML, then rendered locally to SVG. Class detail is rendered locally and embedded as Structurizr image views. This is an export pipeline with its own layout, not a claim of pixel-identical browser-renderer output.

Real Gradle targets are validateArchitecture, generateArchitectureDiagrams, generateUiAssets, renderStoryboards, verifyDesignDocs and packageDesignDocs. They enforce input/output dependencies, complete expected view names, invalid-model failure, code-view presence, vector-text SVG output and Markdown link integrity. The package uses deterministic ZIP order/timestamps and includes its DSL, editable code blocks, vector screens/storyboards, Gradle wrapper and Kotlin DSL build scripts. Gradle wrapper distribution verification is mandatory; a clean machine needs the documented dependency bootstrap before offline use. Cached offline builds fail explicitly if dependencies are missing rather than silently accessing an online renderer.

![C4 system context](diagrams/structurizr-Context.svg)

![C4 Kotlin server components](diagrams/structurizr-ServerComponents.svg)

![C4 ARC and Compose component boundary](diagrams/structurizr-ArcComponents.svg)

![C4 reference deployment](diagrams/structurizr-Deployment.svg)

## Compose screen definitions and user storyboards

Screen definitions and storyboards are native SVG design assets, separate from C4 architecture dynamics. Ten screen specifications label controls, layouts, actions and important state variants for dashboard, clients/permissions, catalog, functional discovery/activation, callbacks, logs, ARC control, settings, protected owner approval and action-required recovery. Three ordered storyboards show callback discovery/activation/cancel, ARC connect/control/disconnect, and owner settings/CAS/start-error flows with alternate denied/error/cancel branches. They are not UML message sequences. The source assets and screen/state/requirement index are [here](ui-screens-and-storyboards.md).

The slideshow source is [Marp Markdown](../storyboards/storyboard.md), one ordered screen/action step per slide. The [self-contained HTML slideshow](storyboards.html) embeds the SVG assets and provides local navigation; no external hosting is used. Marp CLI4.2.3 and its integrity-locked package graph are task-local tooling. HTML is an optional local presentation of the canonical Markdown/SVG definitions; no live application behavior is implied.

| Requirement | Technical and acceptance contract |
| --- | --- |
| FR-DOC-01 C4 and Structurizr only for current architecture | TR-DOC-01 one DSL workspace and scoped image views for class detail. AC-DOC-01 real Structurizr validation/export passes; expected native/code views exist; class ownership matches the textual Kotlin/ARC design. |
| FR-DOC-02 Gradle build management | TR-DOC-02 pinned wrapper and Kotlin DSL targets with defined inputs/outputs/offline cache. AC-DOC-02 invalid model, missing SVG and broken Markdown link fail; a cached network-disabled build generates and packages all current artifacts. |
| FR-DOC-03 Markdown plus SVG documentation | TR-DOC-03 relative vector assets, editable DSL/code definitions and portable folder layout. AC-DOC-03 all relative links resolve and SVGs contain vector text rather than embedded raster; no obsolete renderer/source files are in the current package. |
| FR-UI-01 SVG screen definitions/storyboards | TR-UI-01 screen/state/control IDs and requirement mapping; ordered user actions with alternate branches. AC-UI-01 visual inspection covers normal/empty/denied/disconnected/error and protected-owner surfaces; actual Compose implementation/instrumentation remains pending. |
| FR-UI-02 text-based slideshow | TR-UI-02 pinned Marp Markdown source, SVG references and Gradle-rendered self-contained HTML. AC-UI-02 local navigation works, all13slides are present and the HTML has no external image/style dependencies. |

Current validation status: round013 reviewed the Kotlin/Compose callback/group design frozen before the internal ARC port, C4/Structurizr/Gradle and Markdown/SVG/storyboard changes. It cannot establish acceptance of this revision. A fresh complete design review is required. Existing full-project98/98 acceptance remains unmet; no design-only score is substituted for catalog/source completeness.
'''
if re.search(r'mermaid|pele',s,re.I):raise ValueError('Obsolete renderer mention remains in canonical document')
# Source/code artifacts are linked only when included in this design package; catalog executable artifacts remain separate task deliverables.
s=re.sub(r'\[([^]]*)\]\((?:<?)(/home/[^)>]+)(?:>?)\)',r'\1 (separate local task artifact)',s)
(D/'Android MCP Bridge Design.md').write_text(s)
proto=R/'arc-upstream/19792a52a13a3df342f90bab6b246b547e673919/src/Avalonia.RemoteControl.Protocol/Protos/remote_control.proto';raw=proto.read_text();messages={};enums={}
for kind,name,body in re.findall(r'\b(message|enum)\s+(\w+)\s*\{([^}]*)\}',raw,re.S):
 rows=[]
 if kind=='message':
  for repeated,typ,field,num in re.findall(r'\b(repeated\s+)?(\w+)\s+(\w+)\s*=\s*(\d+)\s*;',body):rows.append(dict(name=field,type=typ,number=int(num),repeated=bool(repeated)))
  messages[name]=rows
 else:enums[name]={k:int(v) for k,v in re.findall(r'(\w+)\s*=\s*(\d+)\s*;',body)}
(D/'arc-protocol-contract.json').write_text(json.dumps(dict(upstream_commit='19792a52a13a3df342f90bab6b246b547e673919',schema_sha256=hashlib.sha256(proto.read_bytes()).hexdigest(),proto_package='avalonia.remotecontrol.v1',transport='arc-protobuf-v1',wire_version='1.0',package_version='0.8.0',messages=messages,enums=enums,implemented_kotlin_port=False,license_metadata='MIT declared by Directory.Build.props; exact copyright/license notice verification pending before code/schema distribution'),indent=2)+'\n')
(P/'tooling').mkdir(exist_ok=True);shutil.copyfile(P/'.tools/marp/package-lock.json',P/'tooling/marp-package-lock.json');shutil.copyfile(P/'.tools/marp/package.json',P/'tooling/marp-package.json')
(P/'README.md').write_text('''# Android MCP design documentation

Canonical design: [Markdown](docs/Android%20MCP%20Bridge%20Design.md). [Screen definitions](docs/ui-screens-and-storyboards.md). [Storyboard deck](storyboards/storyboard.md). Design and offline tooling only: no deployed Kotlin Android app, phone actions or public service.

Use Gradle8.14.3 with Java21, the exact StructurizrCLI2025.11.09 image digest in toolchain-lock.json, PlantUML1.2025.10 and MarpCLI4.2.3. Existing local image supplies Java/Graphviz. Node26.8.2 was used for local Marp HTML. Dependencies reside under .tools and .gradle-home; no host installation is needed. The Gradle wrapper pins distribution SHA256. Keep these folders out of the portable archive; bootstrap verified dependencies into task-local caches first. The wrapper does not make a fresh cache magically offline-ready.

## Targets

Run ./gradlew --offline validateArchitecture generateArchitectureDiagrams generateUiAssets renderStoryboards verifyDesignDocs packageDesignDocs. The same invocation is used inside the pinned container with only this docs project mounted, NODE_BIN selecting a compatible existing read-only Node runtime, network disabled and no exposed ports. STRUCTURIZR_CLI defaults to the existing image's /usr/local/structurizr-cli/structurizr.sh. The README records the actual invocation and receipts once complete.

validateArchitecture depends on locally rendered code-view images and fails on invalid DSL. generateArchitectureDiagrams exports native C4 views through Structurizr then renders SVGs locally; image views scope the editable inline PlantUML class detail. generateUiAssets copies editable vector screen/storyboard source. renderStoryboards embeds SVG assets into a finite local Marp HTML deck. verifyDesignDocs fails on missing outputs, broken Markdown links or raster/script substitutions. packageDesignDocs creates a deterministic archive with only current docs/SVG/source/build configuration and validation receipts. No diagram is sent to a public renderer and no Structurizr workspace is uploaded.

## Tooling and license

The internal ARC port's pinned source and exact field/enum contract are documented in the design. MIT package metadata was verified upstream; authoritative notices remain a pre-distribution gate for copied/translated schema/code. This package contains the derived design contract, not the upstream source archive or a Kotlin port. Gradle/Structurizr/PlantUML/Marp are build dependencies with their own licenses; the archive includes configuration and wrapper provenance, not downloaded distributions or node_modules.
''')
status=json.loads((R/'LIVE-STATUS.json').read_text());status.update(updated_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),current_phase='Canonical Markdown/C4/ARC port design assembled; native SVG exports exist; Marp Gradle rendering failure under diagnosis',latest_design_path=str(D/'Android MCP Bridge Design.md'),current_diagram_pipeline='Gradle8.14.3 / StructurizrCLI2025.11.09 / PlantUML1.2025.10; existing digest-pinned image, network disabled',ui_screen_definitions=10,ui_storyboards=3,review_scope_current=False,review_scope_note='013predates internalARC/C4/Gradle/MarkdownSVG/Marp; preserve verdict then review current snapshot')
(R/'LIVE-STATUS.json').write_text(json.dumps(status,indent=2)+'\n');print(json.dumps(dict(canonical_markdown_bytes=(D/'Android MCP Bridge Design.md').stat().st_size,arc_messages=len(messages),arc_enums=len(enums))))
