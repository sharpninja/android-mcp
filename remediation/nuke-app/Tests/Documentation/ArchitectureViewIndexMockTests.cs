using System.Text;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed record ArchitectureViewIndexCase(string Id,Artifact Input,ArchitectureViewIndex Expected)
{public override string ToString()=>Id;}
public static class ArchitectureViewIndexCases
{
    public static ArchitectureViewIndex Index(IEnumerable<string> native,IEnumerable<string> images)=>new(Array.AsReadOnly(native.Order(StringComparer.Ordinal).ToArray()),Array.AsReadOnly(images.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()));
    public static IEnumerable<object[]> Cases
    {
        get
        {
            var original=ModelContractFixture.Current.Dsl;
            var expected=Index(ArchitecturePipelineEnvironment.NativeKeys,ArchitecturePreparationCases.CreateCode()[0].Expected.Select(artifact=>"docs/diagrams/"+Path.GetFileNameWithoutExtension(artifact.RelativePath)+".svg"));
            Artifact Text(string text)=>original with{Bytes=Encoding.UTF8.GetBytes(text)};
            yield return [new ArchitectureViewIndexCase("VIEW-CANONICAL",original,expected)];
            yield return [new ArchitectureViewIndexCase("VIEW-CRLF",Text(Encoding.UTF8.GetString(original.Bytes.Span).Replace("\n","\r\n",StringComparison.Ordinal)),expected)];
            yield return [new ArchitectureViewIndexCase("VIEW-IMAGE-ONLY",Text("image bridge.app Custom {\n image ../docs/diagrams/custom-class.svg\n}\n"),Index([], ["docs/diagrams/custom-class.svg"]))];
            yield return [new ArchitectureViewIndexCase("VIEW-NATIVE-DUPLICATE",Text(Encoding.UTF8.GetString(original.Bytes.Span)+"\nsystemContext bridge Context \"duplicate\" {\n}\n"),Index(expected.NativeKeys.Append("Context"),expected.ImageSvgPaths))];
            yield return [new ArchitectureViewIndexCase("VIEW-IMAGE-DUPLICATE",Text(Encoding.UTF8.GetString(original.Bytes.Span)+"\nimage ../docs/diagrams/server-core-classes.svg\n"),expected)];
            yield return [new ArchitectureViewIndexCase("VIEW-EMPTY",Text(""),Index([],[]))];
            yield return [new ArchitectureViewIndexCase("VIEW-COMMENT",Text("// systemContext bridge Fake \"ignored\" {\n// image ../docs/diagrams/ignored.svg\n"),Index([],[]))];
            yield return [new ArchitectureViewIndexCase("VIEW-INVALID-NATIVE-ID",Text("systemContext bridge invalid-key \"ignored\" {\n}\n"),Index([],[]))];
            yield return [new ArchitectureViewIndexCase("VIEW-UNRELATED-IMAGE",Text("image ../../outside.svg\n"),Index([],[]))];
        }
    }
    public static void AssertContract(ArchitectureViewIndexCase test,IArchitectureViewIndexReader provider)
    {
        var before=test.Input.Bytes.ToArray();var actual=provider.Read(test.Input);
        Assert.Equal(test.Expected.NativeKeys,actual.NativeKeys);Assert.Equal(test.Expected.ImageSvgPaths,actual.ImageSvgPaths);Assert.Equal(before,test.Input.Bytes.ToArray());
    }
}
public sealed class ScriptedArchitectureViewIndex(ArchitectureViewIndex response):IArchitectureViewIndexReader
{
    public ArchitectureViewIndex Read(Artifact workspaceDsl)=>response;
}
[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class ArchitectureViewIndexMockTests
{
    [Theory,MemberData(nameof(ArchitectureViewIndexCases.Cases),MemberType=typeof(ArchitectureViewIndexCases))]
    public void CorrectSingleViewAuthorityMocksPass(ArchitectureViewIndexCase test)=>ArchitectureViewIndexCases.AssertContract(test,new ScriptedArchitectureViewIndex(test.Expected));
    [Theory,MemberData(nameof(ArchitectureViewIndexCases.Cases),MemberType=typeof(ArchitectureViewIndexCases))]
    public void EveryWrongIndexOutcomeIsRejected(ArchitectureViewIndexCase test)=>Assert.NotNull(Record.Exception(()=>ArchitectureViewIndexCases.AssertContract(test,new ScriptedArchitectureViewIndex(new(["WRONG"],[])))));
    [Theory]
    [InlineData("native-missing")][InlineData("native-extra")][InlineData("native-order")][InlineData("image-missing")][InlineData("image-extra")][InlineData("image-order")]
    public void EachViewSetAndOrderControlRejectsDrift(string mutation)
    {
        var test=(ArchitectureViewIndexCase)ArchitectureViewIndexCases.Cases.First()[0];var expected=test.Expected;
        var wrong=mutation switch
        {
            "native-missing"=>expected with{NativeKeys=expected.NativeKeys.Skip(1).ToArray()},"native-extra"=>expected with{NativeKeys=expected.NativeKeys.Append("WRONG").ToArray()},"native-order"=>expected with{NativeKeys=expected.NativeKeys.Reverse().ToArray()},
            "image-missing"=>expected with{ImageSvgPaths=expected.ImageSvgPaths.Skip(1).ToArray()},"image-extra"=>expected with{ImageSvgPaths=expected.ImageSvgPaths.Append("docs/diagrams/wrong.svg").ToArray()},_=>expected with{ImageSvgPaths=expected.ImageSvgPaths.Reverse().ToArray()}
        };
        Assert.NotNull(Record.Exception(()=>ArchitectureViewIndexCases.AssertContract(test,new ScriptedArchitectureViewIndex(wrong))));
    }
    [Fact]
    public void NativeDuplicatesRemainVisibleToExistingConsumerValidation()
    {
        var test=ArchitectureViewIndexCases.Cases.Select(row=>(ArchitectureViewIndexCase)row[0]).Single(test=>test.Id=="VIEW-NATIVE-DUPLICATE");
        Assert.NotNull(Record.Exception(()=>ArchitectureViewIndexCases.AssertContract(test,new ScriptedArchitectureViewIndex(test.Expected with{NativeKeys=test.Expected.NativeKeys.Distinct(StringComparer.Ordinal).ToArray()}))));
    }
    [Fact]
    public void ExistingImageDeduplicationIsPreserved()
    {
        var test=ArchitectureViewIndexCases.Cases.Select(row=>(ArchitectureViewIndexCase)row[0]).Single(test=>test.Id=="VIEW-IMAGE-DUPLICATE");
        Assert.NotNull(Record.Exception(()=>ArchitectureViewIndexCases.AssertContract(test,new ScriptedArchitectureViewIndex(test.Expected with{ImageSvgPaths=test.Expected.ImageSvgPaths.Append(test.Expected.ImageSvgPaths[0]).ToArray()}))));
    }
}
