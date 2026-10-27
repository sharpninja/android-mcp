# Attribution and source retention

Android SDK/API metadata, AIDL and permission manifests were obtained from Google-hosted public sources at `dl.google.com`, `developer.android.com` and `android.googlesource.com`. Android and Java trademarks belong to their respective owners. The Android Open Source Project and upstream contributors retain copyright in imported source material.

The task workspace preserves acquired bytes under `snapshots/blobs/<content-address>`, source URLs/revisions/timestamps/SHA-256 in the frozen release inventory, and repository tree enumeration under `trees/`. Runtime delivery variants may omit bulk raw source files; the source inventory, hashes and restoration scripts identify and reproduce those exact bytes. Acquired license/NOTICE files and individual source headers remain in the local snapshots. These repositories contain multiple upstream licenses. The catalog does not relicense source material or assert a blanket license for the complete corpus. Missing license evidence is not treated as permission to relicense it.

SDK binary archives were not installed or downloaded, no SDK license acceptance command ran, and no new license agreement was accepted. The official metadata's SDK archive checksum is retained as published metadata, not a verified local binary hash.

For API 37.2, the complete official **source-data** archive `source-37.2_r01.zip` was downloaded and its published SHA-1 and local SHA-256 verified. ZIP-member content addresses use Git's blob hash format solely as local content addresses; they are not claims that those members have an established upstream Git commit. Platform metadata was read through bounded byte ranges with HTTP 206, consistent ETag and ZIP CRC checks. No `android.jar`, compiled class or native-library member was read. The whole platform archive's published checksum is not a locally verified checksum.

Extraction uses task-local Python packages pinned in `requirements-extraction.txt`, including the upstream Tree-sitter Python binding and Java grammar. Their package licenses and authorship remain those of their upstream maintainers. No Android source code was executed or built during extraction.

The input contract foundation is preserved via the original Library ZIP and version/hash receipt. The bridge design input is unchanged and is included only in the separately frozen review evidence. Newly written extractor, schemas and documentation are clearly distinguishable from upstream source snapshots. Their authorship does not imply endorsement by Google, Android or Anthropic.
