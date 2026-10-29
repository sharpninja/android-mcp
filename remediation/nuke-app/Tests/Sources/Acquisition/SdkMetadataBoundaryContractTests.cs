using System.Text;
using AndroidMcp.BuildTool.Core;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;
public sealed record MetadataRangeAction(string Kind,int Value,int Origin=0,bool Continue=false);
public sealed record MetadataRangeCase(string Id,int Length,MetadataRangeAction[] Actions,long Position,string[] Chunks,ByteRange[] Reads,string[] Errors,string Mode="normal")
{public override string ToString()=>Id;}
public sealed record MetadataRangeObservation(long Position,long Length,bool Readable,bool Seekable,bool Writable,string[] Chunks,ByteRange[] Reads,string[] Errors,bool Retained,bool OriginalError);
public interface IMetadataRangeBoundaryProbe{MetadataRangeObservation Run(MetadataRangeCase c);}
public static class SdkMetadataRangeCases
{
    public const int Limit=8*1024*1024;
    public static MetadataRangeAction S(int value,int origin=0,bool next=false)=>new("seek",value,origin,next);
    public static MetadataRangeAction R(int value,bool next=false)=>new("read",value,Continue:next);
    public static string Chunk(ReadOnlySpan<byte> bytes)=>bytes.Length+":"+ContentHash.Sha256(bytes);
    public static byte[] Data(int length)=>length==8?Encoding.ASCII.GetBytes("01234567"):Enumerable.Repeat((byte)'a',length).ToArray();
    public static IEnumerable<object[]> Cases=>new[]{
        new MetadataRangeCase("constructor-getters",8,[],0,[],[],[]),
        new MetadataRangeCase("seek-start",8,[S(3)],3,[],[],[]),
        new MetadataRangeCase("seek-current",8,[S(2),S(3,1)],5,[],[],[]),
        new MetadataRangeCase("seek-end",8,[S(-2,2)],6,[],[],[]),
        new MetadataRangeCase("seek-exact-end",8,[S(0,2),R(99)],8,[Chunk([])],[],[]),
        new MetadataRangeCase("read-default-remaining",8,[R(-1)],8,[Chunk("01234567"u8)],[new(0,8)],[]),
        new MetadataRangeCase("read-other-negative-remaining",8,[S(3),R(-2)],8,[Chunk("34567"u8)],[new(3,5)],[]),
        new MetadataRangeCase("read-zero-no-callback",8,[R(0)],0,[Chunk([])],[],[]),
        new MetadataRangeCase("read-clamps-at-end",8,[S(6),R(99)],8,[Chunk("67"u8)],[new(6,2)],[]),
        new MetadataRangeCase("sequential-read-position",8,[R(3),R(2)],5,[Chunk("012"u8),Chunk("34"u8)],[new(0,3),new(3,2)],[]),
        new MetadataRangeCase("negative-seek-assigns-before-refusal",8,[S(-1)],-1,[],[],["ZIP_SEEK"]),
        new MetadataRangeCase("past-end-seek-assigns-before-refusal",8,[S(9)],9,[],[],["ZIP_SEEK"]),
        new MetadataRangeCase("current-negative-seek-state",8,[S(2),S(-3,1)],-1,[],[],["ZIP_SEEK"]),
        new MetadataRangeCase("end-past-limit-state",8,[S(1,2)],9,[],[],["ZIP_SEEK"]),
        new MetadataRangeCase("recover-after-negative-seek",8,[S(-1,next:true),S(2),R(2)],4,[Chunk("23"u8)],[new(2,2)],["ZIP_SEEK"]),
        new MetadataRangeCase("recover-after-beyond-end-seek",8,[S(9,next:true),S(-1,2),R(2)],8,[Chunk("7"u8)],[new(7,1)],["ZIP_SEEK"]),
        new MetadataRangeCase("invalid-position-read-fails-closed",8,[S(-1,next:true),R(2)],-1,[],[],["ZIP_SEEK","ZIP_SEEK"]),
        new MetadataRangeCase("short-callback-keeps-position",8,[R(3)],0,[],[new(0,3)],["BODY_LENGTH"],"short"),
        new MetadataRangeCase("callback-exception-keeps-position",8,[R(3)],0,[],[new(0,3)],["IO"],"io"),
        new MetadataRangeCase("exact-read-bound-accepted",Limit,[R(Limit)],Limit,[Chunk(Data(Limit))],[new(0,Limit)],[]),
        new MetadataRangeCase("one-over-read-bound-refused",Limit+1,[R(Limit+1)],0,[],[],["RANGE_LIMIT"]),
        new MetadataRangeCase("read-all-over-bound-refused",Limit+1,[R(-1)],0,[],[],["RANGE_LIMIT"]),
        new MetadataRangeCase("empty-stream-read",0,[R(-1)],0,[Chunk([])],[],[])
    }.Select(c=>new object[]{c});
    public static MetadataRangeObservation Expected(MetadataRangeCase c)=>new(c.Position,c.Length,true,true,false,c.Chunks,c.Reads,c.Errors,true,true);
    public static void AssertCase(MetadataRangeCase c,IMetadataRangeBoundaryProbe probe)
    {var actual=probe.Run(c);var expected=Expected(c);Assert.Equal(expected.Position,actual.Position);Assert.Equal(expected.Length,actual.Length);Assert.True(actual.Readable);Assert.True(actual.Seekable);Assert.False(actual.Writable);Assert.Equal(expected.Chunks,actual.Chunks);Assert.Equal(expected.Reads,actual.Reads);Assert.Equal(expected.Errors,actual.Errors);Assert.True(actual.Retained);Assert.True(actual.OriginalError);}
}
public sealed class ScriptedMetadataRangeBoundary(string? mutation=null):IMetadataRangeBoundaryProbe
{
    public MetadataRangeObservation Run(MetadataRangeCase c){var v=SdkMetadataRangeCases.Expected(c);return mutation switch{"position"=>v with{Position=999},"length"=>v with{Length=999},"readable"=>v with{Readable=false},"seekable"=>v with{Seekable=false},"writable"=>v with{Writable=true},"chunks"=>v with{Chunks=["wrong"]},"ranges"=>v with{Reads=[new(99,99)]},"errors"=>v with{Errors=["wrong"]},"retained"=>v with{Retained=false},"exception"=>v with{OriginalError=false},_=>v};}
}
public sealed record MetadataLeafCase(string Path,bool Selected);
public interface IMetadataLeafProbe{bool Selected(string path);}
public static class SdkMetadataLeafCases
{
    public static readonly string[] Leaves=["source.properties","sdk.properties","package.xml","api-versions.xml","api-versions-versioned.xml","annotations.zip","public.xml","attrs_manifest.xml","optional.json","AndroidManifest.xml"];
    public static IEnumerable<object[]> Cases=>Leaves.SelectMany(leaf=>new[]{new MetadataLeafCase(leaf,true),new("nested/"+leaf,true),new(leaf.ToUpperInvariant(),false)})
        .Concat(new[]{new MetadataLeafCase("source.properties/",false),new("prefix-source.properties",false),new("source.properties.extra",false),new("folder\\source.properties",false),new("../source.properties",true),new("android.jar",false)}).Select(c=>new object[]{c});
    public static void AssertCase(MetadataLeafCase c,IMetadataLeafProbe probe)=>Assert.Equal(c.Selected,probe.Selected(c.Path));
}
public sealed class ScriptedMetadataLeaf(bool selected):IMetadataLeafProbe{public bool Selected(string path)=>selected;}
public sealed record MetadataArchiveBoundaryCase(string Id,string Shape,string? Error,string[] Paths,bool StrictNativePolicy=false)
{public override string ToString()=>Id;}
public sealed record MetadataArchiveBoundaryObservation(string? Error,string[] Paths,string[] Payloads,string[] Entries,bool InputsRetained,bool Bounds,bool UnselectedPayloadRead,bool WholeIdentity,string Details="");
public interface IMetadataArchiveBoundaryProbe{MetadataArchiveBoundaryObservation Run(MetadataArchiveBoundaryCase c);}
public static class SdkMetadataArchiveCases
{
    public static IEnumerable<object[]> Cases=>new[]{
        new MetadataArchiveBoundaryCase("selected-all-leaves-and-entry-inventory","all",null,SdkMetadataLeafCases.Leaves.Select(p=>"nested/"+p).ToArray()),
        new MetadataArchiveBoundaryCase("selected-crc-refused","crc","ZIP_CRC",[]),
        new MetadataArchiveBoundaryCase("selected-over32MiB-refused","uncompressed","METADATA_LIMIT",[]),
        new MetadataArchiveBoundaryCase("selected-over8MiB-compressed-refused","compressed","METADATA_LIMIT",[]),
        new MetadataArchiveBoundaryCase("short-range-refused","short","BODY_LENGTH",[]),
        new MetadataArchiveBoundaryCase("directory-not-an-entry","directory",null,[]),
        new MetadataArchiveBoundaryCase("empty-archive","empty",null,[]),
        new MetadataArchiveBoundaryCase("empty-selected-member","zero",null,["data/api-versions.xml"]),
        new MetadataArchiveBoundaryCase("duplicate-selected-native-retention-policy","duplicate","ARCHIVE_DUPLICATE",[],true),
        new MetadataArchiveBoundaryCase("unsafe-selected-native-path-policy","unsafe-selected","PATH",[],true),
        new MetadataArchiveBoundaryCase("unsafe-unselected-native-path-policy","unsafe-unselected","PATH",[],true)
    }.Select(c=>new object[]{c});
    public static string[] Payloads(MetadataArchiveBoundaryCase c)=>c.Paths.Select(p=>p+":"+(c.Shape=="zero"?SdkMetadataRangeCases.Chunk([]):SdkMetadataRangeCases.Chunk("abc"u8))).ToArray();
    public static string[] Entries(MetadataArchiveBoundaryCase c)=>c.Error is not null?[]:c.Shape=="all"?new[]{"android.jar:170000:170000:False"}.Concat(c.Paths.Select(p=>p+":3:3:True")).ToArray():c.Shape=="zero"?["data/api-versions.xml:0:0:True"]:[];
    public static void AssertCase(MetadataArchiveBoundaryCase c,IMetadataArchiveBoundaryProbe probe)
    {var a=probe.Run(c);Assert.Equal(c.Error,a.Error);Assert.Equal(c.Paths,a.Paths);Assert.Equal(Payloads(c),a.Payloads);Assert.Equal(Entries(c),a.Entries);Assert.True(a.InputsRetained);Assert.True(a.Bounds);Assert.DoesNotContain("android.jar",a.Paths);Assert.False(a.WholeIdentity);}
}
public sealed class ScriptedMetadataArchiveBoundary(string? mutation=null):IMetadataArchiveBoundaryProbe
{public MetadataArchiveBoundaryObservation Run(MetadataArchiveBoundaryCase c)=>new(mutation=="error"?"wrong":c.Error,mutation=="paths"?["wrong"]:mutation=="binary"?["android.jar"]:c.Paths,mutation=="bytes"?["wrong"]:SdkMetadataArchiveCases.Payloads(c),mutation=="entries"?["wrong"]:SdkMetadataArchiveCases.Entries(c),mutation!="retained",mutation!="bounds",mutation=="binary",mutation=="identity");}
[Trait("Gate","Mock")]
public sealed class SdkMetadataBoundaryMockTests
{
    [Theory,MemberData(nameof(SdkMetadataRangeCases.Cases),MemberType=typeof(SdkMetadataRangeCases))]public void CorrectRangeMethodBoundaries(MetadataRangeCase c)=>SdkMetadataRangeCases.AssertCase(c,new ScriptedMetadataRangeBoundary());
    [Theory,MemberData(nameof(SdkMetadataRangeCases.Cases),MemberType=typeof(SdkMetadataRangeCases))]public void IndependentRangeStateReadAndErrorControls(MetadataRangeCase c){foreach(var m in new[]{"position","length","readable","seekable","writable","chunks","ranges","errors","retained","exception"})Assert.NotNull(Record.Exception(()=>SdkMetadataRangeCases.AssertCase(c,new ScriptedMetadataRangeBoundary(m))));}
    [Theory,MemberData(nameof(SdkMetadataLeafCases.Cases),MemberType=typeof(SdkMetadataLeafCases))]public void CorrectExactAllowlist(MetadataLeafCase c)=>SdkMetadataLeafCases.AssertCase(c,new ScriptedMetadataLeaf(c.Selected));
    [Theory,MemberData(nameof(SdkMetadataLeafCases.Cases),MemberType=typeof(SdkMetadataLeafCases))]public void OppositeLeafDiscriminates(MetadataLeafCase c)=>Assert.NotNull(Record.Exception(()=>SdkMetadataLeafCases.AssertCase(c,new ScriptedMetadataLeaf(!c.Selected))));
    [Theory,MemberData(nameof(SdkMetadataArchiveCases.Cases),MemberType=typeof(SdkMetadataArchiveCases))]public void CorrectArchiveErrorAndInventoryBoundaries(MetadataArchiveBoundaryCase c)=>SdkMetadataArchiveCases.AssertCase(c,new ScriptedMetadataArchiveBoundary());
    [Theory,MemberData(nameof(SdkMetadataArchiveCases.Cases),MemberType=typeof(SdkMetadataArchiveCases))]public void IndependentArchiveBoundaryControls(MetadataArchiveBoundaryCase c){foreach(var m in new[]{"error","paths","bytes","entries","retained","bounds","binary","identity"})Assert.NotNull(Record.Exception(()=>SdkMetadataArchiveCases.AssertCase(c,new ScriptedMetadataArchiveBoundary(m))));}
}
