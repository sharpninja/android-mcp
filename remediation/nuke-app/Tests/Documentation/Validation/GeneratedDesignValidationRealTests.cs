using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using AndroidMcp.BuildTool.Documentation.Validation;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation.Validation;

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-005")]
public sealed class GeneratedDesignValidationRealTests
{
    public static IEnumerable<object[]> Cases=>GeneratedDesignValidationCases.Cases;
    private static IGeneratedDesignValidator Provider()
    {
        var html=new SourceHtmlDecoder();
        return new GeneratedDesignValidator(new ArchitectureViewIndexReader(),new ArtifactReferenceResolver(html.Decode),
            new StrictJsonInspector(),new ArtifactReceiptVerifier(new NativeContentHash()),html);
    }
    private sealed class RecordingValidator(IGeneratedDesignValidator inner):IGeneratedDesignValidator
    {
        internal GeneratedDesignValidationResult? Result {get;private set;}
        public GeneratedDesignValidationResult Validate(GeneratedDesignValidationInput input){var value=inner.Validate(input);Result=value;return value;}
    }
    [Theory,MemberData(nameof(Cases))]
    public void IdenticalGeneratedDesignCasesBindPureNativeValidator(string id)
    {var test=GeneratedDesignValidationCases.Get(id);GeneratedDesignValidationCases.AssertContract(test,Provider());}
    [Theory,MemberData(nameof(Cases))]
    public void IdenticalOutputControlsRejectMutatedNativeResults(string id)
    {
        var test=GeneratedDesignValidationCases.Get(id);var native=new RecordingValidator(Provider());
        GeneratedDesignValidationCases.AssertContract(test,native);Assert.NotNull(native.Result);
        foreach(var wrong in GeneratedDesignValidationResponseControls.Wrong(native.Result!))
            Assert.NotNull(Record.Exception(()=>GeneratedDesignValidationCases.AssertContract(test,new ScriptedGeneratedDesignValidator(test,wrong.Response))));
    }
    [Theory,MemberData(nameof(Cases))]
    public void IdenticalInputIntegrityControlsRejectMutationsAfterNativeValidation(string id)
    {
        foreach(var mutation in new[]{"bytes","model","order","path","media"})
        {
            var test=GeneratedDesignValidationCases.Get(id);var native=new RecordingValidator(Provider());
            GeneratedDesignValidationCases.AssertContract(test,native);Assert.NotNull(native.Result);
            Assert.NotNull(Record.Exception(()=>GeneratedDesignValidationCases.AssertContract(test,new ScriptedGeneratedDesignValidator(test,native.Result!,mutation))));
        }
    }
}
