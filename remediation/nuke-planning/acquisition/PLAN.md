# Acquisition increment

Parent: Codex-20261028T005928Z-plugin-session / req-20261028T011209Z-turn-1f08.
Own turn: Codex-20261028T030809Z-plugin-session / req-20261028T030843Z-turn-b629.
Requirements: FR-BUILD-001 ac-3, FR-BUILD-003, FR-BUILD-004 ac-2/ac-3, FR-BUILD-007 ac-1; TR-BUILD-TOOL-001/003/004/007; corresponding TEST-BUILD records. The source parity plan is authoritative for exact legacy bounds and transport profiles.

The authorized implementation keeps one `IByteTransport` and one immutable `IArtifactStore` seam. `PinnedRequest` carries URL, revision, expected SHA256/length, bounded range, If-Match/ETag, method, identity encoding, maximum response bytes and declarative retry policy. `TransportResponse` preserves URL/status/bytes/ETag/range/encoding/declared length. Archive partial reads never infer a whole-archive hash. Source pins validate decoded content, optional Git blob framing and exact byte length. File/path validation precedes network, writes or extraction; all original inputs remain retained.

First gate covers TransportMockTests, AcquisitionMockTests, ArchiveMockTests and NativeArchiveMockTests. Correct mocks satisfy identical assertions; independent mutations cover response properties, pins, byte content, inventory selection, CRC, unknown whole hash, response bounds and no-extra-network/write reuse interactions. Production implementations follow the parent-coordinated green mock TRX. Same case providers then bind to real transport, source acquirer and BCL archive readers. Further failure/boundary cases follow the same mock-first rule before any implementation increment they require.

Java dependency source URLs use runtime peeled commit `6e0618704ad758ba2ea5822faa80bcd36fbeba3d`, retaining annotated tag object `695d1530e59804279d9c739335b73b882d9f1caf`, and grammar commit `94703d5a6bed02b98e438d7cad1136c01a60ba2c`. Codeload archives are data. Tar/GZip/ZIP use .NET BCL in-process; extraction never executes source. Source hash manifests record measured archive/member hashes and immutable commit provenance. No system fallback, software installation, Python, Node or external archive CLI is permitted.

Canonical restore retry profile: three total attempts, one-second pacing before each attempt, HTTP429 only; retry delay is max(60 * retry ordinal, Retry-After numeric seconds or HTTP date minus injected clock). Authentication/other HTTP failure pauses immediately and never substitutes URL or a weaker pin. Single-attempt profile represents generic rehydration behavior without a duplicated transport loop.

Metadata range requests are at most8MiB each, selected compressed member at most8MiB and uncompressed member at most32MiB. Full source-data archives are at most64MiB. Metadata selection comes from the single fixed legacy allowlist. Full central-directory inventory includes unselected binaries without reading their payload. ZIP CRC is explicitly verified after bounded expansion. Pinned native tar extraction validates portable member paths and rejects links/special files, duplicate targets and limit overflow before publication.

Cache writes use the shared canonical serializer and hashes. New C# producer identity, source/recipe/profile pins and gzip hash remain separate from historical Python cache identity. Strict historical-byte policy fails before writing; permissive mode writes derived-generation evidence. Generations acquire an artifact lease, bind one recipe digest and publish their receipt last; recovery verifies all recorded output bytes before reuse.

The parent owns registry wiring, whole-application validation and hostile acceptance. No acquisition result here claims full145GB corpus regeneration or cross-platform execution. No goal/TODO/requirement is marked complete without the parent acceptance gate.
