using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review.Staging;

public sealed class SdkReviewMetadataNativeTests
{
    [Theory,MemberData(nameof(SdkReviewMetadataCases.Data),MemberType=typeof(SdkReviewMetadataCases))]
    public async Task Same_packets_bind_to_existing_native_sdk_evaluator_property_and_child_adapters(string name)
    {
        var fixture=new SdkReviewMetadataFixture(name);
        SdkReviewMetadataCases.AssertContract(fixture,await Execute(fixture));
    }

    private static TestGateProjectTestMetadata Metadata(SdkReviewMetadataFixture fixture,string project)
    {var value=fixture.MetadataFor(project);return new(value.IsTestProject,value.Kind);}
    private static JsonNode Node<T>(T value)=>JsonSerializer.SerializeToNode(value)!;
    private static async Task<SdkReviewMetadataObservation> Execute(SdkReviewMetadataFixture f)
    {
        if(f.Name is "legacy-wire" or "populated-wire")
        {
            var config=f.Source.Configuration;var wireRequest=f.Source.Request;var native=f.Source.Projects[f.Host];var result=f.Source.Expected;
            if(f.Name=="populated-wire")
            {
                config=config with{ProjectInventory=f.Graph};wireRequest=wireRequest with{ProjectInventory=f.Graph};
                native=native with{TestMetadata=Metadata(f,f.Host)};
                result=result with{Projects=result.Projects.Select(project=>project with{TestMetadata=Metadata(f,project.ProjectPath)}).ToArray()};
            }
            return new("OK",new JsonObject{["Configuration"]=Node(config),["Request"]=Node(wireRequest),["Native"]=Node(native),["Evaluation"]=Node(result)},[],0,0,0,0,0,[]);
        }
        if(f.Name.StartsWith("property-",StringComparison.Ordinal))
        {
            var project=new MetadataProject(f.Name);var code="OK";TestGateProjectTestMetadata? observed=null;
            try{observed=NativeTestGateSdkSessionFactory.ObserveTestMetadata(project);}
            catch(TestGateFactsException error){code=error.Code;}
            return new(code,observed is null?null:Node(observed),[],0,0,0,0,0,project.Reads);
        }
        if(f.Name=="facts-forward")
        {
            var fixture=new NativeFactsFixture("sdk-source-graph");var io=new NativeFactsDependencies(fixture);
            var factsConfiguration=fixture.Configuration with{ProjectInventory=[fixture.Request.HostProject,fixture.Request.TestsProject]};
            var facts=new SdkEvaluatedTestGateNativeFacts(factsConfiguration,io,io,io,io,io,io);
            await facts.CaptureSourceAsync(fixture.Request,TestGateSnapshotPoint.BeforeBuild);
            Assert.Equal(fixture.SourceReads().Count(),io.Files.Count);Assert.Equal(2,io.Writes.Count);
            return new("OK",new JsonObject{["ProjectInventory"]=Node(io.Evaluations.Single().ProjectInventory)},[],0,0,0,0,0,[]);
        }
        if(f.IsChild)return await Child(f);
        var configuration=f.Source.Configuration with{ProjectInventory=f.Inventory};
        var request=f.Source.Request with{ProjectInventory=f.Name=="inventory-mismatch"?null:f.Inventory};
        foreach(var path in f.Source.Projects.Keys.ToArray())f.Source.Projects[path]=f.Source.Projects[path] with{TestMetadata=Metadata(f,path)};
        f.Source.Projects[f.Extra]=new(f.Extra,"/fixture/sdk","Debug","net10.0","/fixture/app/Unreferenced/Extra.dll",[],[],new Dictionary<string,string>(),[],new("false","none"));
        var source=new SdkSourceEvaluationDependencies(f.Source);var evaluator=new NativeTestGateSdkEvaluator(configuration,source,source);
        var status="OK";TestGateSdkEvaluation? evaluation=null;
        try{evaluation=await evaluator.EvaluateAsync(request);}
        catch(TestGateFactsException error){status=error.Code;}
        return new(status,evaluation is null?null:Node(evaluation),source.Loads,source.Opens.Count,source.Disposals,0,0,0,[]);
    }

    private static async Task<SdkReviewMetadataObservation> Child(SdkReviewMetadataFixture f)
    {
        var old=f.Child;var request=JsonSerializer.Deserialize<TestGateSdkChildRequest>(f.ChildRequestPayload().ToJsonString())!;
        var bytes=new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(f.SourcePayload(f.Name=="child-extra-result")));
        old.Files[old.EvaluationPath]=bytes;
        var outputPin=new StoredArtifact(old.EvaluationPath,ContentHash.Sha256(bytes),bytes.Length,"application/json");
        var requestBytes=new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(request));
        var receipt=new TestGateSdkChildReceipt(ContentHash.Sha256(requestBytes),request.Monolith,outputPin);
        var io=new SdkChildDependencies(old);var captures=new ChildCaptures(io,new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(receipt)));
        var configuration=new TestGateNativeFactsConfiguration(old.Native.Evaluation.Sdk,old.Native.ApprovedReadRoots,"Debug","net10.0",
            f.Name=="child-inventory-mismatch"?null:f.Graph);
        var evaluator=new NativeTestGateSdkChildEvaluator(configuration,request.Monolith,old.Files["bin/App.dll"].LongLength,
            request.RuntimeFiles,io,io,io,captures,new NoNonce());
        var code="OK";TestGateSdkEvaluation? result=null;
        try{result=await evaluator.EvaluateAsync(request);}
        catch(TestGateFactsException error){code=error.Code;}
        JsonNode? payload=null;
        if(io.Writes.Count>0)
        {
            payload=new JsonObject{["Request"]=JsonNode.Parse(Convert.FromBase64String(io.Writes.Single().BytesBase64))};
            if(result is not null)payload["Evaluation"]=Node(result);
        }
        return new(code,payload,[],0,0,io.Processes.Count,io.Pins.Count,io.Writes.Count,[]);
    }
    public sealed class MetadataProject(string name)
    {
        public List<string> Reads{get;}=[];
        public string? GetPropertyValue(string property)
        {
            Reads.Add(property);
            if(name=="property-error")return null;
            if(name=="property-empty")return "";
            return property=="IsTestProject"?"true":"mixed";
        }
    }
    private sealed class ChildCaptures(SdkChildDependencies inner,byte[] stdout):IProcessCaptureReader
    {
        public Task<ReadOnlyMemory<byte>> ReadAsync(ProcessResult result,bool standardError,CancellationToken cancellationToken=default)
            =>standardError?inner.ReadAsync(result,true,cancellationToken):Task.FromResult<ReadOnlyMemory<byte>>(stdout);
    }
    private sealed class NoNonce:ITestGateNonceSource
    {public string CreateNonce()=>throw new InvalidOperationException("Explicit-envelope test must not mint another request.");}
}
