# Package gate scope correction

The immutable `20261029T0007106387299-SdkPackageChoiceAndVersionNative.json` receipt records 52 passed tests, zero failed and zero not executed. Its exact class breakdown is SdkPackageChoiceMockTests24, SdkPackageChoiceRealTests12 and SdkVersionMockTests16. A parent progress message incorrectly described the remaining40 as existing SDK native regression. That description is withdrawn; the receipt itself is unchanged.

The existing native SDK XML cases are in CacheSdkRealTests.SameSdkXmlVersionCases and did not match that gate's filter. Include CacheSdkRealTests in the cohesive exit regression before claiming existing native XML behavior is verified after the typed package-choice change. The new SAME12 native package-choice cases are present in the52 gate.

The separate33 reader and12 caller native adapters remain pending activation of the sole helper-owned MetadataReference projection. Their66 and24 mock cases passed before the staged production bodies. No mock-only count establishes native external-sidecar completion.

Subsequent proof: `20261029T0032336944962-MetadataReaderAndExistingSdkNative.json` (SHA1ba99c5c855d0ec2cc64bb7bf21dd602808ad9986954a5b8ee81e4c7fa0d4afa) records149 passed, zero failed/not executed. Exact breakdown: reader33native/66mocks, package12native/24mocks, CacheSdkRealTests14 (8original SDK XML cases,1cache JSON-lines wire,5cache retention). The reader now binds to the sole helper DTO. Whole caller12native remains pending the helper's PlanCore projection; that dependency is separate from the reader's completed native gate.
