using System.Text;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Publication;

// Original reassembler numeric behavior, not a new production planner/API.
// Positive cases stop at an injected dependency boundary: they do not claim a
// complete reassembly, write bytes, or allocate a declared payload.
public sealed record ReleaseNumericCase(string Id,string PartToken,string WholeToken,bool Empty=false,string? ErrorCode=null);
public sealed record ReleaseNumericObservation(Exception Error,Exception BoundaryError,string? Boundary,
    int ControlReads,int PayloadOpens,int StreamWriteAttempts,int ByteWrites,int Processes,bool OriginalControlPreserved);
public interface IReleaseNumericProbe
{
    Task<ReleaseNumericObservation> RunAsync(ReleaseNumericCase test);
}
public static class ReleaseNumericCases
{
    public static IEnumerable<object[]> Cases=>new ReleaseNumericCase[]{
        new("NUMERIC-PART-BOOL-TRUE","true","1"),
        new("NUMERIC-PART-BOOL-FALSE","false","0",ErrorCode:"PART_SIZE"),
        new("NUMERIC-PART-INTEGRAL-FLOAT","1.0","1",ErrorCode:"PART_SIZE"),
        new("NUMERIC-WHOLE-BOOL-TRUE","1","true"),
        new("NUMERIC-WHOLE-BOOL-FALSE","1","false",Empty:true),
        new("NUMERIC-WHOLE-INTEGRAL-FLOAT","1","1.0"),
        new("NUMERIC-WHOLE-EXPONENT","1","1e0"),
        new("NUMERIC-WHOLE-FRACTION","1","1.5",ErrorCode:"WHOLE_PIN"),
        new("NUMERIC-WHOLE-STRING","1","\"1\"",ErrorCode:"WHOLE_PIN")
    }.Select(test=>new object[]{test});

    public static byte[] Raw(ReleaseNumericCase test)
    {
        var hash=test.Empty?ContentHash.Sha256(ReadOnlySpan<byte>.Empty):ContentHash.Sha256("a"u8);
        var parts=test.Empty?"[]":"[{\"name\":\"artifact.part-0001\",\"bytes\":"+test.PartToken+",\"sha256\":\""+hash+"\"}]";
        return Encoding.UTF8.GetBytes("{\"format\":\"android-mcp-release-parts-v1\",\"bytes\":"+test.WholeToken+
            ",\"sha256\":\""+hash+"\",\"parts\":"+parts+"}");
    }
    public static string[] Mutations(ReleaseNumericCase test)
    {
        var common=new[]{"control-reread","byte-write","process","control-mutated"};
        return common.Concat(test.ErrorCode is null
            ?new[]{"reject-admitted","replace-boundary-error","wrong-boundary","no-boundary","admit-success"}
            :new[]{"admit-rejected","wrong-domain-code","wrong-error-type","payload-on-rejected","write-on-rejected"}).ToArray();
    }
    public static async Task AssertCase(ReleaseNumericCase test,IReleaseNumericProbe probe)
    {
        var result=await probe.RunAsync(test);
        Assert.Equal(1,result.ControlReads);Assert.Equal(0,result.ByteWrites);Assert.Equal(0,result.Processes);
        Assert.True(result.OriginalControlPreserved);
        if(test.ErrorCode is {} code){
            Assert.Equal(code,Assert.IsType<ReleasePartsException>(result.Error).Code);
            Assert.Null(result.Boundary);Assert.Equal(0,result.PayloadOpens);Assert.Equal(0,result.StreamWriteAttempts);
        }else{
            // The exact opaque dependency error must survive. Neither the
            // probe nor production can wrap/rewrite it to simulate admission.
            Assert.Same(result.BoundaryError,result.Error);
            Assert.Equal(test.Empty?"empty-whole-write":"part-open",result.Boundary);
            Assert.Equal(test.Empty?0:1,result.PayloadOpens);Assert.Equal(test.Empty?1:0,result.StreamWriteAttempts);
        }
    }
}

// Declared response/effect model only. Production codec is never called here;
// SAME native bindings will exercise it after these18 mocks pass.
public sealed class ScriptedReleaseNumericProbe(string? mutation=null):IReleaseNumericProbe
{
    public Task<ReleaseNumericObservation> RunAsync(ReleaseNumericCase test)
    {
        var boundary=new NotSupportedException("numeric-admission-dependency-boundary");
        var accepted=test.ErrorCode is null;
        var result=new ReleaseNumericObservation(accepted?boundary:new ReleasePartsException(test.ErrorCode!,"domain-fixture"),
            boundary,accepted?(test.Empty?"empty-whole-write":"part-open"):null,
            1,accepted&&!test.Empty?1:0,accepted&&test.Empty?1:0,0,0,true);
        result=mutation switch{
            "control-reread"=>result with{ControlReads=2},"byte-write"=>result with{ByteWrites=1},
            "process"=>result with{Processes=1},"control-mutated"=>result with{OriginalControlPreserved=false},
            "reject-admitted"=>result with{Error=new ReleasePartsException("WRONG_REJECTION","wrong")},
            "replace-boundary-error"=>result with{Error=new NotSupportedException(boundary.Message)},
            "wrong-boundary"=>result with{Boundary=test.Empty?"part-open":"empty-whole-write"},
            "no-boundary"=>result with{Boundary=null},"admit-success"=>result with{Error=null!},
            "admit-rejected"=>result with{Error=boundary,Boundary="part-open",PayloadOpens=1},
            "wrong-domain-code"=>result with{Error=new ReleasePartsException("WRONG_CODE","wrong")},
            "wrong-error-type"=>result with{Error=new FormatException("wrong")},
            "payload-on-rejected"=>result with{PayloadOpens=1},"write-on-rejected"=>result with{StreamWriteAttempts=1},_=>result
        };
        return Task.FromResult(result);
    }
}

[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-007")]
public sealed class ReleaseManifestNumericMockTests
{
    [Theory,MemberData(nameof(ReleaseNumericCases.Cases),MemberType=typeof(ReleaseNumericCases))]
    public Task CorrectOriginalNumericAdmissionMocksPass(ReleaseNumericCase test)=>
        ReleaseNumericCases.AssertCase(test,new ScriptedReleaseNumericProbe());

    [Theory,MemberData(nameof(ReleaseNumericCases.Cases),MemberType=typeof(ReleaseNumericCases))]
    public async Task IndividualWrongNumericAdmissionAndEffectsAreRejected(ReleaseNumericCase test)
    {
        foreach(var mutation in ReleaseNumericCases.Mutations(test))
            Assert.NotNull(await Record.ExceptionAsync(()=>ReleaseNumericCases.AssertCase(test,new ScriptedReleaseNumericProbe(mutation))));
    }
}
