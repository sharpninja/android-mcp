using System.Text;
using AndroidMcp.BuildTool.Publication;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Publication;

public sealed record PublicationCase(string Id, string Policy, PublicationFile[] Files, long Bytes, PublicationIssue[] Issues);
public static class PublicationContractCases
{
    public const string Policy = "{\"maximum_file_bytes\":8,\"allow\":[\"README.md\",\"src/*.cs\",\"docs/[ab]?.md\"],\"deny\":[\"**/build/**\"]}";
    public static IEnumerable<object[]> Cases => new[]
    {
        new PublicationCase("PUB-01",Policy,[new("README.md",8),new("src/a.cs",2)],10,[]),
        new PublicationCase("PUB-02",Policy,[new("../README.md",1)],1,[new("UNSAFE_PATH","../README.md")]),
        new PublicationCase("PUB-03",Policy,[new("src\\a.cs",1)],1,[new("UNSAFE_PATH","src\\a.cs")]),
        new PublicationCase("PUB-04",Policy,[new("cache.sqlite",1)],1,[new("NOT_ALLOWED","cache.sqlite")]),
        new PublicationCase("PUB-05",Policy,[new("src/build/a.cs",1)],1,[new("DENIED","src/build/a.cs")]),
        new PublicationCase("PUB-06",Policy,[new("README.md",9)],9,[new("TOO_LARGE","README.md")]),
        new PublicationCase("PUB-07",Policy,[new("README.md",1,PublicationFileKind.SymbolicLink)],1,[new("NOT_REGULAR","README.md")]),
        new PublicationCase("PUB-08",Policy,[new("README.md",0,PublicationFileKind.Missing)],0,[new("NOT_REGULAR","README.md")]),
        new PublicationCase("PUB-09",Policy,[new("README.md",1),new("README.md",1)],2,[new("DUPLICATE_PATH","README.md")]),
        new PublicationCase("PUB-10",Policy,[new("README.md",1),new("readme.md",1)],2,[new("PORTABLE_PATH_COLLISION","readme.md"),new("NOT_ALLOWED","readme.md")]),
        new PublicationCase("PUB-11",Policy,[new("docs/a1.md",2)],2,[]),
        new PublicationCase("PUB-12",Policy,[new("/README.md",1)],1,[new("UNSAFE_PATH","/README.md")]),
        new PublicationCase("PUB-13",Policy,[new("src/./a.cs",1)],1,[new("UNSAFE_PATH","src/./a.cs")]),
        new PublicationCase("PUB-14",Policy,[new("src/C:a.cs",1)],1,[new("UNSAFE_PATH","src/C:a.cs")]),
        new PublicationCase("PUB-15",Policy,[new("README.md",-1)],0,[new("INVALID_LENGTH","README.md")]),
        new PublicationCase("PUB-16","{}",[],0,[new("INVALID_POLICY","")]),
        new PublicationCase("PUB-17",Policy,[],0,[])
    }.Select(x=>new object[]{x});

    public static PublicationCheckResult Correct(PublicationCase c)=>new(c.Files.Length,c.Bytes,c.Issues);
    public static void AssertCase(PublicationCase c,IPublicationChecker checker)
    {
        var before=c.Files.ToArray();
        var result=checker.Check(Encoding.UTF8.GetBytes(c.Policy),c.Files);
        Assert.Equal(before,c.Files);
        Assert.Equal(c.Files.Length,result.FileCount);
        Assert.Equal(c.Bytes,result.TotalBytes);
        Assert.Equal(c.Issues.Length==0,result.IsValid);
        Assert.Equal(c.Issues.Select(x=>$"{x.Code}:{x.Path}").Order(StringComparer.Ordinal),result.Issues.Select(x=>$"{x.Code}:{x.Path}").Order(StringComparer.Ordinal));
    }
}
public sealed class ScriptedPublicationChecker(PublicationCheckResult response):IPublicationChecker
{
    public PublicationCheckResult Check(ReadOnlyMemory<byte> policy,IReadOnlyList<PublicationFile> files)=>response;
}
[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class PublicationMockTests
{
    [Theory,MemberData(nameof(PublicationContractCases.Cases),MemberType=typeof(PublicationContractCases))]
    public void CorrectMockPassesAllAssertions(PublicationCase c)=>PublicationContractCases.AssertCase(c,new ScriptedPublicationChecker(PublicationContractCases.Correct(c)));
    [Theory,MemberData(nameof(PublicationContractCases.Cases),MemberType=typeof(PublicationContractCases))]
    public void IndividualCountBytesAndIssueControlsAreRejected(PublicationCase c)
    {
        var correct=PublicationContractCases.Correct(c);
        foreach(var wrong in new[]{correct with{FileCount=correct.FileCount+1},correct with{TotalBytes=correct.TotalBytes+1},correct with{Issues=[new("WRONG","wrong")]}})
            Assert.NotNull(Record.Exception(()=>PublicationContractCases.AssertCase(c,new ScriptedPublicationChecker(wrong))));
        foreach(var issue in c.Issues)
        {
            var omitted=correct with{Issues=correct.Issues.Where(x=>x!=issue).ToArray()};
            Assert.NotNull(Record.Exception(()=>PublicationContractCases.AssertCase(c,new ScriptedPublicationChecker(omitted))));
        }
    }
}
