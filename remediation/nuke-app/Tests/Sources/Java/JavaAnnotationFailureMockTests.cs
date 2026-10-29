using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-004")]
public sealed class JavaAnnotationFailureMockTests
{
    [Fact]
    public void OriginalAnnotationFailureAcceptsIndependentExceptionMock()
    {
        var provider=new ScriptedSourceExtractor("java-source",source=>
        {Assert.Equal(JavaAnnotationFailureCases.Input().Bytes.ToArray(),source.Bytes.ToArray());throw new FormatException("Unclosed annotation");});
        JavaAnnotationFailureCases.AssertContract(provider);Assert.Single(provider.Received);
    }
    [Fact]
    public void MissingWrongTypeAndWrongMessageFailureAreRejected()
    {
        Reject(new ScriptedSourceExtractor("java-source",_=>new([],[],StructuralParseStatus.Clean)));
        Reject(new ScriptedSourceExtractor("java-source",_=>throw new InvalidOperationException("Unclosed annotation")));
        Reject(new ScriptedSourceExtractor("java-source",_=>throw new FormatException("wrong")));
        static void Reject(ISourceExtractor wrong)=>Assert.NotNull(Record.Exception(()=>JavaAnnotationFailureCases.AssertContract(wrong)));
    }
}
