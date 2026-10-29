# Current GeneratedDesign lock fixture selection

The initial corpus fixture selected `remediation/design-c4/toolchain-lock.json` as a current source but checked its bytes against `DocumentationFixture.ExpectedHashes`, whose lock entry intentionally remains the historical `2dc2418974ec245d87551d5ab56e7878c9e844e832e3c79b6e17065873e3e8bf` fixture. That pairing was wrong. All three GeneratedDesign MemberData discovery entrypoints failed before the 129 cases materialized. This is not a 387-test pass or a validator failure.

`RendererProfileCases.Original()` explicitly reads the retained portable fixture `remediation/nuke-planning/docs/fixtures/toolchain-lock.pre-redirect-admission.json` and checks that historical hash. Those original profile tests and their inputs/assertions stay unchanged.

The current GeneratedDesign source is the previously admitted additive redirect migration. Its independent provenance is:

- Existing `docs/evidence/toolchain-lock-redirect-migration.json`, SHA256 `5902596d6c8ce160892db56b01317c89e669d01c3284d91f9731c4e0f6c19f7a`, records historical hash `2dc2418974ec245d87551d5ab56e7878c9e844e832e3c79b6e17065873e3e8bf` and approved current hash `94dbb4754728e970ec1ff37f358f987b76e5eb1e8a456a2b14e003bfc218751e`. Its only semantic change is the explicit checksum redirect allowlist for the already pinned Gradle artifact.
- Existing `docs/evidence/architecture-native-003-inspection.json`, SHA256 `12d8708e8ed95fe0ad1927c452d1ab5ddcf7673b10606c8722df96d5cafc4994`, independently records the same current lock hash among actual Architecture003's six unchanged source inputs.
- Existing `RendererRedirectCases` authors `REDIRECT-OBSERVED` from the retained original fixture and exact approved redirect URI. It is an independent semantic expectation, not a new hash taken from the failing corpus read.

The staged one-file correction verifies the pinned existing migration receipt, binds its before hash to the still-retained historical expectation, checks current source bytes against its after hash, and additionally checks semantic equality with `REDIRECT-OBSERVED`. All paths used by the fixture remain workspace-relative. No source lock, historical golden, original renderer test, validator expectation, count or input-selection descriptor changes. No checksum/corpus claim is inferred from reading a current file.

The earlier compile failure and this discovery failure remain historical failed attempts. Only a later root-owned terminal gate can establish that the corrected finite mock cases materialize and pass. There is no validator implementation or human validation in this increment.
