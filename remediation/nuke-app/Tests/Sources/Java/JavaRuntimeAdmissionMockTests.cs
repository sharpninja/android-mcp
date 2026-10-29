using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

internal sealed class ScriptedJavaRuntimeAdmission(JavaRuntimeAdmissionCase test,string? mutation=null):IJavaSourceRuntimeLoader
{
    internal SourceJavaAdmission? Received {get;private set;}
    public IJavaSourceRuntime Open(SourceJavaAdmission admission)
    {
        Received=admission;
        if((!test.Accepted&&mutation!="accept")||mutation=="reject")throw new InvalidDataException("Scripted rejected native manifest: "+test.Id);
        if(mutation=="input-write")File.AppendAllText(Path.GetFullPath(admission.ManifestPath)," ");
        return new ScriptedJavaRuntime(mutation);
    }
    private sealed class ScriptedJavaRuntime:IJavaSourceRuntime
    {
        private readonly ISourceExtractor extractor;private readonly string? mutation;private bool disposed;
        internal ScriptedJavaRuntime(string? mutation)
        {
            this.mutation=mutation;extractor=new ScriptedSourceExtractor(mutation=="kind"?"wrong":"java-source",_=>
            {if(disposed&&mutation!="use-after-close")throw new ObjectDisposedException(nameof(ScriptedJavaRuntime));return mutation=="rows"?JavaContractCases.Get("JAVA-07").Correct() with{Records=[]}:JavaContractCases.Get("JAVA-07").Correct();});
        }
        public ISourceExtractor Extractor{get{ObjectDisposedException.ThrowIf(disposed&&mutation!="getter-after-close",this);return extractor;}}
        public void Dispose(){if(disposed&&mutation=="double-dispose")throw new InvalidOperationException("Wrong repeated dispose");if(mutation!="not-disposed")disposed=true;}
    }
}
[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-004")]
public sealed class JavaRuntimeAdmissionMockTests
{
    public static IEnumerable<object[]> Cases=>JavaRuntimeAdmissionCases.Cases;
    [Theory,MemberData(nameof(Cases))]
    public void AdmissionFixturesAndIndependentFailureLifetimeAndIntegrityControls(string id)
    {
        var test=JavaRuntimeAdmissionCases.Get(id);var fixture=JavaRuntimeAdmissionCases.Create(test,JavaRuntimeAdmissionCases.MockLibrary);var provider=new ScriptedJavaRuntimeAdmission(test);
        JavaRuntimeAdmissionCases.AssertContract(test,provider,fixture);Assert.Same(fixture.Admission,provider.Received);
        foreach(var mutation in test.Accepted?new[]{"reject","kind","rows","not-disposed","getter-after-close","use-after-close","double-dispose","input-write"}:new[]{"accept"})
        {
            var wrongFixture=JavaRuntimeAdmissionCases.Create(test,JavaRuntimeAdmissionCases.MockLibrary);
            Assert.NotNull(Record.Exception(()=>JavaRuntimeAdmissionCases.AssertContract(test,new ScriptedJavaRuntimeAdmission(test,mutation),wrongFixture)));
        }
    }
}
