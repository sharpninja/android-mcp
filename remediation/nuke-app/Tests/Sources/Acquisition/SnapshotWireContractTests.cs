using System.Text;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;

public sealed record SnapshotWireCase(string Id,string Mode,byte[] Wire,byte[] Expected,string? Error=null);
public sealed record SnapshotWireObservation(string Mode,byte[] Input,byte[] Output,string? Error);
public interface ISnapshotWireProbe { Task<SnapshotWireObservation> DecodeAsync(string mode,byte[] wire); }
public static class SnapshotWireCases
{
    private static byte[] Bytes(string value)=>Encoding.ASCII.GetBytes(value);
    public static IEnumerable<object[]> All
    {
        get
        {
            var cases=new List<SnapshotWireCase>();
            void Add(string id,byte[] wire,string? restore,string? fetch)
            {
                cases.Add(new(id+"-restore","Restore",wire,restore is null?[]:Bytes(restore),restore is null?"BASE64":null));
                cases.Add(new(id+"-fetch","Fetch",wire,fetch is null?[]:Bytes(fetch),fetch is null?"BASE64":null));
            }
            Add("empty",[],"","");Add("quartet",Bytes("YWJj"),"abc","abc");
            Add("two-padding",Bytes("YQ=="),"a","a");Add("one-padding",Bytes("YWI="),"ab","ab");
            Add("unused-four-bits",Bytes("YR=="),"a","a");Add("unused-two-bits",Bytes("YWJ="),"ab","ab");
            Add("whitespace",Bytes(" \tYWJj\r\n"),null,"abc");Add("junk",Bytes("YW!Jj"),null,"abc");
            Add("high-byte",[255],null,"");Add("high-byte-between",[89,87,255,74,106],null,"abc");
            Add("leading-pad",Bytes("=YWJj"),null,"abc");Add("excess-quartet-pad",Bytes("YWJj="),null,"abc");
            Add("excess-pair-pad",Bytes("YQ==="),null,"a");Add("newline-after-pad",Bytes("YQ==\n"),null,"a");
            Add("data-after-pad",Bytes("YQ==YWJj"),null,"a");Add("interior-pad",Bytes("Y=Q=="),null,"a");
            Add("one-data-character",Bytes("Y"),null,null);Add("missing-two-pads",Bytes("YQ"),null,null);
            Add("missing-one-pad",Bytes("YWJ"),null,null);Add("incomplete-pad",Bytes("YQ="),null,null);
            Add("only-padding",Bytes("===="),null,"");Add("ignored-quartet-padding",Bytes("YWJj====ZGVm"),null,"abcdef");
            Add("junk-in-padding",Bytes("YQ=!=trailing"),null,"a");Add("incomplete-pad-with-junk",Bytes("YQ=!"),null,null);
            Add("leading-many-pads",Bytes("===YWJj==="),null,"abc");Add("pad-between-quartets",Bytes("YWJj=ZGVm"),null,"abcdef");
            Add("discontinuous-padded-data",Bytes("YQ=A="),null,"a\0");
            cases.AddRange(new[]{
                new SnapshotWireCase("generic-quartet","Generic",Bytes("YWJj"),Bytes("abc")),
                new SnapshotWireCase("generic-whitespace","Generic",Bytes(" \tY\nW\vJ\fj\r "),Bytes("abc")),
                new SnapshotWireCase("generic-padding-newline","Generic",Bytes("YQ==\n"),Bytes("a")),
                new SnapshotWireCase("generic-junk","Generic",Bytes("YW!Jj"),[],"BASE64"),
                new SnapshotWireCase("generic-high-byte","Generic",[255],[],"BASE64"),
                new SnapshotWireCase("generic-excess-pad","Generic",Bytes("YQ==="),[],"BASE64"),
                new SnapshotWireCase("generic-missing-pad","Generic",Bytes("YQ"),[],"BASE64"),
                new SnapshotWireCase("generic-empty","Generic",[],[])
            });
            return cases.Select(item=>new object[]{item});
        }
    }
    public static async Task AssertCase(SnapshotWireCase test,ISnapshotWireProbe probe)
    {
        var wire=test.Wire.ToArray();var before=wire.ToArray();var actual=await probe.DecodeAsync(test.Mode,wire);
        Assert.Equal(before,wire);Assert.Equal(test.Mode,actual.Mode);Assert.Equal(before,actual.Input);
        Assert.Equal(test.Error,actual.Error);Assert.Equal(test.Expected,actual.Output);
    }
}
public sealed class ScriptedSnapshotWireProbe(SnapshotWireCase test,string? mutation=null):ISnapshotWireProbe
{
    public Task<SnapshotWireObservation> DecodeAsync(string mode,byte[] wire)
    {
        Assert.Equal(test.Mode,mode);Assert.Equal(test.Wire,wire);var input=wire.ToArray();
        if(mutation=="mutate"&&wire.Length>0)wire[0]^=1;
        return Task.FromResult(new SnapshotWireObservation(mutation=="mode"?"WRONG":mode,
            mutation=="input"?input.Append((byte)0).ToArray():input,mutation=="output"?new byte[]{1}:test.Expected,
            mutation=="error"?"WRONG":test.Error));
    }
}
[Trait("Gate","Mock")]
public sealed class SnapshotWireMockTests
{
    [Theory,MemberData(nameof(SnapshotWireCases.All),MemberType=typeof(SnapshotWireCases))]
    public Task CorrectRuntimeSourcedWireCases(SnapshotWireCase test)=>SnapshotWireCases.AssertCase(test,new ScriptedSnapshotWireProbe(test));
    [Theory,MemberData(nameof(SnapshotWireCases.All),MemberType=typeof(SnapshotWireCases))]
    public async Task ExactInputModeOutputAndErrorControlsDiscriminate(SnapshotWireCase test)
    {
        foreach(var mutation in new[]{"mode","input","output","error"}.Concat(test.Wire.Length>0?new[]{"mutate"}:[]))
            Assert.NotNull(await Record.ExceptionAsync(()=>SnapshotWireCases.AssertCase(test,new ScriptedSnapshotWireProbe(test,mutation))));
    }
}
