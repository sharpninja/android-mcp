using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources.Java;
using AndroidMcp.BuildTool.Tests.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-004")]
public sealed class JavaProducerIdentityRealTests(JavaNativeFixture fixture):IClassFixture<JavaNativeFixture>
{
    public static IEnumerable<object[]> Cases=>JavaProducerIdentityCases.Cases;
    [Theory,MemberData(nameof(Cases))]
    public void IdenticalIdentityAndCallerPinCasesBindToTheSoleNativeLoader(string id)
    {
        var relative=fixture.SourceLock["native_artifact"]!["relative_path"]!.GetValue<string>();
        var native=ArtifactPaths.Resolve(Path.GetDirectoryName(fixture.ManifestPath)!,relative);var material=File.ReadAllBytes(native);
        Assert.Equal(fixture.Parser.ArtifactSha256,ContentHash.Sha256(material));
        var test=JavaProducerIdentityCases.Get(id);var input=JavaProducerIdentityCases.Create(test,material,ProcessSessionFixturePaths.NewRoot("java-producer-identity-native"));
        var inspector=new CountingInspector();JavaProducerIdentityCases.AssertContract(test,input,new(new JavaSourceRuntimeLoader(inspector),()=>inspector.Calls));
    }
    private sealed class CountingInspector:IStrictJsonInspector
    {
        private readonly IStrictJsonInspector inner=new StrictJsonInspector();
        internal int Calls {get;private set;}
        public StrictJsonInspection Inspect(ReadOnlyMemory<byte> utf8){Calls++;return inner.Inspect(utf8);}
        public StrictJsonInspection Inspect(JsonElement value){Calls++;return inner.Inspect(value);}
    }
}
