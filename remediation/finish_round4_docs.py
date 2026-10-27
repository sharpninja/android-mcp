import pathlib
R=pathlib.Path(__file__).resolve().parent;C=R/'catalog'
p=C/'README.md';s=p.read_text();start=s.index('Stable entry IDs are SHA-256');end=s.index(' A declaration revision',start)
s=s[:start]+'''Stable entry IDs are SHA-256 of the complete canonical language-prefixed string. java: and java-source: retain source-spelled owners/types; aidl: retains AIDL descriptors and source member signatures; jvm: retains published binary owners and JVM method descriptors (field types may be absent from the lifecycle index). Every language uses the package: infix for package declarations where present. These four namespaces are distinct until explicit equivalence is proven; spelling changes can change source IDs and never silently retarget grants. The full forms and minor-version vocabulary are listed below.'''+s[end:]
s=s.replace('base fields 1.0.0; additions confined to extensions/x-*','base fields1.0.0; documented1.1.0 AIDL kind additions require supported vocabulary; other additive metadata uses extensions/x-*')
p.write_text(s)
p=C/'tooling/build_catalog.py';s=p.read_text().replace("('entry_contract_version','1.0.0')","('entry_contract_version','1.1.0')");p.write_text(s)
p=C/'migrations/002-evidence-and-publication.sql';s=p.read_text().replace('-- Additive migration from version 1. Entry/API JSON contracts retain version 1.0.0;\n-- additions live under their explicitly reserved extensions/x-* namespaces.','-- Storage baseline v3: Entry1.0 base plus documented1.1 AIDL type kinds;\n-- unknown semantic vocabulary is rejected/quarantined, never granted authority.');p.write_text(s)
(C/'schema.sql').write_text('\n'.join(p.read_text() for p in sorted((C/'migrations').glob('*.sql'))))
