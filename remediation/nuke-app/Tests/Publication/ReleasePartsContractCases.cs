using System.Text;
using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Publication;

public sealed record ReleaseCodecCase(string Id,string Mode);
public sealed record ReleaseCodecObservation(byte[] SourceBefore,byte[] SourceAfter,byte[]? Output,long? ResultBytes,
    IReadOnlyList<ReleasePart> Parts,IReadOnlyList<Exception> Errors,bool ManifestExists,bool DestinationExists,
    bool GenerationExists,long SourcePayloadReadBytes,byte[]? ExistingBefore,byte[]? ExistingAfter,
    byte[]? ManifestJson,bool MarkerExists,IReadOnlyList<string> WriteOrder,byte[]? RetainedWhole,
    bool RetryRejected,byte[]? FailedPartial);
public interface IReleaseCodecProbe { Task<ReleaseCodecObservation> RunAsync(ReleaseCodecCase c); }
public static class ReleaseCodecCases
{
    // Exact original deterministic payload: bytes(range(256))*31+b'final'.
    public static byte[] Payload=>Enumerable.Range(0,31).SelectMany(_=>Enumerable.Range(0,256).Select(i=>(byte)i))
        .Concat(Encoding.ASCII.GetBytes("final")).ToArray();
    public static byte[] Keep=>Encoding.ASCII.GetBytes("keep");
    public static IEnumerable<object[]> Cases=>new ReleaseCodecCase[]{
        new("PARTS-ORIGINAL-01","roundtrip"),new("PARTS-ORIGINAL-02","tampered-part"),
        new("PARTS-ORIGINAL-03","wrong-whole"),new("PARTS-ORIGINAL-04","tampered-manifest-path"),
        new("PARTS-ORIGINAL-05","existing"),new("PARTS-ORIGINAL-06","symlink-part"),
        new("PARTS-ORIGINAL-07","two-gib"),new("PARTS-ORIGINAL-08","empty"),
        new("PARTS-RETAINED-FINAL-PARTIAL","final-copy-failure"),
        new("PARTS-MARKER-FAILURE","marker-failure"),
        new("PARTS-SOURCE-IDENTITY-CHANGE","identity-change"),
        new("PARTS-CANCEL-BEFORE","cancel-before")
    }.Select(c=>new object[]{c});
    public static string[] Mutations(ReleaseCodecCase c)
    {
        var common=new[]{"source-before","source-after"};
        var specific=c.Mode switch{
            "roundtrip"=>new[]{"output","result-bytes","last-part","manifest-missing","destination-missing","marker-missing","marker-early","retained-whole","manifest-json","part-json"},
            "empty"=>new[]{"output","result-bytes","empty-parts","manifest-missing","destination-missing","marker-missing","marker-early","retained-whole","manifest-json"},
            "existing"=>new[]{"no-error","wrong-error","existing-before","existing-after","destination-missing","marker-present"},
            "two-gib"=>new[]{"no-error","wrong-error","error-message","generation-present","manifest-present","destination-present","payload-read","marker-present"},
            "cancel-before"=>new[]{"no-error","wrong-error","generation-present","manifest-present","destination-present","payload-read","marker-present"},
            "final-copy-failure"=>new[]{"no-error","wrong-error","error-message","destination-missing","partial-missing","retained-whole","marker-present","retry-admitted"},
            "marker-failure"=>new[]{"no-error","wrong-error","error-message","destination-missing","output","retained-whole","marker-present","retry-admitted"},
            "wrong-whole" or "identity-change"=>new[]{"no-error","wrong-error","error-message","manifest-present","destination-present","marker-present"},
            "tampered-manifest-path"=>new[]{"no-error","wrong-error","error-message","error-count","destination-present","marker-present"},
            _=>new[]{"no-error","wrong-error","error-message","destination-present","marker-present"}
        };
        return common.Concat(specific).ToArray();
    }
    public static async Task AssertCase(ReleaseCodecCase c,IReleaseCodecProbe probe)
    {
        var o=await probe.RunAsync(c);var expected=c.Mode=="empty"?Array.Empty<byte>():Payload;
        Assert.Equal(expected,o.SourceBefore);Assert.Equal(expected,o.SourceAfter);
        if(c.Mode is "roundtrip" or "empty"){
            Assert.Empty(o.Errors);Assert.Equal(expected,o.Output);Assert.Equal((long)expected.Length,o.ResultBytes);
            Assert.True(o.ManifestExists);Assert.True(o.DestinationExists);Assert.True(o.MarkerExists);
            Assert.Equal(expected,o.RetainedWhole);
            Assert.Equal("final",o.WriteOrder[^2]);Assert.Equal("success-marker",o.WriteOrder[^1]);
            if(c.Mode=="empty")Assert.Empty(o.Parts);else Assert.InRange(o.Parts[^1].Bytes,1,376);
            Assert.NotNull(o.ManifestJson);
            using var doc=JsonDocument.Parse(o.ManifestJson!);var m=doc.RootElement;
            Assert.Equal("android-mcp-release-parts-v1",m.GetProperty("format").GetString());
            Assert.Equal("sealed.bin",m.GetProperty("artifact_name").GetString());
            Assert.Equal(expected.Length,m.GetProperty("bytes").GetInt64());
            Assert.Equal(ContentHash.Sha256(expected),m.GetProperty("sha256").GetString());
            Assert.Equal(377,m.GetProperty("part_bytes").GetInt64());
            Assert.Equal(o.Parts.Count,m.GetProperty("parts").GetArrayLength());
            for(var i=0;i<o.Parts.Count;i++){
                var row=m.GetProperty("parts")[i];var typed=o.Parts[i];
                Assert.Equal("artifact.part-"+(i+1).ToString("D4",System.Globalization.CultureInfo.InvariantCulture),typed.Name);
                Assert.Equal(typed.Name,row.GetProperty("name").GetString());
                Assert.Equal(typed.Bytes,row.GetProperty("bytes").GetInt64());
                Assert.Equal(typed.Sha256,row.GetProperty("sha256").GetString());
                Assert.Matches("^[a-f0-9]{64}$",typed.Sha256);
            }
            return;
        }
        Assert.False(o.MarkerExists);
        string?[] messages=c.Mode switch{
            "tampered-part"=>new[]{"Part hash mismatch"},"wrong-whole"=>new[]{"trusted sealed hash"},
            "tampered-manifest-path"=>new[]{"Manifest hash mismatch","part path"},
            "existing"=>new[]{"Never overwrite"},"symlink-part"=>new[]{"regular part"},
            "two-gib"=>new[]{"below2GiB"},"identity-change"=>new[]{"Input changed"},
            "final-copy-failure"=>new[]{"copy-fixture"},"marker-failure"=>new[]{"marker-fixture"},
            "cancel-before"=>new string?[]{null},_=>throw new InvalidOperationException("Unknown case")
        };
        Assert.Equal(messages.Length,o.Errors.Count);
        for(var i=0;i<messages.Length;i++){
            if(messages[i] is {} message)Assert.Contains(message,o.Errors[i].Message,StringComparison.Ordinal);
            if(c.Mode=="cancel-before")Assert.IsType<OperationCanceledException>(o.Errors[i]);
            else if(c.Mode is "existing" or "final-copy-failure" or "marker-failure")Assert.IsType<IOException>(o.Errors[i]);
            else Assert.IsType<ReleasePartsException>(o.Errors[i]);
        }
        if(c.Mode=="existing"){
            Assert.True(o.DestinationExists);Assert.Equal(Keep,o.ExistingBefore);Assert.Equal(Keep,o.ExistingAfter);
        }else if(c.Mode is "final-copy-failure" or "marker-failure"){
            Assert.True(o.DestinationExists);Assert.Equal(Payload,o.RetainedWhole);Assert.True(o.RetryRejected);
            if(c.Mode=="final-copy-failure")Assert.Equal(Payload[..123],o.FailedPartial);else Assert.Equal(Payload,o.Output);
        }else Assert.False(o.DestinationExists);
        if(c.Mode is "wrong-whole" or "identity-change" or "two-gib" or "cancel-before")Assert.False(o.ManifestExists);
        if(c.Mode is "two-gib" or "cancel-before"){
            Assert.False(o.GenerationExists);Assert.Equal(0,o.SourcePayloadReadBytes);
        }
    }
}
