using System.Text;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public sealed record ArtifactReceiptCase(string Id,StoredArtifact Receipt,bool ExpectedMatches)
{public override string ToString()=>Id;}
public sealed class RecordingReceiptHash:IContentHash
{
    public const string Digest="ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";
    public List<byte[]> Inputs {get;}=[];
    public string Sha256(ReadOnlySpan<byte> input){Inputs.Add(input.ToArray());return Digest;}
    public string AidlSha1Legacy(ReadOnlySpan<byte> input)=>throw new NotSupportedException("SHA256-only receipt contract");
    public string GitBlobSha1Legacy(ReadOnlySpan<byte> input)=>throw new NotSupportedException("SHA256-only receipt contract");
}
public static class ArtifactReceiptCases
{
    public static Artifact Input=>new("docs/example.svg",Encoding.UTF8.GetBytes("abc"),"image/svg+xml");
    public static StoredArtifact Expected=>new("docs/example.svg",RecordingReceiptHash.Digest,3,"image/svg+xml");
    public static IEnumerable<object[]> Data=>new[]
    {
        new ArtifactReceiptCase("RECEIPT-EXACT",Expected,true),
        new ArtifactReceiptCase("RECEIPT-PATH",Expected with{RelativePath="Docs/example.svg"},false),
        new ArtifactReceiptCase("RECEIPT-HASH",Expected with{Sha256=new string('a',64)},false),
        new ArtifactReceiptCase("RECEIPT-LENGTH",Expected with{Length=4},false),
        new ArtifactReceiptCase("RECEIPT-MEDIA",Expected with{MediaType="text/plain"},false)
    }.Select(c=>new object[]{c});
    public static void AssertCase(ArtifactReceiptCase test,IArtifactReceiptVerifier provider,RecordingReceiptHash? hashes)
    {
        var input=Input;var before=input.Bytes.ToArray();Assert.Equal(Expected,provider.Describe(input));Assert.Equal(test.ExpectedMatches,provider.Matches(input,test.Receipt));
        if(hashes is not null){Assert.Equal(2,hashes.Inputs.Count);foreach(var bytes in hashes.Inputs)Assert.Equal(before,bytes);}
        Assert.Equal(before,input.Bytes.ToArray());
    }
}
public sealed class ScriptedArtifactReceiptVerifier(RecordingReceiptHash hashes,string mutation=""):IArtifactReceiptVerifier
{
    public StoredArtifact Describe(Artifact artifact)
    {
        var sha=mutation=="skip-hash"?RecordingReceiptHash.Digest:hashes.Sha256(mutation=="hash-input"?new byte[]{0}:artifact.Bytes.Span);
        var result=new StoredArtifact(artifact.RelativePath,sha,artifact.Bytes.Length,artifact.MediaType);
        return mutation switch{"describe-path"=>result with{RelativePath="wrong"},"describe-hash"=>result with{Sha256="wrong"},"describe-length"=>result with{Length=-1},"describe-media"=>result with{MediaType="wrong"},_=>result};
    }
    public bool Matches(Artifact artifact,StoredArtifact stored)
    {var result=mutation=="matches-skip-hash"?stored==ArtifactReceiptCases.Expected:stored==Describe(artifact);return mutation=="outcome"?!result:result;}
}
[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class ArtifactReceiptVerifierMockTests
{
    [Theory,MemberData(nameof(ArtifactReceiptCases.Data),MemberType=typeof(ArtifactReceiptCases))]
    public void CorrectReceiptMocksPass(ArtifactReceiptCase test){var hashes=new RecordingReceiptHash();ArtifactReceiptCases.AssertCase(test,new ScriptedArtifactReceiptVerifier(hashes),hashes);}
    [Theory,MemberData(nameof(ArtifactReceiptCases.Data),MemberType=typeof(ArtifactReceiptCases))]
    public void WrongReceiptOutcomeRejected(ArtifactReceiptCase test){var hashes=new RecordingReceiptHash();Assert.NotNull(Record.Exception(()=>ArtifactReceiptCases.AssertCase(test,new ScriptedArtifactReceiptVerifier(hashes,"outcome"),hashes)));}
    [Theory]
    [InlineData("describe-path")][InlineData("describe-hash")][InlineData("describe-length")][InlineData("describe-media")][InlineData("skip-hash")][InlineData("hash-input")][InlineData("matches-skip-hash")]
    public void EachMetadataAndHashInteractionRejectsWrongResponse(string mutation)
    {var test=(ArtifactReceiptCase)ArtifactReceiptCases.Data.First()[0];var hashes=new RecordingReceiptHash();Assert.NotNull(Record.Exception(()=>ArtifactReceiptCases.AssertCase(test,new ScriptedArtifactReceiptVerifier(hashes,mutation),hashes)));}
}
