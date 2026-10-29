using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed record DocumentationBootstrapCase(string Id,ToolArtifactAccess Access,string? Proof,string Error)
{public override string ToString()=>Id;}
public sealed class DocumentationBootstrapEnvironment
{
    public DocumentationRendererProfile Profile {get;}=RendererProfileCases.Original().Expected;
    public DocumentationBootstrapCase Test {get;}
    public CancellationTokenSource Cancellation {get;}=new();
    public List<ToolArtifactRequest> Calls {get;}=[];
    public List<CancellationToken> Tokens {get;}=[];
    public ToolArtifactRequest[] Requests {get;}
    public ToolArtifactReceipt[] Receipts {get;}
    public DocumentationBootstrapEnvironment(DocumentationBootstrapCase test)
    {
        Test=test;if(test.Id=="BOOTSTRAP-CANCELLED")Cancellation.Cancel();
        Requests=Profile.Downloads.Select((pin,index)=>new ToolArtifactRequest(new(pin.LocalPath,pin.Revision,pin.Url,pin.Sha256,pin.SizeBytes!.Value,index==0?ToolArtifactFormat.Zip:ToolArtifactFormat.Stored,
            index==0?new(137393837,512L*1024*1024,128L*1024*1024,10000):new(pin.SizeBytes.Value,0,0,0),index==0?new Uri("https://services.gradle.org/distributions/gradle-8.14.3-bin.zip.sha256"):null),test.Access,pin.LocalPath,index==0?".tools":null,index==0&&test.Access==ToolArtifactAccess.Offline?test.Proof:null)).ToArray();
        Receipts=Requests.Select(request=>new ToolArtifactReceipt(request.Pin,new(request.CachePath,request.Pin.Sha256,request.Pin.SizeBytes,"application/octet-stream"),[],request.Pin.PublishedChecksumUrl is not null?new(request.CachePath+".published-checksum.json",new string('b',64),100,"application/json"):null,"reused",request.Access,request.Pin.Url)).ToArray();
    }
}
public sealed class RecordingDocumentationToolAcquirer(DocumentationBootstrapEnvironment environment):IToolArtifactAcquirer
{
    public Task<ToolArtifactReceipt> AcquireAsync(ToolArtifactRequest request,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();environment.Calls.Add(request);environment.Tokens.Add(cancellationToken);var index=environment.Calls.Count-1;
        if(environment.Test.Id=="BOOTSTRAP-ACQUISITION-FAILURE"&&index==1)throw new TransportException("TOOL_HASH","Pinned tool artifact mismatch");
        return Task.FromResult(environment.Receipts[index]);
    }
}
public static class DocumentationBootstrapCases
{
    public static IEnumerable<object[]> Data=>new[]
    {
        new DocumentationBootstrapCase("BOOTSTRAP-ONLINE",ToolArtifactAccess.Online,null,""),
        new DocumentationBootstrapCase("BOOTSTRAP-OFFLINE",ToolArtifactAccess.Offline,".tools/proofs/gradle-8.14.3.json",""),
        new DocumentationBootstrapCase("BOOTSTRAP-MISSING-PROOF",ToolArtifactAccess.Offline,null,"BOOTSTRAP_PROOF_REQUIRED"),
        new DocumentationBootstrapCase("BOOTSTRAP-UNSAFE-PROOF",ToolArtifactAccess.Offline,"../proof.json","BOOTSTRAP_ARGUMENT"),
        new DocumentationBootstrapCase("BOOTSTRAP-BAD-ACCESS",(ToolArtifactAccess)999,null,"BOOTSTRAP_ARGUMENT"),
        new DocumentationBootstrapCase("BOOTSTRAP-CANCELLED",ToolArtifactAccess.Online,null,"CANCELLED"),
        new DocumentationBootstrapCase("BOOTSTRAP-ACQUISITION-FAILURE",ToolArtifactAccess.Online,null,"TOOL_HASH")
    }.Select(c=>new object[]{c});
    public static async Task AssertContract(DocumentationBootstrapCase test,DocumentationBootstrapEnvironment environment,IDocumentationToolBootstrap provider)
    {
        IReadOnlyList<ToolArtifactReceipt>? result=null;var error=await Record.ExceptionAsync(async()=>result=await provider.AcquireAsync(environment.Profile,test.Access,test.Proof,environment.Cancellation.Token));
        if(test.Error=="CANCELLED")Assert.IsAssignableFrom<OperationCanceledException>(error);
        else if(test.Error=="TOOL_HASH")Assert.Equal(test.Error,Assert.IsType<TransportException>(error).Code);
        else if(test.Error.Length>0)Assert.Equal(test.Error,Assert.IsType<ArchitectureAssetException>(error).Code);
        else{Assert.Null(error);Assert.Equal(environment.Receipts,result);}
        var expectedCalls=test.Error.Length==0?5:test.Error=="TOOL_HASH"?2:0;Assert.Equal(expectedCalls,environment.Calls.Count);
        for(var index=0;index<expectedCalls;index++)Assert.Equal(environment.Requests[index],environment.Calls[index]);
        Assert.Equal(expectedCalls,environment.Tokens.Count);foreach(var token in environment.Tokens)Assert.Equal(environment.Cancellation.Token,token);
        Assert.DoesNotContain(environment.Calls,call=>call.Pin.Id.Contains("node",StringComparison.OrdinalIgnoreCase)||call.Pin.Id.Contains("marp",StringComparison.OrdinalIgnoreCase));
    }
}
public sealed class ScriptedDocumentationToolBootstrap(DocumentationBootstrapEnvironment environment,string mutation=""):IDocumentationToolBootstrap
{
    public async Task<IReadOnlyList<ToolArtifactReceipt>> AcquireAsync(DocumentationRendererProfile profile,ToolArtifactAccess access,string? proof,CancellationToken cancellationToken=default)
    {
        Assert.Same(environment.Profile,profile);Assert.Equal(environment.Test.Access,access);Assert.Equal(environment.Test.Proof,proof);
        if(mutation=="outcome")throw new ArchitectureAssetException("WRONG","Wrong scripted bootstrap outcome");
        cancellationToken.ThrowIfCancellationRequested();
        if(environment.Test.Error is "BOOTSTRAP_PROOF_REQUIRED" or "BOOTSTRAP_ARGUMENT")throw new ArchitectureAssetException(mutation=="outcome"?"WRONG":environment.Test.Error,"Scripted bootstrap preflight rejection");
        var result=new List<ToolArtifactReceipt>();var requests=environment.Requests.ToArray();
        if(mutation=="order")Array.Reverse(requests);if(mutation=="omission")requests=requests.Skip(1).ToArray();
        if(mutation.StartsWith("request-",StringComparison.Ordinal))
        {
            var request=requests[0];request=mutation switch
            {
                "request-access"=>request with{Access=ToolArtifactAccess.Offline},"request-cache"=>request with{CachePath="wrong"},"request-prefix"=>request with{ExtractionPrefix="wrong"},"request-proof"=>request with{OfflineChecksumProofPath="wrong"},
                "request-id"=>request with{Pin=request.Pin with{Id="wrong"}},"request-version"=>request with{Pin=request.Pin with{Version="wrong"}},"request-url"=>request with{Pin=request.Pin with{Url=new("https://wrong.test/")}},
                "request-hash"=>request with{Pin=request.Pin with{Sha256="wrong"}},"request-size"=>request with{Pin=request.Pin with{SizeBytes=0}},"request-format"=>request with{Pin=request.Pin with{Format=ToolArtifactFormat.Stored}},
                "request-bounds"=>request with{Pin=request.Pin with{Bounds=new(1,1,1,1)}},"request-checksum"=>request with{Pin=request.Pin with{PublishedChecksumUrl=null}},_=>request with{Pin=request.Pin with{AllowedRedirects=[new("https://unverified.test/")]}}
            };requests[0]=request;
        }
        foreach(var request in requests)result.Add(await new RecordingDocumentationToolAcquirer(environment).AcquireAsync(request,mutation=="request-token"?CancellationToken.None:cancellationToken));
        if(mutation=="result")return [];if(mutation=="result-order")result.Reverse();return result;
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationToolBootstrapMockTests
{
    [Theory,MemberData(nameof(DocumentationBootstrapCases.Data),MemberType=typeof(DocumentationBootstrapCases))]
    public Task CorrectBootstrapMocksPass(DocumentationBootstrapCase test){var environment=new DocumentationBootstrapEnvironment(test);return DocumentationBootstrapCases.AssertContract(test,environment,new ScriptedDocumentationToolBootstrap(environment));}
    [Theory,MemberData(nameof(DocumentationBootstrapCases.Data),MemberType=typeof(DocumentationBootstrapCases))]
    public async Task WrongBootstrapOutcomeRejected(DocumentationBootstrapCase test)
    {var environment=new DocumentationBootstrapEnvironment(test);Assert.NotNull(await Record.ExceptionAsync(()=>DocumentationBootstrapCases.AssertContract(test,environment,new ScriptedDocumentationToolBootstrap(environment,"outcome"))));}
    [Theory]
    [InlineData("order")][InlineData("omission")][InlineData("result")][InlineData("result-order")]
    [InlineData("request-access")][InlineData("request-cache")][InlineData("request-prefix")][InlineData("request-proof")][InlineData("request-id")][InlineData("request-version")][InlineData("request-url")][InlineData("request-hash")][InlineData("request-size")][InlineData("request-format")][InlineData("request-bounds")][InlineData("request-checksum")][InlineData("request-redirects")][InlineData("request-token")]
    public async Task EveryBootstrapFieldAndInteractionRejectsWrongResponse(string mutation)
    {var test=(DocumentationBootstrapCase)DocumentationBootstrapCases.Data.First()[0];var environment=new DocumentationBootstrapEnvironment(test);Assert.NotNull(await Record.ExceptionAsync(()=>DocumentationBootstrapCases.AssertContract(test,environment,new ScriptedDocumentationToolBootstrap(environment,mutation))));}
}
