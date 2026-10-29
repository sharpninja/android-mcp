using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Publication;

// Declared observations only, not a split/reassembly engine. Native probe will
// drive the shared codec/store/reader seams with the SAME cases and assertions.
public sealed class ScriptedReleaseCodecProbe(string? mutation=null):IReleaseCodecProbe
{
    public Task<ReleaseCodecObservation> RunAsync(ReleaseCodecCase c)
    {
        var payload=c.Mode=="empty"?Array.Empty<byte>():ReleaseCodecCases.Payload;
        var success=c.Mode is "roundtrip" or "empty";
        // Fixed fixture has 21 full377-byte parts plus one24-byte tail, total7941.
        var parts=c.Mode=="empty"?Array.Empty<ReleasePart>():Enumerable.Range(0,22).Select(i=>
            new ReleasePart("artifact.part-"+(i+1).ToString("D4",System.Globalization.CultureInfo.InvariantCulture),
                i==21?24:377,ContentHash.Sha256(payload.AsSpan(i*377,i==21?24:377)))).ToArray();
        var errors=c.Mode switch{
            "roundtrip" or "empty"=>Array.Empty<Exception>(),
            "tampered-manifest-path"=>new Exception[]{new ReleasePartsException("MANIFEST_PIN","Manifest hash mismatch"),new ReleasePartsException("PART_PATH","Invalid part path")},
            "tampered-part"=>new Exception[]{new ReleasePartsException("PART_PIN","Part hash mismatch")},
            "wrong-whole"=>new Exception[]{new ReleasePartsException("WHOLE_PIN","Input differs from trusted sealed hash")},
            "existing"=>new Exception[]{new IOException("Never overwrite an existing artifact")},
            "symlink-part"=>new Exception[]{new ReleasePartsException("PART_KIND","Missing/invalid regular part")},
            "two-gib"=>new Exception[]{new ReleasePartsException("PART_SIZE","Every part must be strictly below2GiB")},
            "identity-change"=>new Exception[]{new ReleasePartsException("SOURCE_CHANGED","Input changed")},
            "final-copy-failure"=>new Exception[]{new IOException("copy-fixture")},
            "marker-failure"=>new Exception[]{new IOException("marker-fixture")},
            "cancel-before"=>new Exception[]{Assert.Throws<OperationCanceledException>(
                ()=>new CancellationToken(canceled:true).ThrowIfCancellationRequested())},
            _=>throw new InvalidOperationException("Unknown case")
        };
        var manifest=JsonSerializer.SerializeToUtf8Bytes(new{
            format="android-mcp-release-parts-v1",artifact_name="sealed.bin",bytes=payload.Length,
            sha256=ContentHash.Sha256(payload),part_bytes=377,
            parts=parts.Select(p=>new{name=p.Name,bytes=p.Bytes,sha256=p.Sha256})
        });
        var o=new ReleaseCodecObservation(payload.ToArray(),payload.ToArray(),success||c.Mode=="marker-failure"?payload.ToArray():null,
            success?payload.LongLength:null,parts,errors,c.Mode is not("wrong-whole" or "identity-change" or "two-gib" or "cancel-before"),
            success||c.Mode is "existing" or "final-copy-failure" or "marker-failure",c.Mode is not("two-gib" or "cancel-before"),
            c.Mode is "two-gib" or "cancel-before"?0:payload.LongLength,c.Mode=="existing"?ReleaseCodecCases.Keep:null,
            c.Mode=="existing"?ReleaseCodecCases.Keep:null,manifest,success,new[]{"retained-whole","final","success-marker"},
            success||c.Mode is "final-copy-failure" or "marker-failure"?payload.ToArray():null,
            c.Mode is "final-copy-failure" or "marker-failure",c.Mode=="final-copy-failure"?payload[..123]:null);
        o=mutation switch{
            "source-before"=>o with{SourceBefore=[255]},"source-after"=>o with{SourceAfter=[255]},
            "output"=>o with{Output=[255]},"result-bytes"=>o with{ResultBytes=999},
            "last-part"=>o with{Parts=o.Parts.Take(o.Parts.Count-1).Append(o.Parts[^1] with{Bytes=377}).ToArray()},
            "empty-parts"=>o with{Parts=new[]{new ReleasePart("artifact.part-0001",1,new('0',64))}},
            "manifest-missing"=>o with{ManifestExists=false},"manifest-present"=>o with{ManifestExists=true},
            "destination-missing"=>o with{DestinationExists=false},"destination-present"=>o with{DestinationExists=true},
            "marker-missing"=>o with{MarkerExists=false},"marker-present"=>o with{MarkerExists=true},
            "marker-early"=>o with{WriteOrder=new[]{"success-marker","retained-whole","final"}},
            "retained-whole"=>o with{RetainedWhole=[255]},"manifest-json"=>o with{ManifestJson="{}"u8.ToArray()},
            "part-json"=>o with{Parts=o.Parts.Select(p=>p with{Sha256=new('0',64)}).ToArray()},
            "no-error"=>o with{Errors=Array.Empty<Exception>()},"wrong-error"=>o with{Errors=o.Errors.Select(e=>(Exception)new FormatException(e.Message)).ToArray()},
            "error-message"=>o with{Errors=o.Errors.Select(e=>e is IOException?(Exception)new IOException("wrong-message"):
                e is OperationCanceledException?(Exception)new OperationCanceledException("wrong-message"):new ReleasePartsException("WRONG","wrong-message")).ToArray()},
            "error-count"=>o with{Errors=o.Errors.Take(1).ToArray()},
            "existing-before"=>o with{ExistingBefore=[255]},"existing-after"=>o with{ExistingAfter=[255]},
            "generation-present"=>o with{GenerationExists=true},"payload-read"=>o with{SourcePayloadReadBytes=1},
            "partial-missing"=>o with{FailedPartial=null},"retry-admitted"=>o with{RetryRejected=false},_=>o
        };
        return Task.FromResult(o);
    }
}

[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-007")]
public sealed class ReleaseCodecMockTests
{
    [Theory,MemberData(nameof(ReleaseCodecCases.Cases),MemberType=typeof(ReleaseCodecCases))]
    public Task CorrectOriginalAndRetainedCommitCasesPass(ReleaseCodecCase c)=>ReleaseCodecCases.AssertCase(c,new ScriptedReleaseCodecProbe());
    [Theory,MemberData(nameof(ReleaseCodecCases.Cases),MemberType=typeof(ReleaseCodecCases))]
    public async Task IndividualWrongResultsAndEffectsAreRejected(ReleaseCodecCase c)
    {
        foreach(var mutation in ReleaseCodecCases.Mutations(c))
            Assert.NotNull(await Record.ExceptionAsync(()=>ReleaseCodecCases.AssertCase(c,new ScriptedReleaseCodecProbe(mutation))));
    }
}
