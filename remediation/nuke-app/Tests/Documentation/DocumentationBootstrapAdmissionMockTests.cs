using System.Text;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed class LatePropertiesEnvironment
{
    public string Scenario{get;}
    public BootstrapFileEnvironment Effects{get;}=new("FULL");
    public Artifact Captured{get;}
    public List<string> Trace{get;}=[];
    public CancellationTokenSource Cancellation{get;}=new();
    public FileNotFoundException Missing{get;}=new("wrapper properties missing at original read boundary",WrapperPropertiesCases.Path);
    public Func<CancellationToken,Task<Artifact>> Read=>token=>{
        Assert.Equal(Cancellation.Token,token);Assert.Equal(new[]{"tree:root","mode:gradlew"},Effects.Trace);Trace.Add("read");
        if(Scenario=="MISSING-MISSING")throw Missing;if(Scenario=="CANCEL-READ"){Cancellation.Cancel();token.ThrowIfCancellationRequested();}
        if(Scenario=="MISSING-CREATED")Trace.Add("created-before-read");return Task.FromResult(Captured);
    };
    public IDocumentationWrapperPropertiesValidator Validator=>new RecordingValidator(this);
    public LatePropertiesEnvironment(string scenario){Scenario=scenario;Captured=Effects.Properties;if(scenario=="BAD")Captured=Captured with{Bytes=Encoding.UTF8.GetBytes(WrapperPropertiesCases.Expected.Replace("networkTimeout=10000","networkTimeout=1",StringComparison.Ordinal))};}
    private sealed class RecordingValidator(LatePropertiesEnvironment e):IDocumentationWrapperPropertiesValidator
    {public void Validate(DocumentationRendererProfile profile,Artifact properties){Assert.Same(e.Effects.Profile,profile);Assert.Same(e.Captured,properties);e.Trace.Add("validate");if(e.Scenario=="BAD")throw new ArchitectureAssetException("WRAPPER_PROPERTIES","Committed wrapper properties differ from pinned expected configuration");}}
}
public static class LatePropertiesCases
{
    public static IEnumerable<object[]> Cases=>new[]{"GOOD","MISSING-MISSING","MISSING-CREATED","BAD","CANCEL-READ"}.Select(value=>new object[]{value});
    public static async Task AssertContract(LatePropertiesEnvironment e,IDocumentationBootstrapFileEffects provider)
    {
        var failure=await Record.ExceptionAsync(()=>provider.WrappersAcquiredAsync(e.Effects.Profile,e.Cancellation.Token));
        if(e.Scenario=="MISSING-MISSING")Assert.Same(e.Missing,failure);else if(e.Scenario=="CANCEL-READ")Assert.IsAssignableFrom<OperationCanceledException>(failure);else if(e.Scenario=="BAD")Assert.Equal("WRAPPER_PROPERTIES",Assert.IsType<ArchitectureAssetException>(failure).Code);else Assert.Null(failure);
        Assert.Equal(new[]{"tree:root","mode:gradlew"},e.Effects.Trace);
        Assert.Equal(e.Scenario is "MISSING-MISSING" or "CANCEL-READ"?new[]{"read"}:e.Scenario=="MISSING-CREATED"?new[]{"read","created-before-read","validate"}:new[]{"read","validate"},e.Trace);
        Assert.All(e.Effects.Tokens,token=>Assert.Equal(e.Cancellation.Token,token));
    }
}
public sealed class ScriptedLateProperties(LatePropertiesEnvironment e,string mutation=""):IDocumentationBootstrapFileEffects
{
    public Task GradleExtractedAsync(DocumentationRendererProfile profile,ToolArtifactReceipt gradle,CancellationToken token=default)=>throw new InvalidOperationException("Existing Gradle phase remains separately proved");
    public async Task WrappersAcquiredAsync(DocumentationRendererProfile profile,CancellationToken token=default)
    {
        if(mutation=="outcome")throw new InvalidOperationException("wrong outcome");
        Artifact? value=null;if(mutation=="early")value=await e.Read(token);
        await e.Effects.EnumerateAsync(null,false,token);await e.Effects.ApplyAsync(e.Effects.WrapperRequest(),token);
        value??=await e.Read(mutation=="token"?CancellationToken.None:token);
        if(mutation=="duplicate")await e.Read(token);
        if(mutation!="skip-validation")e.Validator.Validate(profile,value);
    }
}
public sealed class AdmittedToolsEnvironment:IPinnedArtifactReader
{
    public string Scenario{get;}
    public DocumentationBootstrapEnvironment Original{get;}=new(new("BOOTSTRAP-OFFLINE",ToolArtifactAccess.Offline,"proofs/gradle.json",""));
    public DocumentationRendererProfile Requested{get;}
    public ToolArtifactAccess Access{get;}=ToolArtifactAccess.Offline;
    public string? Proof{get;}="proofs/gradle.json";
    public ToolArtifactReceipt[] Receipts{get;}
    public List<StoredArtifact> Reads{get;}=[];
    public List<CancellationToken> Tokens{get;}=[];
    public CancellationTokenSource Cancellation{get;}=new();
    public PinnedArtifactException Changed{get;}=new("ARTIFACT_PIN_HASH");
    public IReadOnlyList<StoredArtifact> Pins=>Receipts.SelectMany(receipt=>new[]{receipt.Artifact}.Concat(receipt.Files).Concat(receipt.ChecksumProof is null?Array.Empty<StoredArtifact>():[receipt.ChecksumProof])).ToArray();
    public bool Mismatch=>Scenario.StartsWith("PROFILE-",StringComparison.Ordinal)||Scenario is "ACCESS" or "PROOF" or "RECEIPT-PIN" or "RECEIPT-COUNT";
    public AdmittedToolsEnvironment(string scenario)
    {
        Scenario=scenario;Requested=Original.Profile;Receipts=Original.Receipts.ToArray();
        Receipts[0]=Receipts[0] with{Files=[new(".tools/gradle-8.14.3/bin/gradle",ContentHash.Sha256("abc"u8),3,"application/octet-stream")],ChecksumProof=new("proofs/gradle.json",new string('a',64),7,"application/json")};
        Requested=scenario switch{
            "PROFILE-IMAGE"=>Requested with{Image="different"},"PROFILE-JAVA"=>Requested with{JavaVersion="different"},"PROFILE-GRADLE"=>Requested with{GradleVersion="different"},"PROFILE-PLANTUML"=>Requested with{PlantUmlVersion="different"},"PROFILE-CLI"=>Requested with{StructurizrVersion="different"},
            "PROFILE-DOWNLOAD"=>Requested with{Downloads=Requested.Downloads.Select((pin,index)=>index==0?pin with{Sha256=new string('0',64)}:pin).ToArray()},
            "PROFILE-RENDERER"=>Requested with{RendererFiles=Requested.RendererFiles.Skip(1).ToArray()},
            "PROFILE-REDIRECT"=>Requested with{ToolRedirects=new Dictionary<string,IReadOnlyList<Uri>>{["gradlew"]=[new("https://different.test/file")]}},_=>Requested};
        if(scenario=="ACCESS")Access=ToolArtifactAccess.Online;if(scenario=="PROOF")Proof="different.json";if(scenario=="CANCELLED")Cancellation.Cancel();
        if(scenario=="RECEIPT-PIN")Receipts[0]=Receipts[0] with{Artifact=Receipts[0].Artifact with{Sha256=new string('0',64)}};
        if(scenario=="RECEIPT-COUNT")Receipts=Receipts.Skip(1).ToArray();
    }
    public Task<ReadOnlyMemory<byte>> ReadAsync(StoredArtifact pin,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();Reads.Add(pin);Tokens.Add(token);
        if(Scenario=="TOOL-CHANGED"&&Reads.Count==2)throw Changed;if(Scenario=="TOOL-MISSING"&&Reads.Count==2)throw new FileNotFoundException("retained tool missing",pin.RelativePath);
        return Task.FromResult<ReadOnlyMemory<byte>>(new byte[]{1});
    }
}
public static class AdmittedToolsCases
{
    public static IEnumerable<object[]> Cases=>new[]{"GOOD","REPEATED","PROFILE-IMAGE","PROFILE-JAVA","PROFILE-GRADLE","PROFILE-PLANTUML","PROFILE-CLI","PROFILE-DOWNLOAD","PROFILE-RENDERER","PROFILE-REDIRECT","ACCESS","PROOF","RECEIPT-PIN","RECEIPT-COUNT","TOOL-CHANGED","TOOL-MISSING","CANCELLED"}.Select(value=>new object[]{value});
    public static async Task AssertContract(AdmittedToolsEnvironment e,IDocumentationToolBootstrap provider)
    {
        IReadOnlyList<ToolArtifactReceipt>? result=null;var failure=await Record.ExceptionAsync(async()=>result=await provider.AcquireAsync(e.Requested,e.Access,e.Proof,e.Cancellation.Token));
        if(e.Mismatch)Assert.Equal("BOOTSTRAP_ADMISSION",Assert.IsType<ArchitectureAssetException>(failure).Code);else if(e.Scenario=="TOOL-CHANGED")Assert.Same(e.Changed,failure);else if(e.Scenario=="TOOL-MISSING")Assert.IsType<FileNotFoundException>(failure);else if(e.Scenario=="CANCELLED")Assert.IsAssignableFrom<OperationCanceledException>(failure);
        else {Assert.Null(failure);Assert.NotNull(result);Assert.Equal(5,result.Count);for(var i=0;i<5;i++){Assert.Equal(e.Receipts[i].Pin,result[i].Pin);Assert.Equal(e.Receipts[i].Artifact,result[i].Artifact);Assert.Equal(e.Receipts[i].Files,result[i].Files);Assert.Equal(e.Receipts[i].ChecksumProof,result[i].ChecksumProof);}}
        if(e.Mismatch||e.Scenario is "TOOL-CHANGED" or "TOOL-MISSING" or "CANCELLED")Assert.Null(result);
        var expected=e.Mismatch||e.Scenario=="CANCELLED"?Array.Empty<StoredArtifact>():e.Scenario is "TOOL-CHANGED" or "TOOL-MISSING"?e.Pins.Take(2).ToArray():e.Pins.ToArray();Assert.Equal(expected,e.Reads);Assert.All(e.Tokens,token=>Assert.Equal(e.Cancellation.Token,token));
        if(e.Scenario=="REPEATED"){await provider.AcquireAsync(e.Requested,e.Access,e.Proof,e.Cancellation.Token);Assert.Equal(expected.Concat(expected),e.Reads);}
        Assert.Empty(e.Original.Calls); // No reacquisition or mode authority is supplied to this facade.
    }
}
public sealed class ScriptedAdmittedTools(AdmittedToolsEnvironment e,string mutation=""):IDocumentationToolBootstrap
{
    public async Task<IReadOnlyList<ToolArtifactReceipt>> AcquireAsync(DocumentationRendererProfile profile,ToolArtifactAccess access,string? proof,CancellationToken token=default)
    {
        if(mutation=="outcome")return [];token.ThrowIfCancellationRequested();if(e.Mismatch)throw new ArchitectureAssetException("BOOTSTRAP_ADMISSION","fixture identity mismatch");
        foreach(var pin in mutation=="skip-read"?Array.Empty<StoredArtifact>():mutation=="reverse"?e.Pins.Reverse():e.Pins)await e.ReadAsync(pin,mutation=="token"?CancellationToken.None:token);
        if(mutation=="duplicate")await e.ReadAsync(e.Pins[0],token);
        return mutation=="result"?[]:e.Receipts;
    }
}
[Trait("Gate","Mock"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationBootstrapAdmissionMockTests
{
    [Theory,MemberData(nameof(LatePropertiesCases.Cases),MemberType=typeof(LatePropertiesCases))]
    public Task CorrectOriginalLatePropertiesMocksPass(string scenario){var e=new LatePropertiesEnvironment(scenario);return LatePropertiesCases.AssertContract(e,new ScriptedLateProperties(e));}
    [Theory,MemberData(nameof(LatePropertiesCases.Cases),MemberType=typeof(LatePropertiesCases))]
    public async Task EveryLateReadWrongOutcomeIsRejected(string scenario){var e=new LatePropertiesEnvironment(scenario);Assert.NotNull(await Record.ExceptionAsync(()=>LatePropertiesCases.AssertContract(e,new ScriptedLateProperties(e,"outcome"))));}
    [Theory,InlineData("early"),InlineData("token"),InlineData("duplicate"),InlineData("skip-validation")]
    public async Task EveryLateReadOrderingTokenAndValidationFaultIsRejected(string mutation){var e=new LatePropertiesEnvironment("GOOD");Assert.NotNull(await Record.ExceptionAsync(()=>LatePropertiesCases.AssertContract(e,new ScriptedLateProperties(e,mutation))));}
    [Theory,MemberData(nameof(AdmittedToolsCases.Cases),MemberType=typeof(AdmittedToolsCases))]
    public Task CorrectAdmittedReceiptReuseMocksPass(string scenario){var e=new AdmittedToolsEnvironment(scenario);return AdmittedToolsCases.AssertContract(e,new ScriptedAdmittedTools(e));}
    [Theory,MemberData(nameof(AdmittedToolsCases.Cases),MemberType=typeof(AdmittedToolsCases))]
    public async Task EveryReuseWrongOutcomeIsRejected(string scenario){var e=new AdmittedToolsEnvironment(scenario);Assert.NotNull(await Record.ExceptionAsync(()=>AdmittedToolsCases.AssertContract(e,new ScriptedAdmittedTools(e,"outcome"))));}
    [Theory,InlineData("skip-read"),InlineData("reverse"),InlineData("token"),InlineData("duplicate"),InlineData("result")]
    public async Task EveryRetainedPinReadAndResultFaultIsRejected(string mutation){var e=new AdmittedToolsEnvironment("GOOD");Assert.NotNull(await Record.ExceptionAsync(()=>AdmittedToolsCases.AssertContract(e,new ScriptedAdmittedTools(e,mutation))));}
}
