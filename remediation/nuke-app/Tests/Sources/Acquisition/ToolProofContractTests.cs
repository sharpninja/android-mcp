using System.Text;
using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;
public sealed record ToolProofObservation(StoredArtifact Artifact,ReadOnlyMemory<byte> Bytes);
public interface IToolProofProbe{Task<ToolProofObservation> ReadAsync(bool offline);}
public static class ToolProofCases
{
    public static ToolCase Fixture=>(ToolCase)ToolArtifactCases.All.First(x=>((ToolCase)x[0]).Id=="zip-online")[0];
    public static async Task AssertCase(bool offline,IToolProofProbe provider)
    {
        var result=await provider.ReadAsync(offline);var expected=Fixture.Request.Pin;
        Assert.Equal(ContentHash.Sha256(result.Bytes.Span),result.Artifact.Sha256);Assert.Equal(result.Bytes.Length,result.Artifact.Length);Assert.Equal("application/json",result.Artifact.MediaType);
        Assert.Equal(offline?"proof/retained.json":"tool-checksums/"+result.Artifact.Sha256+".json",result.Artifact.RelativePath);
        var proof=JsonSerializer.Deserialize<ToolChecksumProof>(result.Bytes.Span);Assert.NotNull(proof);Assert.Equal(1,proof!.Version);
        Assert.Equal(expected.Url.AbsoluteUri,proof.ArtifactUrl);Assert.Equal(expected.Sha256,proof.ArtifactSha256);Assert.Equal(expected.SizeBytes,proof.ArtifactSize);Assert.Equal(expected.PublishedChecksumUrl!.AbsoluteUri,proof.ChecksumUrl);
        var raw=Convert.FromBase64String(proof.ResponseBase64);Assert.Equal(ContentHash.Sha256(raw),proof.ResponseSha256);Assert.Equal(expected.Sha256,Encoding.ASCII.GetString(raw).Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries)[0]);
        Assert.Equal(new CanonicalJson().Serialize(result.Bytes),result.Bytes.ToArray());
    }
}
public sealed class ScriptedToolProof(string? mutation=null):IToolProofProbe
{
    public Task<ToolProofObservation> ReadAsync(bool offline)
    {
        var pin=ToolProofCases.Fixture.Request.Pin;var raw=Encoding.ASCII.GetBytes(pin.Sha256+"\n");
        var proof=new ToolChecksumProof(1,pin.Url.AbsoluteUri,pin.Sha256,pin.SizeBytes,pin.PublishedChecksumUrl!.AbsoluteUri,ContentHash.Sha256(raw),Convert.ToBase64String(raw));
        proof=mutation switch{"version"=>proof with{Version=2},"url"=>proof with{ArtifactUrl="wrong"},"pin"=>proof with{ArtifactSha256=new string('0',64)},"size"=>proof with{ArtifactSize=1},"checksum-url"=>proof with{ChecksumUrl="wrong"},"response-hash"=>proof with{ResponseSha256=new string('0',64)},"response-bytes"=>proof with{ResponseBase64=Convert.ToBase64String(Encoding.ASCII.GetBytes(new string('0',64))),ResponseSha256=ContentHash.Sha256(Encoding.ASCII.GetBytes(new string('0',64)))},_=>proof};
        var bytes=mutation=="canonical"?JsonSerializer.SerializeToUtf8Bytes(proof,new JsonSerializerOptions{WriteIndented=true}):new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(proof));var sha=ContentHash.Sha256(bytes);
        var artifact=new StoredArtifact(mutation=="path"?"wrong":offline?"proof/retained.json":"tool-checksums/"+sha+".json",mutation=="hash"?new string('0',64):sha,mutation=="length"?1:bytes.Length,mutation=="media"?"text/plain":"application/json");
        return Task.FromResult(new ToolProofObservation(artifact,bytes));
    }
}
[Trait("Gate","Mock")]
public sealed class ToolProofMockTests
{
    [Theory][InlineData(false)][InlineData(true)]public Task CorrectImmutableProofFields(bool offline)=>ToolProofCases.AssertCase(offline,new ScriptedToolProof());
    [Theory][InlineData(false)][InlineData(true)]public async Task EveryProofFieldAndStoredMetadataMutationRejected(bool offline)
    {foreach(var mutation in new[]{"path","hash","length","media","version","url","pin","size","checksum-url","response-hash","response-bytes","canonical"})Assert.NotNull(await Record.ExceptionAsync(()=>ToolProofCases.AssertCase(offline,new ScriptedToolProof(mutation))));}
}
