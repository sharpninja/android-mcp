using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review.Staging;

public sealed record SdkBindingLoad(int Session,string ProjectPath);
public sealed record SdkBindingObservation(string Code,IReadOnlyList<TestGateSdkNativeOpenRequest> Opens,
    IReadOnlyList<StoredArtifact> PinReads,IReadOnlyList<SdkBindingLoad> Loads,
    IReadOnlyList<TestGateSdkNativeProject> Projects,IReadOnlyList<string> AfterDispose,
    IReadOnlyList<string> DefaultSdkAssemblyAdditions);

// Explicit local input shapes exercise native ProjectCollection without restore,
// compilation, custom SDK packages or a new general MSBuild-hosting framework.
// Native binding will use the SAME declared inputs/assertions with real selected
// SDK bytes and an owned physical fixture root; this mock never loads the SDK.
public sealed class SdkBindingFixture
{
    public const string SelectedSdkVersion="10.0.403";
    public static readonly string[] Names=["explicit-native-project","private-project-properties",
        "wrong-sdk-pin","missing-core-pin","escaped-project","malformed-project",
        "escaped-import","unsupported-reference"];
    public static readonly string[] RequiredPins=["Microsoft.Build.dll","Microsoft.Build.Framework.dll",
        "Microsoft.Build.Utilities.Core.dll","Microsoft.Build.Tasks.Core.dll","Microsoft.Common.CurrentVersion.targets",
        "Microsoft.NET.StringTools.dll"];
    public string Name { get; }
    public string Root { get; }
    public string SdkRoot { get; }
    public string ProjectPath=>Path.Combine(Root,"Host.csproj");
    public string LoadedPath=>Name=="escaped-project" ? Path.GetFullPath(Path.Combine(Root,"..","outside","Other.csproj")) : ProjectPath;
    public string ImportedPath=>Name=="escaped-import" ? Path.GetFullPath(Path.Combine(Root,"..","outside","External.props")) : Path.Combine(Root,"inputs","Shared.props");
    public Dictionary<string,byte[]> SdkBytes { get; }
    public TestGateSdkNativeOpenRequest OpenRequest { get; }
    public IReadOnlyList<TestGateSdkNativeOpenRequest> Requests=>Name=="private-project-properties"
        ? [OpenRequest,OpenRequest with {Evaluation=OpenRequest.Evaluation with {Configuration="Release"}}] : [OpenRequest];
    public string Code=>Name switch
    {
        "wrong-sdk-pin"=>"SDK_PIN_MISMATCH","missing-core-pin"=>"SDK_PIN_MISSING",
        "escaped-project" or "escaped-import"=>"INPUT_SCOPE_VIOLATION",
        "malformed-project"=>"SDK_EVALUATION_FAILED","unsupported-reference"=>"SOURCE_INPUTS_UNRESOLVED",_=>"OK"
    };
    public bool OpensSession=>Name is not ("wrong-sdk-pin" or "missing-core-pin");
    public SdkBindingFixture(string name,string root="/fixture/app",string sdkRoot="/fixture/sdk",
        Dictionary<string,byte[]>? sdkBytes=null,ToolIdentity? dotnet=null)
    {
        Name=name;Root=Path.GetFullPath(root);SdkRoot=Path.GetFullPath(sdkRoot);
        SdkBytes=sdkBytes??RequiredPins.ToDictionary(path=>path,path=>Encoding.UTF8.GetBytes("explicit-sdk-fixture:"+path),StringComparer.Ordinal);
        var pins=RequiredPins.Where(path=>name!="missing-core-pin"||path!="Microsoft.Build.dll")
            .Select(path=>new StoredArtifact(path,ContentHash.Sha256(SdkBytes[path]),SdkBytes[path].LongLength,"application/octet-stream")).ToArray();
        if(name=="wrong-sdk-pin")pins[0]=pins[0] with {Sha256=new string('0',64)};
        var run=new TestGateProducerRequest(Root,ProjectPath,Path.Combine(Root,"Tests","Tests.csproj"),
            dotnet??new("/fixture/dotnet",new string('d',64),SelectedSdkVersion),
            Path.Combine(Root,"artifacts","tests","sdk-binding"),null,[]);
        OpenRequest=new(new(run,new(SdkRoot,pins),"Debug","net10.0"),[Root,SdkRoot]);
    }
    public byte[] ProjectBytes
    {
        get
        {
            if(Name=="malformed-project")return Encoding.UTF8.GetBytes("<Project><PropertyGroup>");
            var items=new XElement("ItemGroup",new XElement("Compile",new XAttribute("Include","Core/Host.cs")),
                new XElement("EmbeddedResource",new XAttribute("Include","assets/input.bin")),
                new XElement("AdditionalFiles",new XAttribute("Include","config/input.txt")),
                new XElement("Analyzer",new XAttribute("Include","tools/fixture-analyzer.dll")),
                new XElement("ProjectReference",new XAttribute("Include","Library/Library.csproj")));
            if(Name=="unsupported-reference")items.Add(new XElement("Reference",new XAttribute("Include","CustomBinary"),
                new XElement("HintPath","tools/custom-reference.dll")));
            var import=Name=="escaped-import" ? "../outside/External.props" : "inputs/Shared.props";
            var document=new XDocument(new XElement("Project",new XAttribute("ToolsVersion","Current"),
                new XElement("PropertyGroup",new XElement("TargetPath","$(MSBuildProjectDirectory)/bin/$(Configuration)/Host.dll")),
                new XElement("Import",new XAttribute("Project",import)),items));
            return Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting));
        }
    }
    public byte[] ImportBytes=>Encoding.UTF8.GetBytes(new XDocument(new XElement("Project",new XElement("PropertyGroup",
        new XElement("ProjectAssetsFile","$(MSBuildProjectDirectory)/obj/project.assets.json")))).ToString(SaveOptions.DisableFormatting));
    public TestGateSdkNativeProject ExpectedProject(TestGateSdkNativeOpenRequest request)=>new(ProjectPath,SdkRoot,
        request.Evaluation.Configuration,request.Evaluation.TargetFramework,
        Path.Combine(Root,"bin",request.Evaluation.Configuration,"Host.dll"),
        [new("Compile","Core/Host.cs",Path.Combine(Root,"Core","Host.cs"),ProjectPath),
         new("EmbeddedResource","assets/input.bin",Path.Combine(Root,"assets","input.bin"),ProjectPath),
         new("AdditionalFiles","config/input.txt",Path.Combine(Root,"config","input.txt"),ProjectPath),
         new("Analyzer","tools/fixture-analyzer.dll",Path.Combine(Root,"tools","fixture-analyzer.dll"),ProjectPath),
         new("ProjectReference","Library/Library.csproj",Path.Combine(Root,"Library","Library.csproj"),ProjectPath)],
        [new(ImportedPath,ProjectPath)],new Dictionary<string,string>{{"ProjectAssetsFile",Path.Combine(Root,"obj","project.assets.json")}},[]);
    public SdkBindingObservation Expected
    {
        get
        {
            var reads=Name=="missing-core-pin" ? Array.Empty<StoredArtifact>() : Name=="wrong-sdk-pin"
                ? OpenRequest.Evaluation.Sdk.AssemblyFiles.Take(1).ToArray()
                : Requests.SelectMany(request=>request.Evaluation.Sdk.AssemblyFiles).ToArray();
            return new(Code,Requests.Take(Code=="OK" ? Requests.Count : 1).ToArray(),reads,
                OpensSession ? Requests.Select((_,index)=>new SdkBindingLoad(index,LoadedPath)).ToArray() : [],
                Code=="OK" ? Requests.Select(ExpectedProject).ToArray() : [],
                OpensSession ? Requests.Select(_=>"OBJECT_DISPOSED").ToArray() : [],[]);
        }
    }
}
public sealed class SdkBindingPinReader(SdkBindingFixture fixture):IPinnedArtifactReader
{
    public List<StoredArtifact> Reads { get; }=[];
    private readonly PinnedArtifactReader native=new(new SdkBindingFixtureStore(fixture));
    public Task<ReadOnlyMemory<byte>> ReadAsync(StoredArtifact callerPin,CancellationToken cancellationToken=default)
    {Reads.Add(callerPin);return native.ReadAsync(callerPin,cancellationToken);}
    private sealed class SdkBindingFixtureStore(SdkBindingFixture fixture):IArtifactStore
    {
        public Task<ReadOnlyMemory<byte>> ReadAsync(string relativePath,CancellationToken cancellationToken=default)
            =>Task.FromResult<ReadOnlyMemory<byte>>(fixture.SdkBytes[relativePath]);
        public Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken cancellationToken=default)
            =>throw new InvalidOperationException("The SDK binding may not write into the installed SDK.");
    }
}
public sealed class ScriptedSdkBindingFactory(SdkBindingFixture fixture,IPinnedArtifactReader pins):ITestGateSdkNativeSessionFactory
{
    public async Task<ITestGateSdkNativeSession> OpenAsync(TestGateSdkNativeOpenRequest request,CancellationToken cancellationToken=default)
    {
        if(SdkBindingFixture.RequiredPins.Any(required=>!request.Evaluation.Sdk.AssemblyFiles.Any(pin=>pin.RelativePath==required)))
            throw new TestGateFactsException("SDK_PIN_MISSING","Required fixed SDK pin is absent.");
        try{foreach(var pin in request.Evaluation.Sdk.AssemblyFiles)await pins.ReadAsync(pin,cancellationToken);}
        catch(PinnedArtifactException){throw new TestGateFactsException("SDK_PIN_MISMATCH","Exact SDK input bytes differ.");}
        return new Session(fixture,request);
    }
    private sealed class Session(SdkBindingFixture fixture,TestGateSdkNativeOpenRequest request):ITestGateSdkNativeSession
    {
        private bool disposed;
        public Task<TestGateSdkNativeProject> LoadProjectAsync(string absoluteProjectPath,CancellationToken cancellationToken=default)
        {
            ObjectDisposedException.ThrowIf(disposed,this);
            if(fixture.Code!="OK")throw new TestGateFactsException(fixture.Code,"Scripted native API outcome.");
            return Task.FromResult(fixture.ExpectedProject(request));
        }
        public ValueTask DisposeAsync(){disposed=true;return ValueTask.CompletedTask;}
    }
}
public static class SdkBindingCases
{
    public static IEnumerable<object[]> Data=>SdkBindingFixture.Names.Select(name=>new object[]{name});
    public static async Task AssertMissingStringToolsAsync(SdkBindingFixture fixture,
        Func<IPinnedArtifactReader,ITestGateSdkNativeSessionFactory> create)
    {
        var pins=new SdkBindingPinReader(fixture);
        var missing=fixture.OpenRequest with {Evaluation=fixture.OpenRequest.Evaluation with
            {Sdk=fixture.OpenRequest.Evaluation.Sdk with {AssemblyFiles=fixture.OpenRequest.Evaluation.Sdk.AssemblyFiles
                .Where(pin=>pin.RelativePath!="Microsoft.NET.StringTools.dll").ToArray()}}};
        var error=await Assert.ThrowsAsync<TestGateFactsException>(()=>create(pins).OpenAsync(missing));
        Assert.Equal("SDK_PIN_MISSING",error.Code);Assert.Empty(pins.Reads);
    }
    public static async Task<SdkBindingObservation> ExecuteAsync(SdkBindingFixture fixture,
        Func<IPinnedArtifactReader,ITestGateSdkNativeSessionFactory> create)
    {
        var pins=new SdkBindingPinReader(fixture);var factory=create(pins);
        var opens=new List<TestGateSdkNativeOpenRequest>();var loads=new List<SdkBindingLoad>();
        var projects=new List<TestGateSdkNativeProject>();var lifecycle=new List<string>();var code="OK";
        var before=DefaultSdkAssemblies(fixture.SdkRoot).ToHashSet(StringComparer.Ordinal);
        foreach(var request in fixture.Requests)
        {
            ITestGateSdkNativeSession? session=null;
            try
            {
                opens.Add(request);session=await factory.OpenAsync(request);
                loads.Add(new(opens.Count-1,fixture.LoadedPath));projects.Add(await session.LoadProjectAsync(fixture.LoadedPath));
            }
            catch(TestGateFactsException error){code=error.Code;}
            finally
            {
                if(session is not null)
                {
                    await session.DisposeAsync();
                    try{await session.LoadProjectAsync(fixture.LoadedPath);lifecycle.Add("DISPOSED_SESSION_ACCEPTED");}
                    catch(ObjectDisposedException){lifecycle.Add("OBJECT_DISPOSED");}
                }
            }
            if(code!="OK")break;
        }
        return new(code,opens,pins.Reads,loads,projects,lifecycle,
            DefaultSdkAssemblies(fixture.SdkRoot).Where(value=>!before.Contains(value)).Order(StringComparer.Ordinal).ToArray());
    }
    private static IEnumerable<string> DefaultSdkAssemblies(string sdkRoot)=>AssemblyLoadContext.Default.Assemblies
        .Where(assembly=>!assembly.IsDynamic&&((assembly.GetName().Name??"").StartsWith("Microsoft.Build",StringComparison.Ordinal)||
            assembly.GetName().Name=="Microsoft.NET.StringTools"||(!string.IsNullOrEmpty(assembly.Location)&&
            Path.GetFullPath(assembly.Location).StartsWith(Path.GetFullPath(sdkRoot)+Path.DirectorySeparatorChar,StringComparison.Ordinal))))
        .Select(assembly=>assembly.FullName+"|"+assembly.Location+"|"+assembly.ManifestModule.ModuleVersionId);
    public static void AssertContract(SdkBindingFixture fixture,JsonNode actual)=>Assert.Equal(
        new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(fixture.Expected)),
        new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(actual)));
    public static IEnumerable<object[]> Controls
    {
        get
        {
            foreach(var name in SdkBindingFixture.Names)
            {
                var members=ReviewOfficialTransportCases.Leaves(JsonSerializer.SerializeToNode(new SdkBindingFixture(name).Expected)!).ToArray();
                for(var memberIndex=0;memberIndex<members.Length;memberIndex++)yield return new object[]{name,memberIndex,members[memberIndex].Label};
            }
        }
    }
}
[CollectionDefinition("SDK native binding isolation",DisableParallelization=true)]
public sealed class SdkNativeBindingCollection { }
[Collection("SDK native binding isolation")]
public sealed class TestGateSdkNativeBindingMockTests
{
    [Fact]
    public async Task Required_StringTools_manifest_omission_is_rejected_before_any_SDK_read()
    {
        var fixture=new SdkBindingFixture("explicit-native-project");
        await SdkBindingCases.AssertMissingStringToolsAsync(fixture,pins=>new ScriptedSdkBindingFactory(fixture,pins));
    }
    [Theory,MemberData(nameof(SdkBindingCases.Data),MemberType=typeof(SdkBindingCases))]
    public async Task Explicit_native_binding_fixture_before_implementation(string name)
    {
        var fixture=new SdkBindingFixture(name);
        var actual=await SdkBindingCases.ExecuteAsync(fixture,pins=>new ScriptedSdkBindingFactory(fixture,pins));
        SdkBindingCases.AssertContract(fixture,JsonSerializer.SerializeToNode(actual)!);
    }
    [Theory,MemberData(nameof(SdkBindingCases.Controls),MemberType=typeof(SdkBindingCases))]
    public void Each_pin_input_native_projection_or_lifecycle_mutation_is_rejected(string name,int memberIndex,string label)
    {
        var fixture=new SdkBindingFixture(name);var actual=JsonSerializer.SerializeToNode(fixture.Expected)!;
        var member=ReviewOfficialTransportCases.Leaves(actual).ElementAt(memberIndex);Assert.Equal(label,member.Label);
        ReviewOfficialTransportCases.Mutate(actual,member.Path);Assert.ThrowsAny<Exception>(()=>SdkBindingCases.AssertContract(fixture,actual));
    }
}
