import pathlib,json,datetime
R=pathlib.Path(__file__).resolve().parent;D=R/'design';p=D/'Android MCP Bridge Design.md';s=p.read_text()
replacements={
'Avalonia 12 permission management and SQLite client configuration':'Kotlin MCP service, Jetpack Compose management UI and SQLite client configuration',
'Avalonia 12 manages permissions':'Jetpack Compose manages permissions through the Kotlin service',
'Avalonia activity':'Compose activity','Avalonia top AppBar':'Compose top AppBar','# 8 Avalonia management interface':'# 8 Kotlin Compose management interface',
'| Bridge.UI | Avalonia views, view models, local admin workflows':'| Bridge.UI | Kotlin Compose views, ViewModels, local admin workflows',
'They call the same application interfaces in process; creating another Binder hop solely for UI control adds no security boundary.':'The Compose activity uses a bounded unexported local Binder facade into the Kotlin service; both run in the same process and this is not a privilege boundary.',
'Avalonia 12 supports Android on .NET 10; target net10.0-android and pin a tested, consistent 12.x package set.':'Both the MCP server and management UI are Kotlin by the user\'s latest decision. Use a Kotlin Android foreground service and Jetpack Compose Activity; pin a tested compatible Kotlin/Android/Compose package set. No .NET runtime is required by the bridge.',
'Release build runs an Android compatible HTTP engine with the MCP protocol layer. Do not assume ASP.NET Core or Kestrel works on Android; hide the engine behind ILoopbackHost.':'Kotlin release build runs an Android-compatible HTTP engine behind the Kotlin LoopbackHost interface, with pinned MCP wire contracts and bounded IO. No ASP.NET Core/Kestrel host is used.',
'Prove selected provider on each shipped ABI, trimmed release and chosen AOT mode. Microsoft.Data.Sqlite defaults to SQLitePCLRaw.bundle_e_sqlite3; this does not prove this app deployment works. [S2]':'Use service-owned Android SQLite access with prepared statements and executable DDL. Prove constraints, triggers, migrations, transactions and catalog scale on every supported Android/ABI profile. [S2]',
'A protected Avalonia management surface':'A protected Compose management surface',
'.NET 10 interop, shipped ABIs':'Kotlin/JNI boundary where native ADB code is required, shipped ABIs',
'In Avalonia, add an All bindings browser':'In Compose, add an All bindings browser',
'| Avalonia view models |':'| Kotlin Compose ViewModels |',
'Retain every earlier requirement: Avalonia12, SQLite':'Retain every current requirement: Kotlin server and Compose UI (superseding the earlier Avalonia12/.NET UI choice), SQLite',
'prove Avalonia 12 Android release build':'prove Kotlin Android service and Compose release build',
'[[S1] Avalonia 12 breaking changes and Android .NET requirement](https://docs.avaloniaui.net/docs/avalonia12-breaking-changes)':'[[S1] Jetpack Compose native Kotlin UI](https://developer.android.com/compose)',
'[[S2] Microsoft Data Sqlite custom versions and native providers](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/custom-versions)':'[[S2] Android SQLite API](https://developer.android.com/reference/android/database/sqlite/SQLiteDatabase)',
'Avalonia12 management UI':'Kotlin Compose management UI','closing the Avalonia window':'closing the Compose activity',
'Avalonia12 presents a functional-group editor':'The Kotlin Compose UI presents a functional-group editor',
'and Avalonia group/subscription editors':'and Compose group/subscription editors','actual Avalonia/MCP host behavior':'actual Compose/MCP host behavior',
'Bridge.Domain':'bridge.domain (Kotlin)','Bridge.Application':'bridge.application (Kotlin)','Bridge.Protocol':'bridge.protocol (Kotlin)','Bridge.Bindings':'bridge.bindings (Kotlin runtime)','Bridge.Storage.Sqlite':'bridge.storage.sqlite (Kotlin)','Bridge.Android':'bridge.android (Kotlin)','Bridge.UI':'bridge.ui (Kotlin Compose)','Bridge.Tests':'bridge.tests (Kotlin plus offline tooling)',
}
for a,b in replacements.items():
 if a not in s:raise ValueError('Expected prior text not found: '+a)
 s=s.replace(a,b)
s=s.replace('The MCP server runs in a persistent foreground service','The Kotlin MCP server runs in a persistent foreground service')
s=s.replace('Use one Android application process and UID initially.','Use one Kotlin Android APK, application process and UID initially.')
s=s.rstrip()+'\n\n'+(D/'kotlin-host-and-ui.md').read_text();p.write_text(s)
for name in ['architecture','callback-architecture']:
 p=D/'sources'/(name+'.mmd');t=p.read_text().replace('Persistent MCP service','Kotlin foreground MCP service').replace('Avalonia management UI','Compose management UI').replace('Headless callback broker','Kotlin headless callback broker');p.write_text(t)
p=D/'render.mjs';t=p.read_text().replace("'tool-group-flow'])","'tool-group-flow','kotlin-service-ui'])");p.write_text(t)
# Existing embedded architecture source follows the corrected canonical source.
p=D/'Android MCP Bridge Design.md';s=p.read_text();a=s.index('```mermaid\nflowchart TD');b=s.index('```',a+11);s=s[:a]+'```mermaid\n'+(D/'sources/architecture.mmd').read_text()+s[b:];p.write_text(s)
p=R/'review_design_round.py';s=p.read_text().replace('3 Avalonia12 management UI, SQLite','3 LATEST USER DECISION: MCP server Kotlin and management UI Kotlin JetpackCompose, superseding original Avalonia12/.NETUI. OneAPK Kotlin foregroundservice+ComposeActivity with bounded unexported localBinder facade; service owns runtime/policy/SQLite, UI cannot bypass ownerauth. No managedruntime or Kestrel. Pythonextractor/reference is tooling, not Kotlinserver implementation. Preserve genericADBcontrol; optional externalAvaloniaRuntime target adapter is protocol only, not bridgeUI dependency. SQLite').replace('and AvaloniaUI.','and ComposeUI.');p.write_text(s)
live=json.loads((R/'LIVE-STATUS.json').read_text());live.update(updated_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),current_phase='Kotlin service and Compose UI requirement integrated before review launch; regenerate design for next frozen snapshot',new_requirement_scope='Callbacks, functional tool groups, Kotlin server and Kotlin Compose UI. Last saved7/5/4 precedes language switch; new revision not yet saved.');(R/'LIVE-STATUS.json').write_text(json.dumps(live,indent=2)+'\n')
print('Kotlin and Compose architecture applied; no reviewer launched yet.')
