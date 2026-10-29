# Focused mock-first contract for staged SDK metadata delta

No implementation or tests were run for this proposal. Root owns Core and the frozen full016 source. The existing evaluator/session/child chain remains the only SDK authority. Extend its existing cases rather than adding another evaluator or process probe.

Required named cases before production edits:

- EvaluatedTestProjectAndExplicitKind: actual native Project.GetPropertyValue returns true/unit (or the Root-approved explicit integration role), and both strings survive native DTO -> child receipt -> evaluated project -> Review normalization with actual project/import fingerprints.
- EvaluatedNonTestProject: explicit false/none survives unchanged. An empty property is a different observation and must not be normalized to false.
- MissingOrMalformedTestMetadata: null/empty/invalid boolean or unknown/mismatched kind blocks Review normalization, while Core's general source/PE collector retains unresolved values without fabricating facts.
- ConditionalImportedMetadata: the selected Configuration/TargetFramework and actual imported property definitions determine the values; changed import bytes or conflicting policy invalidate the head-bound normalized receipt.
- CompleteDiscoverySeed: a discovered MSBuild project absent from the host/tests reference graph is still loaded by the same evaluator via ProjectInventory. No missing/extra/duplicate definition can be hidden by a caller-selected TestsProject.
- InventoryChildRoundTrip: typed ProjectInventory and test metadata survive the existing TestGateSdkChildRequest.Native.Evaluation path. Parent configuration/inventory mismatch, child request mutation or omitted fields fail before accepting a child receipt. Preserve existing native pins, loaded MVID, strict-new artifacts and nonce guarantees.
- NonMsbuildPolicy: exact Gradle/Maven/package definitions use an explicit Root-reviewed policy pin tied to their Git bytes; the SDK is never claimed to have evaluated them. Missing/stale/conflicting classification blocks.
- MixedProjectPolicy: a project needed in both unit and integration inventories cannot be duplicated as two project rows or silently forced to one kind. Root must resolve its explicit policy/schema and test every affected prerequisite/Prepare contract before positive composition.
- CurrentCoreGateCompatibility: omitted ProjectInventory retains the existing host/tests graph contract. New optional DTO members require updated typed mock packets and actual child round-trip checks; do not rewrite positive expected bytes from production output.

For each case assert exact metadata, project denominator, original-source/import hashes, child request/response pins and process/read/write effects. Supply independently correct fixtures, then individual wrong metadata/denominator/head/hash/interaction controls inside a passing mock gate. Bind SAME fixtures to the existing native adapter. These scoped gates precede an eventual Root integrated gate; no broad tests, provider/HV or denied official-plugin access is part of this staging contribution.
