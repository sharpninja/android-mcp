# Shared canonical JSON gate

Approved API: `Core.ICanonicalJson` accepts raw UTF8 bytes or `JsonElement`; both serialization and hashing use the same canonical bytes. `CanonicalJsonException` exposes explicit error codes. No production canonicalizer exists at this checkpoint. Root owns the next NUKE run.

FR-BUILD-003/ac-2 and FR-BUILD-004/ac-1/ac-3 map to TEST-BUILD-003/004. Canonicalization is a semantic contract, separate from the new native producer fingerprint. Ordinary JsonNode serialization cannot define identity bytes. Shared `ContentHash.Sha256` will own production cryptography.

`Tests/Core/CanonicalJsonMockTests.cs` supplies36 fixture vectors:35 raw inputs and29 JsonElement inputs (including explicit undefined). Each runs correct responses and individually wrong bytes/hash or wrong exception code/omitted exception. Expected hashes use the standard library over independent literal expected bytes. This is a test oracle, not another production canonicalizer.

The vectors preserve compact sorted objects, nested values, integer1 versus float1.0, integer negative zero versus float negative zero, float exponent cutovers and padding, arbitrary integer literals, shortest finite binary64 representation, subnormal/min/max finite examples, Unicode scalar ordering, no Unicode normalization, literal Unicode output and JSON escapes. The original metadata numeric/Unicode vector remains explicit. Duplicate decoded property names reject, including nested and escape-equivalent names. Overflow, unpaired surrogate and malformed JSON cases retain exact errors.

Undefined JsonElement and invalid UTF8 raw bytes are checked at the same assertion seam, with exact UNDEFINED_VALUE and INVALID_UNICODE codes. These complete the explicit input-boundary scope before production canonicalization begins.

Native `PolicyReference` already uses the49 unchanged cases from sealed checkpoint001. Native `CatalogReference` implements the16 unchanged entry scenarios against injected canonical/hash services. Its Gate=Real binding waits for the shared canonical implementation so a scripted hash dependency cannot be misreported as full native parity.
