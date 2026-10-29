using System.Text;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed record DocumentationManifestCase(string Id,IReadOnlyList<Artifact> Inputs,
    DocumentationManifest Expected,string? Error,bool Verify)
{public override string ToString()=>Id;}
public static class DocumentationManifestCases
{
    public const string ChecksumPath="docs/checksums.sha256";
    public const string GoldenRows="ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad  README.md\n"+
        "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855  docs/empty.svg\n";
    public static IReadOnlyList<Artifact> Inputs=>[
        new("README.md",Encoding.UTF8.GetBytes("abc"),"text/markdown"),
        new("docs/empty.svg",Array.Empty<byte>(),"image/svg+xml")];
    public static DocumentationManifest Golden=>new(new(ChecksumPath,Encoding.UTF8.GetBytes(GoldenRows),"text/plain"),[
        new("README.md","ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",3,"text/markdown"),
        new("docs/empty.svg","e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",0,"image/svg+xml")]);
    public static IEnumerable<object[]> BuildCases
    {
        get
        {
            var inputs=Inputs;var golden=Golden;
            return new[]{
                new DocumentationManifestCase("MF-BUILD-EXACT",inputs,golden,null,false),
                new("MF-BUILD-INPUT-ORDER",inputs.Reverse().ToArray(),golden,null,false),
                new("MF-BUILD-EMPTY",[],new(new(ChecksumPath,Array.Empty<byte>(),"text/plain"),[]),null,false),
                new("MF-BUILD-UNSAFE",inputs.Append(new("../escape",new byte[]{1},"text/plain")).ToArray(),golden,"MANIFEST_INPUT_PATH",false),
                new("MF-BUILD-DUPLICATE",inputs.Append(inputs[0]).ToArray(),golden,"MANIFEST_INPUT_DUPLICATE",false),
                new("MF-BUILD-CASE-COLLISION",inputs.Append(inputs[0] with{RelativePath="readme.md"}).ToArray(),golden,"MANIFEST_INPUT_DUPLICATE",false),
                new("MF-BUILD-RESERVED",inputs.Append(golden.Checksums).ToArray(),golden,"MANIFEST_RESERVED",false),
                new("MF-BUILD-RESERVED-CASE",inputs.Append(golden.Checksums with{RelativePath="DOCS/CHECKSUMS.SHA256"}).ToArray(),golden,"MANIFEST_RESERVED",false)
            }.Select(value=>new object[]{value});
        }
    }
    public static IEnumerable<object[]> VerifyCases
    {
        get
        {
            var rows=new List<DocumentationManifestCase>{new("MF-VERIFY-EXACT",Inputs,Golden,null,true),new("MF-VERIFY-INPUT-ORDER",Inputs.Reverse().ToArray(),Golden,null,true)};
            foreach(var member in new[]{"checksum-path","checksum-media","checksum-bytes","covered-path","covered-hash","covered-length","covered-media","covered-missing","covered-extra","covered-empty","covered-order","covered-duplicate","covered-case","input-bytes","input-media","input-missing","input-extra"})
            {
                var manifest=Golden;var covered=manifest.CoveredInputs.ToArray();var inputs=Inputs.ToArray();
                switch(member)
                {
                    case "checksum-path":manifest=manifest with{Checksums=manifest.Checksums with{RelativePath="docs/other.sha256"}};break;
                    case "checksum-media":manifest=manifest with{Checksums=manifest.Checksums with{MediaType="application/json"}};break;
                    case "checksum-bytes":manifest=manifest with{Checksums=manifest.Checksums with{Bytes=Encoding.UTF8.GetBytes("caller-forged\n")}};break;
                    case "covered-path":covered[0]=covered[0] with{RelativePath="docs/renamed.md"};break;
                    case "covered-hash":covered[0]=covered[0] with{Sha256=new string('a',64)};break;
                    case "covered-length":covered[0]=covered[0] with{Length=4};break;
                    case "covered-media":covered[0]=covered[0] with{MediaType="text/plain"};break;
                    case "covered-missing":covered=covered.Skip(1).ToArray();break;
                    case "covered-extra":covered=covered.Append(new("docs/extra",new string('b',64),0,"text/plain")).ToArray();break;
                    case "covered-empty":covered=[];manifest=manifest with{Checksums=manifest.Checksums with{Bytes=ReadOnlyMemory<byte>.Empty}};break;
                    case "covered-order":Array.Reverse(covered);break;
                    case "covered-duplicate":covered=covered.Append(covered[0]).ToArray();break;
                    case "covered-case":covered[0]=covered[0] with{RelativePath="readme.md"};break;
                    case "input-bytes":inputs[0]=inputs[0] with{Bytes=Encoding.UTF8.GetBytes("abd")};break;
                    case "input-media":inputs[0]=inputs[0] with{MediaType="text/plain"};break;
                    case "input-missing":inputs=inputs.Skip(1).ToArray();break;
                    case "input-extra":inputs=inputs.Append(new("docs/extra",Array.Empty<byte>(),"text/plain")).ToArray();break;
                }
                manifest=manifest with{CoveredInputs=covered};rows.Add(new("MF-VERIFY-"+member,inputs,manifest,"MANIFEST_MISMATCH",true));
            }
            return rows.Select(value=>new object[]{value});
        }
    }
    public static IEnumerable<object[]> AllCases=>BuildCases.Concat(VerifyCases);
    public static void AssertContract(DocumentationManifestCase test,IDocumentationManifestBuilder provider)
    {
        var snapshots=test.Inputs.Select(value=>value.Bytes.ToArray()).ToArray();var checksum=test.Expected.Checksums.Bytes.ToArray();var pins=test.Expected.CoveredInputs.ToArray();
        if(test.Verify)
        {
            var failure=Record.Exception(()=>provider.Verify(test.Inputs,test.Expected));
            if(test.Error is null)Assert.Null(failure);else Assert.Equal(test.Error,Assert.IsType<ArchitectureAssetException>(failure).Code);
        }
        else if(test.Error is not null)Assert.Equal(test.Error,Assert.Throws<ArchitectureAssetException>(()=>provider.Build(test.Inputs)).Code);
        else
        {
            var actual=provider.Build(test.Inputs);Assert.Equal(test.Expected.Checksums.RelativePath,actual.Checksums.RelativePath);
            Assert.Equal(test.Expected.Checksums.MediaType,actual.Checksums.MediaType);Assert.Equal(checksum,actual.Checksums.Bytes.ToArray());Assert.Equal(pins,actual.CoveredInputs);
            var repeated=provider.Build(test.Inputs);Assert.Equal(checksum,repeated.Checksums.Bytes.ToArray());Assert.Equal(pins,repeated.CoveredInputs);
        }
        Assert.Equal(checksum,test.Expected.Checksums.Bytes.ToArray());Assert.Equal(pins,test.Expected.CoveredInputs);
        for(var index=0;index<snapshots.Length;index++)Assert.Equal(snapshots[index],test.Inputs[index].Bytes.ToArray());
    }
}
public sealed class ScriptedDocumentationManifestBuilder(DocumentationManifestCase test,string mutation=""):IDocumentationManifestBuilder
{
    public DocumentationManifest Build(IReadOnlyList<Artifact> artifacts)
    {
        Assert.Same(test.Inputs,artifacts);if(test.Error is not null)throw new ArchitectureAssetException(mutation=="outcome"?"WRONG":test.Error,"fixture");
        if(mutation=="outcome")throw new ArchitectureAssetException("WRONG","fixture");
        var result=test.Expected;var pins=result.CoveredInputs.ToArray();
        switch(mutation)
        {
            case "path":result=result with{Checksums=result.Checksums with{RelativePath="wrong"}};break;
            case "media":result=result with{Checksums=result.Checksums with{MediaType="wrong"}};break;
            case "bytes":result=result with{Checksums=result.Checksums with{Bytes=new byte[]{0}}};break;
            case "covered-path":pins[0]=pins[0] with{RelativePath="wrong"};break;
            case "covered-hash":pins[0]=pins[0] with{Sha256="wrong"};break;
            case "covered-length":pins[0]=pins[0] with{Length=999};break;
            case "covered-media":pins[0]=pins[0] with{MediaType="wrong"};break;
            case "covered-set":pins=pins.Skip(1).ToArray();break;
        }
        return result with{CoveredInputs=pins};
    }
    public void Verify(IReadOnlyList<Artifact> artifacts,DocumentationManifest manifest)
    {
        Assert.Same(test.Inputs,artifacts);Assert.Same(test.Expected,manifest);
        if(test.Error is not null&&mutation!="outcome")throw new ArchitectureAssetException(test.Error,"fixture");
        if(test.Error is null&&mutation=="outcome")throw new ArchitectureAssetException("WRONG","fixture");
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationManifestMockTests
{
    [Theory,MemberData(nameof(DocumentationManifestCases.AllCases),MemberType=typeof(DocumentationManifestCases))]
    public void CorrectBuildAndIndependentAdmissionMocksPass(DocumentationManifestCase test)=>DocumentationManifestCases.AssertContract(test,new ScriptedDocumentationManifestBuilder(test));
    [Theory,MemberData(nameof(DocumentationManifestCases.AllCases),MemberType=typeof(DocumentationManifestCases))]
    public void EveryWrongOutcomeIsRejected(DocumentationManifestCase test)=>Assert.NotNull(Record.Exception(()=>DocumentationManifestCases.AssertContract(test,new ScriptedDocumentationManifestBuilder(test,"outcome"))));
    [Theory]
    [InlineData("path")][InlineData("media")][InlineData("bytes")][InlineData("covered-path")][InlineData("covered-hash")][InlineData("covered-length")][InlineData("covered-media")][InlineData("covered-set")]
    public void EveryIncorrectArtifactOrCoveragePropertyIsRejected(string mutation)
    {var test=(DocumentationManifestCase)DocumentationManifestCases.BuildCases.First()[0];Assert.NotNull(Record.Exception(()=>DocumentationManifestCases.AssertContract(test,new ScriptedDocumentationManifestBuilder(test,mutation))));}
}
