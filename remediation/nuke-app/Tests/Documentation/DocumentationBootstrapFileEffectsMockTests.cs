using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed class BootstrapFileEnvironment:IArtifactTreeReader,IArtifactPermissionEditor,IDocumentationWrapperPropertiesValidator
{
    public string Scenario{get;}
    public DocumentationRendererProfile Profile{get;}=RendererProfileCases.Original().Expected;
    public Artifact Properties{get;}
    public string Bin=>".tools/gradle-8.14.3/bin";
    public ToolArtifactReceipt Gradle{get;}
    public IReadOnlyList<ArtifactTreeEntry> BinEntries{get;}
    public IReadOnlyList<ArtifactTreeEntry> RootEntries{get;}
    public List<string> Trace{get;}=[];
    public List<(string? Directory,bool Recursive)> TreeCalls{get;}=[];
    public List<ArtifactPermissionRequest> PermissionCalls{get;}=[];
    public List<CancellationToken> Tokens{get;}=[];
    public CancellationTokenSource Cancellation{get;}=new();
    public int PropertyCalls{get;private set;}
    public static NativeArtifactObservation Identity(NativeArtifactKind kind,ulong id,long length)=>new(NativeArtifactPlatform.Linux,kind,8,id,0,length,10,1);
    public BootstrapFileEnvironment(string scenario)
    {
        Scenario=scenario;Properties=new(WrapperPropertiesCases.Path,System.Text.Encoding.UTF8.GetBytes(WrapperPropertiesCases.Expected),"application/octet-stream");
        var original=new DocumentationBootstrapEnvironment(new("BOOTSTRAP-ONLINE",ToolArtifactAccess.Online,null,""));
        Gradle=original.Receipts[0] with{Files=[new(Bin+"/gradle",ContentHash.Sha256("abc"u8),3,"application/octet-stream")]};
        BinEntries=[new(Bin+"/gradle",NativeArtifactKind.RegularFile,3,Identity(NativeArtifactKind.RegularFile,1,3)),
            new(Bin+"/existing-extra",NativeArtifactKind.RegularFile,2,Identity(NativeArtifactKind.RegularFile,2,2)),
            new(Bin+"/existing-directory",NativeArtifactKind.Directory,0,Identity(NativeArtifactKind.Directory,3,0))];
        var wrapper=Profile.Downloads.Single(pin=>pin.LocalPath=="gradlew");
        RootEntries=[new("gradlew",NativeArtifactKind.RegularFile,wrapper.SizeBytes!.Value,Identity(NativeArtifactKind.RegularFile,4,wrapper.SizeBytes.Value)),
            new("unrelated",NativeArtifactKind.RegularFile,1,Identity(NativeArtifactKind.RegularFile,5,1))];
        if(scenario=="EMPTY-BIN")BinEntries=[];
        if(scenario=="MISSING-WRAPPER")RootEntries=RootEntries.Skip(1).ToArray();
        if(scenario=="NONREGULAR")BinEntries=[BinEntries[0] with{Kind=NativeArtifactKind.Other,Native=null}];
        if(scenario=="MISSING-IDENTITY")BinEntries=[BinEntries[0] with{Native=null}];
        if(scenario=="OUTSIDE-SCOPE")BinEntries=[BinEntries[0] with{RelativePath="outside/gradle"}];
        if(scenario=="CANCELLED")Cancellation.Cancel();
    }
    public Task<IReadOnlyList<ArtifactTreeEntry>> EnumerateAsync(string? relativeDirectory=null,bool recursive=true,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();Tokens.Add(cancellationToken);TreeCalls.Add((relativeDirectory,recursive));Trace.Add(relativeDirectory is null?"tree:root":"tree:bin");
        if(Scenario=="TREE-FAIL")throw new IOException("tree fixture");
        Assert.False(recursive);Assert.True(relativeDirectory is null||relativeDirectory==Bin);
        return Task.FromResult(relativeDirectory is null?RootEntries:BinEntries);
    }
    public Task<ArtifactPermissionResult> ApplyAsync(ArtifactPermissionRequest request,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();Tokens.Add(cancellationToken);PermissionCalls.Add(request);Trace.Add("mode:"+request.Expected.RelativePath);
        if(Scenario=="PERMISSION-0"&&PermissionCalls.Count==1||Scenario=="PERMISSION-1"&&PermissionCalls.Count==2||Scenario=="WRAPPER-PERMISSION"&&request.Expected.RelativePath=="gradlew")throw new IOException("mode fixture");
        var before=UnixFileMode.UserRead|UnixFileMode.UserWrite;
        var after=request.Operation==ArtifactPermissionOperation.AddExecuteBits?before|UnixFileMode.UserExecute|UnixFileMode.GroupExecute|UnixFileMode.OtherExecute:
            UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute|UnixFileMode.GroupRead|UnixFileMode.GroupExecute|UnixFileMode.OtherRead|UnixFileMode.OtherExecute;
        return Task.FromResult(new ArtifactPermissionResult(request.Expected,before,after));
    }
    public void Validate(DocumentationRendererProfile profile,Artifact properties)
    {Assert.Same(Profile,profile);Assert.Same(Properties,properties);PropertyCalls++;Trace.Add("properties");if(Scenario=="PROPERTY-FAIL")throw new ArchitectureAssetException("WRAPPER_PROPERTIES","property fixture");}
    public ArtifactPermissionRequest BinRequest(ArtifactTreeEntry entry)=>new(new(entry.RelativePath,entry.Native!),ArtifactPermissionOperation.AddExecuteBits,Gradle.Files.SingleOrDefault(file=>file.RelativePath==entry.RelativePath));
    public ArtifactPermissionRequest WrapperRequest()
    {var entry=RootEntries.Single(value=>value.RelativePath=="gradlew");var pin=Profile.Downloads.Single(value=>value.LocalPath=="gradlew");return new(new(entry.RelativePath,entry.Native!),ArtifactPermissionOperation.SetMode0755,new(pin.LocalPath,pin.Sha256,pin.SizeBytes!.Value,"application/octet-stream"));}
}
public static class BootstrapFileCases
{
    public static IEnumerable<object[]> Cases=>new[]{"FULL","EMPTY-BIN","MISSING-WRAPPER","NONREGULAR","MISSING-IDENTITY","OUTSIDE-SCOPE","TREE-FAIL","PERMISSION-0","PERMISSION-1","WRAPPER-PERMISSION","PROPERTY-FAIL","CANCELLED"}.Select(id=>new object[]{id});
    public static async Task AssertContract(BootstrapFileEnvironment e,IDocumentationBootstrapFileEffects provider)
    {
        var error=await Record.ExceptionAsync(async()=>{await provider.GradleExtractedAsync(e.Profile,e.Gradle,e.Cancellation.Token);await provider.WrappersAcquiredAsync(e.Profile,e.Cancellation.Token);});
        if(e.Scenario is "FULL" or "EMPTY-BIN")Assert.Null(error);
        else if(e.Scenario=="CANCELLED")Assert.IsAssignableFrom<OperationCanceledException>(error);
        else if(e.Scenario=="PROPERTY-FAIL")Assert.Equal("WRAPPER_PROPERTIES",Assert.IsType<ArchitectureAssetException>(error).Code);
        else if(e.Scenario is "MISSING-WRAPPER" or "NONREGULAR" or "MISSING-IDENTITY" or "OUTSIDE-SCOPE")Assert.Equal("BOOTSTRAP_FILES",Assert.IsType<ArchitectureAssetException>(error).Code);
        else Assert.IsType<IOException>(error);
        var expected=new List<ArtifactPermissionRequest>();var trace=new List<string>();var treeCalls=new List<(string?,bool)>();
        if(e.Scenario!="CANCELLED"){
            trace.Add("tree:bin");treeCalls.Add((e.Bin,false));
            if(e.Scenario is not ("NONREGULAR" or "MISSING-IDENTITY" or "OUTSIDE-SCOPE" or "TREE-FAIL")){
                foreach(var entry in e.BinEntries){expected.Add(e.BinRequest(entry));trace.Add("mode:"+entry.RelativePath);if(e.Scenario=="PERMISSION-0"&&expected.Count==1||e.Scenario=="PERMISSION-1"&&expected.Count==2)break;}
                if(e.Scenario is not ("PERMISSION-0" or "PERMISSION-1")){
                    trace.Add("tree:root");treeCalls.Add((null,false));
                    if(e.Scenario!="MISSING-WRAPPER"){
                        expected.Add(e.WrapperRequest());trace.Add("mode:gradlew");
                        if(e.Scenario!="WRAPPER-PERMISSION")trace.Add("properties");
                    }
                }
            }
        }
        Assert.Equal(expected,e.PermissionCalls);Assert.Equal(trace,e.Trace);Assert.Equal(treeCalls,e.TreeCalls);
        Assert.Equal(trace.Count(step=>step=="properties"),e.PropertyCalls);foreach(var token in e.Tokens)Assert.Equal(e.Cancellation.Token,token);
    }
}
public sealed class ScriptedBootstrapFileEffects(BootstrapFileEnvironment e,string mutation=""):IDocumentationBootstrapFileEffects
{
    public async Task GradleExtractedAsync(DocumentationRendererProfile profile,ToolArtifactReceipt gradle,CancellationToken token=default)
    {
        Assert.Same(e.Profile,profile);Assert.Same(e.Gradle,gradle);token.ThrowIfCancellationRequested();
        var entries=await e.EnumerateAsync(e.Bin,false,token);
        if(e.Scenario is "NONREGULAR" or "MISSING-IDENTITY" or "OUTSIDE-SCOPE")throw new ArchitectureAssetException("BOOTSTRAP_FILES","original entry admission");
        foreach(var entry in mutation=="omit-entry"?entries.Skip(1):entries){
            var request=e.BinRequest(entry);request=mutation switch{"operation"=>request with{Operation=ArtifactPermissionOperation.SetMode0755},"identity"=>request with{Expected=request.Expected with{Native=request.Expected.Native with{FileIdLow=999}}},"pin"=>request with{ExpectedArtifact=null},_=>request};
            await e.ApplyAsync(request,mutation=="token"?CancellationToken.None:token);
        }
    }
    public async Task WrappersAcquiredAsync(DocumentationRendererProfile profile,CancellationToken token=default)
    {
        var root=await e.EnumerateAsync(null,false,token);if(!root.Any(entry=>entry.RelativePath=="gradlew"))throw new ArchitectureAssetException("BOOTSTRAP_FILES","missing wrapper");
        if(mutation=="property-order")e.Validate(profile,e.Properties);
        var request=e.WrapperRequest();if(mutation=="wrapper-mode")request=request with{Operation=ArtifactPermissionOperation.AddExecuteBits};
        if(mutation!="omit-wrapper")await e.ApplyAsync(request,token);
        if(mutation is not ("property-order" or "omit-properties"))e.Validate(profile,e.Properties);
    }
}
[Trait("Gate","Mock"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationBootstrapFileEffectsMockTests
{
    [Theory,MemberData(nameof(BootstrapFileCases.Cases),MemberType=typeof(BootstrapFileCases))]
    public Task CorrectSharedBoundaryMocksPreserveOriginalModeAndPropertyEffects(string scenario)
    {var e=new BootstrapFileEnvironment(scenario);return BootstrapFileCases.AssertContract(e,new ScriptedBootstrapFileEffects(e));}
    [Theory,InlineData("omit-entry"),InlineData("operation"),InlineData("identity"),InlineData("pin"),InlineData("token"),InlineData("wrapper-mode"),InlineData("omit-wrapper"),InlineData("property-order"),InlineData("omit-properties")]
    public async Task EveryModeEntryIdentityPinAndPhasePredicateRejectsWrongMocks(string mutation)
    {var e=new BootstrapFileEnvironment("FULL");Assert.NotNull(await Record.ExceptionAsync(()=>BootstrapFileCases.AssertContract(e,new ScriptedBootstrapFileEffects(e,mutation))));}
}
