import pathlib
R=pathlib.Path(__file__).resolve().parent
s=(R/'package_design_update_round12.py').read_text()
s=s.replace("len(manifest['diagrams'])==4","len(manifest['diagrams'])==7")
s=s.replace("'AC-BIND-01','unavailable','unknown'","'AC-BIND-01','FR-CB-01','FR-GRP-01','resources/subscribe','notifications/resources/updated','Last-Event-ID','android.groups.discover','unavailable','unknown'")
s=s.replace('current_library_baseline_versions=[5,3,2]','current_library_baseline_versions=[6,4,3]')
s=s.replace("pages=[27,28,29,36],result='Readable responsibility table, full sequence, new projection/epoch section, traceability and sizing; no clipping or overlap observed.'","pages=[37,38,40,43,44,45],result='Callback protocol and diagrams, group flow and requirement table visually inspected; no clipping or overlap observed.'")
s=s.replace('mermaid_sources=4,standalone_svg_assets=4','mermaid_sources=7,standalone_svg_assets=7')
s=s.replace("resolver_reference_tests='245normal and245optimized suite pass; explicit malformed-kind rejection, effective overlay/document/schema/type gates; no device observations'","resolver_reference_tests='271 normal and271 optimized offline regression tests pass, including lossless nonUTF8 native retention. New callback/group lifecycle fixtures are pending; no deployed service or device observations.'")
s=s.replace("independent_validation_status='Round011 overall93/84 NOT_READY. Corrected245testcandidate frozen in running012; no98/98claim.'","independent_validation_status='Round012 overall93/80 NOT_READY, design94/95 contracts95/94. New callback/group design not in frozen012 and awaiting fresh independent review. No98/98claim.'")
s=s.replace('library-baseline-round012/Android MCP Bridge Design Portable.zip','library-baseline-callback-groups/Android MCP Bridge Design Portable.zip')
a=s.index("readme+='\\n\\n## Current corrected revision")
b=s.index("\n(D/'README.md').write_text(readme)",a)
s=s[:a]+"readme+='\\n\\n## Callback and functional tool-group design update (27 October 2026)\\n\\nThis revision adds exhaustive evidence-qualified callback roles, finite registration with bounded resource-journal streaming, cancellation and synchronous local reply policies, plus small functional groups, job discovery, session activation, byte budgets and a static-host generic-invocation fallback. Seven editable Mermaid sources, Pele1.10.0 SVGs and rendered PNG assets are included. Existing startup resolution, Binder/policy, Avalonia12, SQLite, ADB, RemoteControl and lifecycle requirements are retained. Current offline regression suite passes271 normal and271 optimized tests; callback/group lifecycle reference fixtures and new independent review are pending. Latest complete Claude012 is NOT_READY overall93/80, design94/95 contracts95/94; no98/98agreement is claimed. Only design/render assets and design QA are packaged; no source/catalog/review-evidence bundle.\\n'"+s[b:]
(R/'package_callback_group_design.py').write_text(s)
print('Prepared package validation for seven diagrams; run after visual QA.')
