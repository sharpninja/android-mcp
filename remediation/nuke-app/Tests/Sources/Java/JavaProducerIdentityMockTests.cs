using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using AndroidMcp.BuildTool.Sources.Java;
using AndroidMcp.BuildTool.Tests.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

internal sealed class ScriptedJavaProducerIdentity(JavaProducerIdentityCase test,JavaProducerIdentityFixture fixture,int mutation=-1):IJavaSourceRuntimeLoader
{
    internal int StrictCalls {get;private set;}
    public IJavaSourceRuntime Open(SourceJavaAdmission admission)
    {
        Assert.Same(fixture.Admission,admission);
        if(!test.Accepted&&mutation!=15){if(mutation==14)StrictCalls++;throw new InvalidDataException("Scripted caller pin rejection");}
        StrictCalls=mutation==14?0:1;return new Runtime(fixture.Expected,mutation);
    }
    private sealed class Runtime(JavaSourceProducerIdentity identity,int mutation):IJavaSourceProducerIdentityRuntime
    {
        private bool disposed;
        private readonly JavaSourceProducerIdentity descriptor=JavaProducerIdentityCases.Wrong(identity,mutation);
        private readonly ISourceExtractor extractor=new ScriptedSourceExtractor("java-source",_=>JavaContractCases.Get("JAVA-07").Correct());
        public JavaSourceProducerIdentity Identity {get{ObjectDisposedException.ThrowIf(disposed&&mutation!=13,this);return descriptor;}}
        public ISourceExtractor Extractor {get{ObjectDisposedException.ThrowIf(disposed,this);return extractor;}}
        public void Dispose()=>disposed=true;
    }
}
[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-004")]
public sealed class JavaProducerIdentityMockTests
{
    public static IEnumerable<object[]> Cases=>JavaProducerIdentityCases.Cases;
    [Theory,MemberData(nameof(Cases))]
    public void ExactDescriptorAndCallerPinAdmissionCasesPassScriptedProvider(string id)
    {
        var test=JavaProducerIdentityCases.Get(id);var fixture=JavaProducerIdentityCases.Create(test,JavaRuntimeAdmissionCases.MockLibrary,ProcessSessionFixturePaths.NewRoot("java-producer-identity-mock"));
        var loader=new ScriptedJavaProducerIdentity(test,fixture);JavaProducerIdentityCases.AssertContract(test,fixture,new(loader,()=>loader.StrictCalls));
    }
    [Theory,MemberData(nameof(Cases))]
    public void IndependentDescriptorLifetimeAndBeforeValidationControlsAreRejected(string id)
    {
        var test=JavaProducerIdentityCases.Get(id);
        foreach(var mutation in test.Accepted?Enumerable.Range(0,15):new[]{14,15})
        {
            var fixture=JavaProducerIdentityCases.Create(test,JavaRuntimeAdmissionCases.MockLibrary,ProcessSessionFixturePaths.NewRoot("java-producer-identity-control"));
            var loader=new ScriptedJavaProducerIdentity(test,fixture,mutation);Assert.NotNull(Record.Exception(()=>JavaProducerIdentityCases.AssertContract(test,fixture,new(loader,()=>loader.StrictCalls))));
        }
    }
}
