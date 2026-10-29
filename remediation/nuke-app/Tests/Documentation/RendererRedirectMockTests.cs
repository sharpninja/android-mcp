using System.Text;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public static class RendererRedirectCases
{
    public const string Gradle=".tools/gradle-8.14.3-bin.zip";
    public static readonly Uri Observed=new("https://downloads.gradle.org/distributions/gradle-8.14.3-bin.zip.sha256");
    public static IReadOnlyDictionary<string,IReadOnlyList<Uri>> Allowlist(params Uri[] urls)=>new Dictionary<string,IReadOnlyList<Uri>>(StringComparer.Ordinal){{Gradle,urls}};
    public static IEnumerable<object[]> Data
    {
        get
        {
            var original=RendererProfileCases.Original();
            RendererProfileCase Modify(string id,Action<JsonObject> mutation,string error,IReadOnlyDictionary<string,IReadOnlyList<Uri>>? expected=null)
            {var json=JsonNode.Parse(original.Toolchain.Bytes.Span)!.AsObject();mutation(json);return original with{Id=id,Toolchain=original.Toolchain with{Bytes=Encoding.UTF8.GetBytes(json.ToJsonString())},Expected=original.Expected with{ToolRedirects=expected},Error=error};}
            var exact=Modify("REDIRECT-OBSERVED",root=>root["files"]![0]!["allowed_redirects"]=new JsonArray(Observed.AbsoluteUri),"",Allowlist(Observed));
            var raw=Encoding.UTF8.GetString(original.Toolchain.Bytes.Span);var renderer=Encoding.UTF8.GetString(original.Renderer.Bytes.Span);
            return new[]
            {
                original with{Id="REDIRECT-DEFAULT-NONE"},exact,
                Modify("REDIRECT-EMPTY",root=>root["files"]![0]!["allowed_redirects"]=new JsonArray(),"",Allowlist()),
                Modify("REDIRECT-DUPLICATE",root=>root["files"]![0]!["allowed_redirects"]=new JsonArray(Observed.AbsoluteUri,Observed.AbsoluteUri),"TOOLCHAIN_PIN"),
                Modify("REDIRECT-HTTP",root=>root["files"]![0]!["allowed_redirects"]=new JsonArray("http://downloads.gradle.org/file"),"TOOLCHAIN_PIN"),
                Modify("REDIRECT-CREDENTIALS",root=>root["files"]![0]!["allowed_redirects"]=new JsonArray("https://fixture-user:fixture-password@downloads.gradle.org/file"),"TOOLCHAIN_PIN"),
                Modify("REDIRECT-RELATIVE",root=>root["files"]![0]!["allowed_redirects"]=new JsonArray("/file"),"TOOLCHAIN_PIN"),
                Modify("REDIRECT-NONARRAY",root=>root["files"]![0]!["allowed_redirects"]=Observed.AbsoluteUri,"TOOLCHAIN_PIN"),
                Modify("REDIRECT-NONSTRING",root=>root["files"]![0]!["allowed_redirects"]=new JsonArray(17),"TOOLCHAIN_PIN"),
                Modify("REDIRECT-UNKNOWN-ARTIFACT",root=>{root["files"]![0]!["file"]=".tools/unknown.zip";root["files"]![0]!["allowed_redirects"]=new JsonArray(Observed.AbsoluteUri);},"TOOLCHAIN_PIN"),
                original with{Id="REDIRECT-DUPLICATE-JSON-TOOLCHAIN",Toolchain=original.Toolchain with{Bytes=Encoding.UTF8.GetBytes(raw.Replace("\"gradle\": \"8.14.3\"","\"gradle\": \"8.14.3\",\"gradle\": \"8.14.3\"",StringComparison.Ordinal))},Error="TOOLCHAIN_PIN"},
                original with{Id="REDIRECT-DUPLICATE-JSON-RENDERER",Renderer=original.Renderer with{Bytes=Encoding.UTF8.GetBytes(renderer.Replace("\"java_version\": \"21.0.8\"","\"java_version\": \"21.0.8\",\"java_version\": \"21.0.8\"",StringComparison.Ordinal))},Error="RENDERER_PIN"}
            }.Select(test=>new object[]{test});
        }
    }
    public static void AssertReader(RendererProfileCase test,IDocumentationRendererProfileReader provider)
    {
        RendererProfileCases.AssertProfile(test,provider);if(test.Error.Length>0)return;
        var actual=provider.Read(test.Toolchain,test.Renderer).ToolRedirects;AssertMap(test.Expected.ToolRedirects,actual);
    }
    public static void AssertMap(IReadOnlyDictionary<string,IReadOnlyList<Uri>>? expected,IReadOnlyDictionary<string,IReadOnlyList<Uri>>? actual)
    {
        if(expected is null){Assert.Null(actual);return;}Assert.NotNull(actual);Assert.Equal(expected.Keys.Order(StringComparer.Ordinal),actual.Keys.Order(StringComparer.Ordinal));
        foreach(var pair in expected)Assert.Equal(pair.Value,actual[pair.Key]);
    }
}
public sealed class RedirectBootstrapEnvironment
{
    public DocumentationRendererProfile Profile {get;}
    public List<ToolArtifactRequest> Calls {get;}=[];
    public List<CancellationToken> Tokens {get;}=[];
    public CancellationTokenSource Cancellation {get;}=new();
    public ToolArtifactRequest[] Expected {get;}
    public ToolArtifactReceipt[] Receipts {get;}
    public RedirectBootstrapEnvironment(bool redirected,bool empty=false)
    {
        var original=new DocumentationBootstrapEnvironment(new("BOOTSTRAP-ONLINE",ToolArtifactAccess.Online,null,""));
        Profile=original.Profile with{ToolRedirects=redirected?(empty?RendererRedirectCases.Allowlist():RendererRedirectCases.Allowlist(RendererRedirectCases.Observed)):null};
        Expected=original.Requests.Select((request,index)=>redirected&&index==0?request with{Pin=request.Pin with{AllowedRedirects=Profile.ToolRedirects![RendererRedirectCases.Gradle]}}:request).ToArray();
        Receipts=Expected.Select((request,index)=>original.Receipts[index] with{Pin=request.Pin}).ToArray();
    }
}
public sealed class RecordingRedirectToolAcquirer(RedirectBootstrapEnvironment e):IToolArtifactAcquirer
{
    public Task<ToolArtifactReceipt> AcquireAsync(ToolArtifactRequest request,CancellationToken cancellationToken=default)
    {e.Calls.Add(request);e.Tokens.Add(cancellationToken);return Task.FromResult(e.Receipts[e.Calls.Count-1]);}
}
public sealed class ScriptedRedirectBootstrap(RedirectBootstrapEnvironment e,string mutation=""):IDocumentationToolBootstrap
{
    public async Task<IReadOnlyList<ToolArtifactReceipt>> AcquireAsync(DocumentationRendererProfile profile,ToolArtifactAccess access,string? proof,CancellationToken cancellationToken=default)
    {
        Assert.Same(e.Profile,profile);Assert.Equal(ToolArtifactAccess.Online,access);Assert.Null(proof);var result=new List<ToolArtifactReceipt>();
        foreach(var original in e.Expected)
        {
            var request=original;
            if(mutation=="omission")request=request with{Pin=request.Pin with{AllowedRedirects=null}};
            if(mutation=="substitution")request=request with{Pin=request.Pin with{AllowedRedirects=[new("https://unapproved.invalid/file")]}};
            if(mutation=="broadening")request=request with{Pin=request.Pin with{AllowedRedirects=[RendererRedirectCases.Observed]}};
            result.Add(await new RecordingRedirectToolAcquirer(e).AcquireAsync(request,cancellationToken));
        }
        return result;
    }
}
public static class RedirectBootstrapAssertions
{
    public static async Task AssertContract(RedirectBootstrapEnvironment e,IDocumentationToolBootstrap provider)
    {
        var result=await provider.AcquireAsync(e.Profile,ToolArtifactAccess.Online,null,e.Cancellation.Token);Assert.Equal(e.Receipts,result);Assert.Equal(5,e.Calls.Count);
        for(var index=0;index<e.Expected.Length;index++)
        {
            var expected=e.Expected[index];var actual=e.Calls[index];
            Assert.Equal(expected with{Pin=expected.Pin with{AllowedRedirects=null}},actual with{Pin=actual.Pin with{AllowedRedirects=null}});
            if(expected.Pin.AllowedRedirects is null)Assert.Null(actual.Pin.AllowedRedirects);else{Assert.NotNull(actual.Pin.AllowedRedirects);Assert.Equal(expected.Pin.AllowedRedirects,actual.Pin.AllowedRedirects);}
        }
        Assert.Equal(5,e.Tokens.Count);Assert.All(e.Tokens,token=>Assert.Equal(e.Cancellation.Token,token));
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class RendererRedirectProfileMockTests
{
    [Theory,MemberData(nameof(RendererRedirectCases.Data),MemberType=typeof(RendererRedirectCases))]
    public void CorrectReaderRedirectMocksPass(RendererProfileCase test)=>RendererRedirectCases.AssertReader(test,new ScriptedRendererProfile(test.Expected,test.Error));
    [Theory,MemberData(nameof(RendererRedirectCases.Data),MemberType=typeof(RendererRedirectCases))]
    public void EveryWrongRedirectReaderOutcomeIsRejected(RendererProfileCase test)=>Assert.NotNull(Record.Exception(()=>RendererRedirectCases.AssertReader(test,new ScriptedRendererProfile(test.Expected,"WRONG"))));
    [Theory]
    [InlineData("omission")][InlineData("substitution")][InlineData("unknown-key")]
    public void EachRedirectAuthorityFieldHasItsOwnNegativeControl(string mutation)
    {
        var test=RendererRedirectCases.Data.Select(row=>(RendererProfileCase)row[0]).Single(test=>test.Id=="REDIRECT-OBSERVED");
        var wrong=test.Expected with{ToolRedirects=mutation=="omission"?null:mutation=="substitution"?RendererRedirectCases.Allowlist(new Uri("https://unapproved.invalid/file")):new Dictionary<string,IReadOnlyList<Uri>>{{"unknown",[RendererRedirectCases.Observed]}}};
        Assert.NotNull(Record.Exception(()=>RendererRedirectCases.AssertReader(test,new ScriptedRendererProfile(wrong))));
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class RendererRedirectBootstrapMockTests
{
    [Theory]
    [InlineData(false,false)][InlineData(true,false)][InlineData(true,true)]
    public Task CorrectOnlyDeclaredRedirectForwardingMocksPass(bool redirected,bool empty){var e=new RedirectBootstrapEnvironment(redirected,empty);return RedirectBootstrapAssertions.AssertContract(e,new ScriptedRedirectBootstrap(e));}
    [Theory]
    [InlineData("omission")][InlineData("substitution")][InlineData("broadening")]
    public async Task EachForwardingEscapeIsIndividuallyRejected(string mutation){var e=new RedirectBootstrapEnvironment(true);Assert.NotNull(await Record.ExceptionAsync(()=>RedirectBootstrapAssertions.AssertContract(e,new ScriptedRedirectBootstrap(e,mutation))));}
}
