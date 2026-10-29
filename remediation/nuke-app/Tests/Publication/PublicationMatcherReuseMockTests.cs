using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Publication;

public sealed record PublicationMatcherCase(string Id,byte[] Policy,PublicationFile[] Files,
    string[] Allow,string[] Deny,int[] AllowPositions,bool[] Denied,bool ForceFalse=false)
{public override string ToString()=>Id;}
public sealed record PublicationMatcherCall(string Text,string Pattern,GlobMatchingProfile Profile,bool Result);

public static class PublicationMatcherReuseCases
{
    public static IEnumerable<object[]> Cases
    {
        get
        {
            using var resource=typeof(PublicationMatcherReuseCases).Assembly.GetManifestResourceStream("PublicationScalePolicy.json")
                ??throw new InvalidDataException("Pinned small publication policy resource unavailable.");
            using var memory=new MemoryStream();resource.CopyTo(memory);var bytes=memory.ToArray();
            Assert.Equal("4ab12829cc0702514981787cdf9cd34b0b2a3b90dc4eb60eac4e286f19eca4a1",ContentHash.Sha256(bytes));
            using var doc=JsonDocument.Parse(bytes);var root=doc.RootElement;
            var allow=root.GetProperty("allow").EnumerateArray().Select(x=>x.GetString()!).ToArray();var deny=root.GetProperty("deny").EnumerateArray().Select(x=>x.GetString()!).ToArray();
            Assert.Equal(392,allow.Length);Assert.Equal(22,deny.Length);
            var position=Array.IndexOf(allow,"remediation/design-c4/docs/screens/*.svg");Assert.Equal(44,position);
            var files=Enumerable.Range(0,849).Select(i=>new PublicationFile($"remediation/design-c4/docs/screens/scale-{i:D4}.svg",1)).ToArray();
            yield return [new PublicationMatcherCase("ROOT-POLICY-849-CONTROLLED-FILES",bytes,files,allow,deny,Enumerable.Repeat(position,849).ToArray(),new bool[849])];
            string[] smallAllow=["dir/*.md","file?.txt","docs/[ab].md"];string[] smallDeny=["**/blocked/**"];
            var smallPolicy=JsonSerializer.SerializeToUtf8Bytes(new{maximum_file_bytes=8,allow=smallAllow,deny=smallDeny});
            yield return [new PublicationMatcherCase("FILENAME-PROFILE-SLASH-AND-BRACKETS",smallPolicy,[new("dir/nested/x.md",1),new("file1.txt",1),new("docs/a.md",1),new("dir/blocked/y.md",1)],smallAllow,smallDeny,[0,1,2,0],[false,false,false,true])];
            yield return [new PublicationMatcherCase("INJECTED-MATCHER-VERDICT-IS-AUTHORITATIVE",smallPolicy,[new("dir/x.md",1),new("docs/a.md",1)],smallAllow,smallDeny,[0,2],[false,false],true)];
        }
    }
    public static PublicationCheckResult Correct(PublicationMatcherCase c)=>new(c.Files.Length,c.Files.Sum(x=>x.Length),
        c.Files.Select((f,i)=>c.ForceFalse?new PublicationIssue("NOT_ALLOWED",f.Path):c.Denied[i]?new PublicationIssue("DENIED",f.Path):null).Where(x=>x is not null).Select(x=>x!).ToArray());
    public static IReadOnlyList<PublicationMatcherCall> ExpectedCalls(PublicationMatcherCase c)
    {
        var calls=new List<PublicationMatcherCall>();
        for(var i=0;i<c.Files.Length;i++)
        {
            var last=c.ForceFalse?c.Allow.Length-1:c.AllowPositions[i];
            for(var a=0;a<=last;a++)calls.Add(new(c.Files[i].Path,c.Allow[a],GlobMatchingProfile.PublicationFilename,!c.ForceFalse&&a==last));
            if(!c.ForceFalse)foreach(var deny in c.Deny)calls.Add(new(c.Files[i].Path,deny,GlobMatchingProfile.PublicationFilename,c.Denied[i]));
        }
        return calls;
    }
    public static void AssertCase(PublicationMatcherCase c,Func<RecordingPublicationMatcher,IPublicationChecker> factory,bool nativeMatcher=false)
    {
        var expected=ExpectedCalls(c);var matcher=new RecordingPublicationMatcher(c,nativeMatcher);var checker=factory(matcher);
        var result=checker.Check(c.Policy,c.Files);var wanted=Correct(c);
        Assert.Equal(wanted.FileCount,result.FileCount);Assert.Equal(wanted.TotalBytes,result.TotalBytes);Assert.Equal(wanted.Issues,result.Issues);
        Assert.Equal(expected,matcher.Calls);
        if(c.Id.StartsWith("ROOT-POLICY",StringComparison.Ordinal))Assert.Equal(56883,matcher.Calls.Count);
    }
}

public sealed class RecordingPublicationMatcher(PublicationMatcherCase c,bool native):IGlobMatcher
{
    private readonly IGlobMatcher engine=new GlobMatcher();
    private readonly Dictionary<string,int> rows=c.Files.Select((f,i)=>(f.Path,i)).ToDictionary(x=>x.Path,x=>x.i,StringComparer.Ordinal);
    public List<PublicationMatcherCall> Calls {get;}=[];
    public bool Matches(string text,string pattern,GlobMatchingProfile profile)
    {
        var index=rows[text];
        var value=!c.ForceFalse && (native?engine.Matches(text,pattern,profile):pattern==c.Allow[c.AllowPositions[index]] || c.Denied[index]&&c.Deny.Contains(pattern,StringComparer.Ordinal));
        Calls.Add(new(text,pattern,profile,value));return value;
    }
}
public sealed class ScriptedPublicationMatcherChecker(PublicationMatcherCase c,IGlobMatcher matcher,string? mutation=null):IPublicationChecker
{
    public PublicationCheckResult Check(ReadOnlyMemory<byte> policy,IReadOnlyList<PublicationFile> files)
    {
        var calls=PublicationMatcherReuseCases.ExpectedCalls(c);
        if(mutation!="bypass")foreach(var call in calls.Take(mutation=="omit"?calls.Count-1:calls.Count))
            matcher.Matches(call.Text,call.Pattern,mutation=="profile"?GlobMatchingProfile.PosixFullPath:call.Profile);
        if(mutation=="extra")matcher.Matches(c.Files[0].Path,c.Allow[0],GlobMatchingProfile.PublicationFilename);
        var correct=PublicationMatcherReuseCases.Correct(c);
        return mutation switch{"count"=>correct with{FileCount=0},"bytes"=>correct with{TotalBytes=-1},"issues"=>correct with{Issues=[new("WRONG","")]},"ignore"=>correct with{Issues=[]},_=>correct};
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class PublicationMatcherReuseMockTests
{
    [Theory,MemberData(nameof(PublicationMatcherReuseCases.Cases),MemberType=typeof(PublicationMatcherReuseCases))]
    public void CorrectSharedMatcherReuseMocksPass(PublicationMatcherCase c)=>PublicationMatcherReuseCases.AssertCase(c,m=>new ScriptedPublicationMatcherChecker(c,m));
    [Theory,MemberData(nameof(PublicationMatcherReuseCases.Cases),MemberType=typeof(PublicationMatcherReuseCases))]
    public void BypassWrongProfileOmissionExtraCallsAndWrongVerdictsAreRejected(PublicationMatcherCase c)
    {var mutations=new List<string>{"bypass","profile","omit","extra","count","bytes","issues"};if(c.ForceFalse)mutations.Add("ignore");foreach(var mutation in mutations)Assert.NotNull(Record.Exception(()=>PublicationMatcherReuseCases.AssertCase(c,m=>new ScriptedPublicationMatcherChecker(c,m,mutation))));}
}
