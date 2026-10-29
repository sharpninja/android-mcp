using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review;

public sealed record CoverageInput(string Text, string? Key = null, int Start = 0, int? End = null,
    string File = "evidence", string FullSha = "whole-file-pin");
public sealed record CoverageRow(string? Key, int Start, int End, string BodySha256);
public sealed record CoverageObservation(IReadOnlyList<CoverageRow> Rows, bool SameAsReference, int ProviderCalls = 0);
public sealed record CoverageCase(string Name, IReadOnlyList<CoverageInput> Inputs, IReadOnlyList<CoverageInput> Reference,
    IReadOnlyList<CoverageRow> Expected, bool Same, int? Partition = null);

public static class ReviewCoverageSignatureCases
{
    public const string Abc = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";
    public const string Abd = "a52d159f262b2c6ddb724a61840befc36eb30c88877a4030b65cbe86298449c9";
    public const string Empty = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
    public const string Unicode = "6e87a40ea7f9e76123c2226f3e98569f9b2ceddbb8b01c80b09b511240dd4b5e";
    public const string Left = "38164fbd17603d73f696b8b4d72664d735bb6a7c88577687fd2ae33fd6964153";
    public const string Right = "90ec58127ec472ffb7e3f90c3ee320f8bb1dc6bc64a48143e6d91f7d9a6de236";
    public static IReadOnlyList<CoverageCase> All { get; } = [
        new("defaults", [new("abc")], [new("abc")], [new(null,0,3,Abc)], true),
        new("empty-text", [new("")], [new("")], [new(null,0,0,Empty)], true),
        new("explicit-zero-end", [new("abc","x",5,0)], [new("abc","x",5,0)], [new("x",5,0,Abc)], true),
        new("default-end-with-start", [new("abc","x",5)], [new("abc","x",5)], [new("x",5,3,Abc)], true),
        new("unicode-default-end", [new("A😀éZ")], [new("A😀éZ")], [new(null,0,4,Unicode)], true),
        new("duplicate-collapse", [new("abc","x",0,3),new("abc","x",0,3)], [new("abc","x",0,3)], [new("x",0,3,Abc)], true),
        new("order-independent", [new("abc","z",0,3),new("abd","a",0,3)], [new("abd","a",0,3),new("abc","z",0,3)], [new("a",0,3,Abd),new("z",0,3,Abc)], true),
        new("changed-key", [new("abc","y",0,3)], [new("abc","x",0,3)], [new("y",0,3,Abc)], false),
        new("changed-start", [new("abc","x",1,3)], [new("abc","x",0,3)], [new("x",1,3,Abc)], false),
        new("changed-end", [new("abc","x",0,2)], [new("abc","x",0,3)], [new("x",0,2,Abc)], false),
        new("changed-body", [new("abd","x",0,3)], [new("abc","x",0,3)], [new("x",0,3,Abd)], false),
        new("unrelated-metadata-ignored", [new("abc","x",0,3,"alias","different-full-pin")], [new("abc","x",0,3)], [new("x",0,3,Abc)], true),
        new("unicode-offsets", [new("A😀éZ","x",7,11)], [new("A😀éZ","x",7,11)], [new("x",7,11,Unicode)], true),
        new("original-left-partition", [new("ABCD","one-artifact",0,4)], [new("ABCD","one-artifact",0,4)], [new("one-artifact",0,2,Left)], false, 0),
        new("original-right-partition", [new("ABCD","one-artifact",0,4)], [new("ABCD","one-artifact",0,4)], [new("one-artifact",2,4,Right)], false, 1),
        new("empty-set", [], [], [], true)
    ];
    public static IEnumerable<object[]> Data => All.Select(x=>new object[]{x.Name});
    public static CoverageCase Find(string name)=>All.Single(x=>x.Name==name);
    public static void AssertContract(CoverageCase item,CoverageObservation actual)
    {
        Assert.Equal(item.Expected.ToArray(),actual.Rows.ToArray());
        Assert.Equal(item.Same,actual.SameAsReference);
        Assert.Equal(0,actual.ProviderCalls);
    }
}

public sealed class ReviewCoverageSignatureMockTests
{
    [Theory,MemberData(nameof(ReviewCoverageSignatureCases.Data),MemberType=typeof(ReviewCoverageSignatureCases))]
    [Trait("Gate","Mock")]
    public void Correct_mock_and_individual_field_controls(string name)
    {
        var item=ReviewCoverageSignatureCases.Find(name);
        var correct=Scripted(name);
        ReviewCoverageSignatureCases.AssertContract(item,correct);
        var wrongRows=correct.Rows.Count==0?new[]{new CoverageRow("unexpected",0,0,ReviewCoverageSignatureCases.Empty)}:Array.Empty<CoverageRow>();
        Assert.ThrowsAny<Exception>(()=>ReviewCoverageSignatureCases.AssertContract(item,correct with{Rows=wrongRows}));
        Assert.ThrowsAny<Exception>(()=>ReviewCoverageSignatureCases.AssertContract(item,correct with{SameAsReference=!correct.SameAsReference}));
        Assert.ThrowsAny<Exception>(()=>ReviewCoverageSignatureCases.AssertContract(item,correct with{ProviderCalls=1}));
        if(correct.Rows.Count>0)
        {
            var row=correct.Rows[0];
            foreach(var wrong in new[]{row with{Key="wrong-key"},row with{Start=row.Start+1},row with{End=row.End+1},row with{BodySha256=new string('0',64)}})
                Assert.ThrowsAny<Exception>(()=>ReviewCoverageSignatureCases.AssertContract(item,correct with{Rows=new[]{wrong}.Concat(correct.Rows.Skip(1)).ToArray()}));
        }
    }
    private static CoverageObservation Scripted(string name)=>name switch
    {
        "defaults"=>new([new(null,0,3,ReviewCoverageSignatureCases.Abc)],true),
        "empty-text"=>new([new(null,0,0,ReviewCoverageSignatureCases.Empty)],true),
        "explicit-zero-end"=>new([new("x",5,0,ReviewCoverageSignatureCases.Abc)],true),
        "default-end-with-start"=>new([new("x",5,3,ReviewCoverageSignatureCases.Abc)],true),
        "unicode-default-end"=>new([new(null,0,4,ReviewCoverageSignatureCases.Unicode)],true),
        "duplicate-collapse"=>new([new("x",0,3,ReviewCoverageSignatureCases.Abc)],true),
        "order-independent"=>new([new("a",0,3,ReviewCoverageSignatureCases.Abd),new("z",0,3,ReviewCoverageSignatureCases.Abc)],true),
        "changed-key"=>new([new("y",0,3,ReviewCoverageSignatureCases.Abc)],false),
        "changed-start"=>new([new("x",1,3,ReviewCoverageSignatureCases.Abc)],false),
        "changed-end"=>new([new("x",0,2,ReviewCoverageSignatureCases.Abc)],false),
        "changed-body"=>new([new("x",0,3,ReviewCoverageSignatureCases.Abd)],false),
        "unrelated-metadata-ignored"=>new([new("x",0,3,ReviewCoverageSignatureCases.Abc)],true),
        "unicode-offsets"=>new([new("x",7,11,ReviewCoverageSignatureCases.Unicode)],true),
        "original-left-partition"=>new([new("one-artifact",0,2,ReviewCoverageSignatureCases.Left)],false),
        "original-right-partition"=>new([new("one-artifact",2,4,ReviewCoverageSignatureCases.Right)],false),
        "empty-set"=>new([],true),
        _=>throw new InvalidOperationException("Unknown coverage fixture")
    };
}
