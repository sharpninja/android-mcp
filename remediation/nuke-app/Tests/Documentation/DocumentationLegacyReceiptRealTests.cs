using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationLegacyReceiptRealTests
{
    private static DocumentationLegacyReceipts Provider()=>new(new CanonicalJson(),new StrictJsonInspector(),new ArtifactReceiptVerifier(new NativeContentHash()));
    [Theory,MemberData(nameof(LegacyReceiptCases.Cases),MemberType=typeof(LegacyReceiptCases))]
    public void SameTypedMetadataAndFailureCasesBindNativeReceipts(LegacyReceiptCase test)
    {
        var provider=Provider();LegacyReceiptCases.AssertReceipt(test,input=>input.Kind switch
        {
            LegacyReceiptKind.Module=>provider.Module(input.Modules!),LegacyReceiptKind.Model=>provider.Model(input.Model!),
            LegacyReceiptKind.Ui=>provider.Ui(input.UiManifest!),LegacyReceiptKind.Architecture=>provider.Architecture(input.Workspace!,input.Process!),
            LegacyReceiptKind.Docs=>provider.Docs(input.Documents!),_=>throw new ArgumentOutOfRangeException(nameof(input))
        });
    }
    [Theory,MemberData(nameof(LegacyReceiptSelectionCases.Cases),MemberType=typeof(LegacyReceiptSelectionCases))]
    public void SameCoreStageSelectionsBindNativeReceiptMapping(LegacyReceiptSelectionCase test)=>LegacyReceiptSelectionCases.AssertPaths(test,Provider().Describe);
    [Theory,MemberData(nameof(LegacyReceiptAliasCases.Cases),MemberType=typeof(LegacyReceiptAliasCases))]
    public void SameFiveAliasAndArchiveCoverageCasesBindNativeMapping(LegacyReceiptAliasCase test)=>LegacyReceiptAliasCases.AssertMap(test,Provider().AddPackageAliases);
}
