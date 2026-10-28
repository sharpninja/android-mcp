using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review.Staging;

public sealed record SdkChildDefaultBind(string Context,IReadOnlyDictionary<string,string> AssemblyPaths);
public sealed record SdkChildBindingObservation(string Code,TestGateSdkNativeOpenRequest Request,IReadOnlyCollection<string> InitiallyLoadedNames,IReadOnlyList<StoredArtifact> PinReads,
    IReadOnlyList<SdkChildDefaultBind> Binds,IReadOnlyList<string> ProjectLoads,
    IReadOnlyList<TestGateSdkNativeProject> Projects,int Disposals,IReadOnlyList<string> Order);

public sealed class SdkChildBindingFixture
{
    public static readonly string[] Names=["admitted-child","missing-framework-pin","bad-sdk-pin",
        "framework-already-loaded","build-already-loaded","causal-bind-failure","escaped-sdk-pin"];
    public string Name { get; }
    public SdkBindingFixture Source { get; }=new("explicit-native-project");
    public TestGateSdkNativeOpenRequest Request { get; }
    public IReadOnlyCollection<string> LoadedNames=>Name switch
    {"framework-already-loaded"=>["Microsoft.Build.Framework"],"build-already-loaded"=>["Microsoft.Build"],_=>[]};
    public string Code=>Name switch
    {"missing-framework-pin"=>"SDK_PIN_MISSING","bad-sdk-pin"=>"SDK_PIN_MISMATCH",
     "framework-already-loaded" or "build-already-loaded"=>"SDK_CHILD_CONTEXT_CONFLICT",
     "causal-bind-failure"=>"SDK_EVALUATION_FAILED","escaped-sdk-pin"=>"SDK_PIN_MISMATCH",_=>"OK"};
    public SdkChildBindingFixture(string name)
    {
        Name=name;var pins=Source.OpenRequest.Evaluation.Sdk.AssemblyFiles.ToList();
        if(name=="missing-framework-pin")pins.RemoveAll(pin=>pin.RelativePath=="Microsoft.Build.Framework.dll");
        if(name=="bad-sdk-pin")pins[0]=pins[0] with {Sha256=new string('0',64)};
        if(name=="escaped-sdk-pin")pins.Add(new("../escape.dll",new string('e',64),1,"application/octet-stream"));
        Request=Source.OpenRequest with {Evaluation=Source.OpenRequest.Evaluation with {Sdk=Source.OpenRequest.Evaluation.Sdk with {AssemblyFiles=pins}}};
    }
    public SdkChildDefaultBind Binding=>new("Default",Source.OpenRequest.Evaluation.Sdk.AssemblyFiles
        .Where(pin=>pin.RelativePath.EndsWith(".dll",StringComparison.OrdinalIgnoreCase))
        .ToDictionary(pin=>Path.GetFileNameWithoutExtension(pin.RelativePath),pin=>Path.Combine(Source.SdkRoot,pin.RelativePath),StringComparer.OrdinalIgnoreCase));
    private IReadOnlyList<StoredArtifact> ExpectedPins=>
        Name=="missing-framework-pin" ? [] : Name=="bad-sdk-pin" ? Request.Evaluation.Sdk.AssemblyFiles.Take(1).ToArray()
            : Source.OpenRequest.Evaluation.Sdk.AssemblyFiles;
    public SdkChildBindingObservation Expected=>new(Code,Request,LoadedNames,ExpectedPins,
        Name is "admitted-child" or "causal-bind-failure" ? [Binding] : [],
        Code=="OK" ? [Source.ProjectPath] : [],Code=="OK" ? [Source.ExpectedProject(Request)] : [],Code=="OK" ? 1 : 0,
        [..ExpectedPins.Select(pin=>"pin:"+pin.RelativePath),..(Name is "admitted-child" or "causal-bind-failure" ? new[]{"bind:Default"} : []),
         ..(Code=="OK" ? new[]{"project:"+Source.ProjectPath,"dispose"} : [])]);
}

public sealed class SdkChildBindingDependencies(SdkChildBindingFixture fixture)
{
    public List<string> Order { get; }=[];
    public SdkBindingPinReader RecordedPins { get; }=new(fixture.Source);
    public IPinnedArtifactReader Pins=>new OrderedPins(this);
    public List<SdkChildDefaultBind> Binds { get; }=[];
    public List<string> Loads { get; }=[];public List<TestGateSdkNativeProject> Projects { get; }=[];
    public int Disposals { get; private set; }
    public ITestGateSdkNativeSession Bind(IReadOnlyDictionary<string,string> paths)
    {
        Binds.Add(new("Default",paths));Order.Add("bind:Default");
        if(fixture.Name=="causal-bind-failure")throw new IOException("Exact causal SDK child binder failure.");
        return new Session(this,fixture);
    }
    public SdkChildBindingObservation Observe(string code)=>new(code,fixture.Request,fixture.LoadedNames,RecordedPins.Reads,Binds,Loads,Projects,Disposals,Order);
    private sealed class OrderedPins(SdkChildBindingDependencies owner):IPinnedArtifactReader
    {
        public Task<ReadOnlyMemory<byte>> ReadAsync(StoredArtifact pin,CancellationToken cancellationToken=default)
        {owner.Order.Add("pin:"+pin.RelativePath);return owner.RecordedPins.ReadAsync(pin,cancellationToken);}
    }
    private sealed class Session(SdkChildBindingDependencies owner,SdkChildBindingFixture fixture):ITestGateSdkNativeSession
    {
        public Task<TestGateSdkNativeProject> LoadProjectAsync(string path,CancellationToken cancellationToken=default)
        {
            owner.Loads.Add(path);owner.Order.Add("project:"+path);var project=fixture.Source.ExpectedProject(fixture.Request);
            owner.Projects.Add(project);return Task.FromResult(project);
        }
        public ValueTask DisposeAsync(){owner.Disposals++;owner.Order.Add("dispose");return ValueTask.CompletedTask;}
    }
}

public sealed class ScriptedSdkChildAdmission(SdkChildBindingFixture fixture,SdkChildBindingDependencies io)
{
    public async Task<ITestGateSdkNativeSession> OpenAsync()
    {
        var request=fixture.Request;
        if(SdkBindingFixture.RequiredPins.Any(required=>!request.Evaluation.Sdk.AssemblyFiles.Any(pin=>pin.RelativePath==required)))
            throw new TestGateFactsException("SDK_PIN_MISSING","Required fixed SDK pin is absent.");
        var paths=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(var pin in request.Evaluation.Sdk.AssemblyFiles)
        {
            string path;
            try{path=ArtifactPaths.Resolve(fixture.Source.SdkRoot,pin.RelativePath);await io.Pins.ReadAsync(pin);}
            catch(Exception error)when(error is ArgumentException or PinnedArtifactException)
            {throw new TestGateFactsException("SDK_PIN_MISMATCH","Exact scoped SDK bytes differ.");}
            if(path.EndsWith(".dll",StringComparison.OrdinalIgnoreCase))paths.Add(Path.GetFileNameWithoutExtension(path),path);
        }
        if(fixture.LoadedNames.Any(paths.ContainsKey))throw new TestGateFactsException("SDK_CHILD_CONTEXT_CONFLICT","Selected SDK identity already exists in Default.");
        try{return io.Bind(paths);}
        catch(IOException error){throw new TestGateFactsException("SDK_EVALUATION_FAILED",error.ToString());}
    }
}
public static class SdkChildBindingCases
{
    public static IEnumerable<object[]> Data=>SdkChildBindingFixture.Names.Select(name=>new object[]{name});
    public static void AssertContract(SdkChildBindingFixture fixture,JsonNode actual)=>Assert.Equal(
        new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(fixture.Expected)),
        new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(actual)));
    public static IEnumerable<object[]> Controls
    {
        get
        {
            foreach(var name in SdkChildBindingFixture.Names)
            {
                var members=ReviewOfficialTransportCases.Leaves(JsonSerializer.SerializeToNode(new SdkChildBindingFixture(name).Expected)!).ToArray();
                for(var memberIndex=0;memberIndex<members.Length;memberIndex++)yield return new object[]{name,memberIndex,members[memberIndex].Label};
            }
        }
    }
    public static async Task<SdkChildBindingObservation> ObserveAsync(SdkChildBindingFixture fixture,SdkChildBindingDependencies io,
        Func<Task<ITestGateSdkNativeSession>> open)
    {
        var code="OK";
        try{await using var session=await open();await session.LoadProjectAsync(fixture.Source.ProjectPath);}
        catch(TestGateFactsException error){code=error.Code;}
        return io.Observe(code);
    }
}
public sealed class TestGateSdkChildBindingMockTests
{
    [Theory,MemberData(nameof(SdkChildBindingCases.Data),MemberType=typeof(SdkChildBindingCases))]
    public async Task Exact_pins_before_Default_binding_and_causal_failures(string name)
    {
        var fixture=new SdkChildBindingFixture(name);var io=new SdkChildBindingDependencies(fixture);
        var observed=await SdkChildBindingCases.ObserveAsync(fixture,io,new ScriptedSdkChildAdmission(fixture,io).OpenAsync);
        SdkChildBindingCases.AssertContract(fixture,JsonSerializer.SerializeToNode(observed)!);
    }
    [Theory,MemberData(nameof(SdkChildBindingCases.Controls),MemberType=typeof(SdkChildBindingCases))]
    public void Each_binding_input_scope_and_projection_field_mutation_is_rejected(string name,int memberIndex,string label)
    {
        var fixture=new SdkChildBindingFixture(name);var observed=JsonSerializer.SerializeToNode(fixture.Expected)!;
        var member=ReviewOfficialTransportCases.Leaves(observed).ElementAt(memberIndex);Assert.Equal(label,member.Label);
        ReviewOfficialTransportCases.Mutate(observed,member.Path);Assert.ThrowsAny<Exception>(()=>SdkChildBindingCases.AssertContract(fixture,observed));
    }
}
