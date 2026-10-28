using System.Text;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed record ReconstructionCase(string Id,FrozenEvidenceInput Input,Artifact View,Artifact Map,Artifact Original,string Error)
{
    public override string ToString()=>Id;
}
public static class ReconstructionContractCases
{
    public static IEnumerable<object[]> Cases=>Create().Select(c=>new object[]{c});
    public static IReadOnlyList<ReconstructionCase> Create()
    {
        var input=LosslessContractCases.Input(LosslessContractCases.KnownUri);
        var factorCase=new LosslessCase("FACTOR-KNOWN-SINGLE",input,LosslessContractCases.KnownReference,1,0,1);
        var factored=LosslessContractCases.Correct(factorCase);var original=input.Artifacts.Single(a=>a.RelativePath==input.HtmlPath);
        ReconstructionCase Case(string id,string error,Func<FrozenEvidenceInput,FrozenEvidenceInput>? changeInput=null,Func<Artifact,Artifact>? changeView=null,Action<JsonNode>? changeMap=null)
        {
            var clone=input with{Artifacts=input.Artifacts.Select(a=>a with{Bytes=a.Bytes.ToArray()}).ToArray(),Manifest=input.Manifest.ToArray()};
            var map=JsonNode.Parse(factored.ReconstructionMap.Bytes.Span)!;changeMap?.Invoke(map);
            return new(id,changeInput?.Invoke(clone)??clone,changeView?.Invoke(factored.ReviewHtml)??factored.ReviewHtml,factored.ReconstructionMap with{Bytes=Encoding.UTF8.GetBytes(map.ToJsonString())},original,error);
        }
        FrozenEvidenceInput ChangeFile(FrozenEvidenceInput i,string path)=>i with{Artifacts=i.Artifacts.Select(a=>a.RelativePath==path?a with{Bytes=Encoding.UTF8.GetBytes("changed")}:a).ToArray()};
        var unknownView=LosslessContractCases.KnownReference+" review-asset://sha256/"+new string('f',64);
        return
        [
            Case("RESTORE-ORIGINAL",""),
            Case("RESTORE-VIEW-CHANGED","VIEW_HASH_CHANGED",changeView:a=>a with{Bytes=Encoding.UTF8.GetBytes("changed")}),
            Case("RESTORE-ASSET-CHANGED","FROZEN_HASH_CHANGED",i=>ChangeFile(i,"known.svg")),
            Case("RESTORE-HTML-CHANGED","FROZEN_HASH_CHANGED",i=>ChangeFile(i,"page.html")),
            Case("RESTORE-URI-HASH-CHANGED","URI_HASH_CHANGED",changeMap:j=>j["replacements"]![0]!["original_uri_sha256"]=new string('0',64)),
            Case("RESTORE-ASSET-HASH-CHANGED","ASSET_HASH_CHANGED",changeMap:j=>j["replacements"]![0]!["asset_sha256"]=new string('0',64)),
            Case("RESTORE-ORIGINAL-HASH-CHANGED","RESTORED_HASH_CHANGED",changeMap:j=>j["original_sha256"]=new string('0',64)),
            Case("RESTORE-UNKNOWN-TOKEN","UNKNOWN_REFERENCE",changeView:a=>a with{Bytes=Encoding.UTF8.GetBytes(unknownView)},changeMap:j=>j["view_sha256"]=DocumentationFixture.Hash(Encoding.UTF8.GetBytes(unknownView))),
            Case("RESTORE-MISSING-MAPPING","UNKNOWN_REFERENCE",changeMap:j=>j["replacements"]!.AsArray().Clear()),
            Case("RESTORE-MISSING-ASSET","ASSET_MISSING",changeMap:j=>j["replacements"]![0]!["asset"]="absent.svg"),
            Case("RESTORE-CONFLICTING-MAPPING","AMBIGUOUS_REFERENCE",changeMap:j=>{var duplicate=j["replacements"]![0]!.DeepClone();duplicate["asset"]="other.svg";j["replacements"]!.AsArray().Add(duplicate);}),
            Case("RESTORE-ORIGINAL-PATH-CHANGED","ORIGINAL_PATH_CHANGED",changeMap:j=>j["original_artifact"]="different.html")
        ];
    }

    public static void AssertContract(ReconstructionCase test,ILosslessEvidenceService provider)
    {
        var before=test.Input.Artifacts.Select(a=>DocumentationFixture.Hash(a.Bytes.Span)).ToArray();
        Artifact? result=null;var failure=Record.Exception(()=>result=provider.Reconstruct(test.Input,test.View,test.Map));
        if(test.Error.Length>0)Assert.Equal(test.Error,Assert.IsType<EvidenceIntegrityException>(failure).Code);
        else
        {
            Assert.Null(failure);Assert.NotNull(result);Assert.Equal(test.Original.RelativePath,result.RelativePath);Assert.Equal(test.Original.MediaType,result.MediaType);Assert.Equal(test.Original.Bytes.ToArray(),result.Bytes.ToArray());
        }
        Assert.Equal(before,test.Input.Artifacts.Select(a=>DocumentationFixture.Hash(a.Bytes.Span)).ToArray());
    }
}

public sealed class ScriptedReconstruction(Artifact result,string error="",bool mutate=false):ILosslessEvidenceService
{
    public (FrozenEvidenceInput Input,Artifact View,Artifact Map)? Received {get;private set;}
    public FactoredEvidence Factor(FrozenEvidenceInput input)=>throw new NotSupportedException("Reconstruction-only mock boundary");
    public Artifact Reconstruct(FrozenEvidenceInput input,Artifact view,Artifact map)
    {
        Received=(input,view,map);
        if(mutate){var array=(Artifact[])input.Artifacts;array[0]=array[0] with{Bytes=Encoding.UTF8.GetBytes("unexpected mutation")};}
        if(error.Length>0)throw new EvidenceIntegrityException(error,"Scripted reconstruction rejection");return result;
    }
}

[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class LosslessReconstructionMockTests
{
    [Theory,MemberData(nameof(ReconstructionContractCases.Cases),MemberType=typeof(ReconstructionContractCases))]
    public void CorrectReconstructionMocksPreserveOriginalMutationRejection(ReconstructionCase test)
    {
        var mock=new ScriptedReconstruction(test.Original,test.Error);ReconstructionContractCases.AssertContract(test,mock);
        Assert.Same(test.Input,mock.Received!.Value.Input);Assert.Same(test.View,mock.Received.Value.View);Assert.Same(test.Map,mock.Received.Value.Map);
    }
    [Theory,MemberData(nameof(ReconstructionContractCases.Cases),MemberType=typeof(ReconstructionContractCases))]
    public void WrongReconstructionOutcomeIsIndividuallyRejected(ReconstructionCase test)=>Assert.NotNull(Record.Exception(()=>ReconstructionContractCases.AssertContract(test,new ScriptedReconstruction(test.Original,"WRONG_ERROR"))));
    [Theory,MemberData(nameof(ReconstructionContractCases.Cases),MemberType=typeof(ReconstructionContractCases))]
    public void FrozenMutationIsRejectedOnSuccessAndFailure(ReconstructionCase test)
    {
        var originals=test.Input.Artifacts.ToArray();
        try{Assert.NotNull(Record.Exception(()=>ReconstructionContractCases.AssertContract(test,new ScriptedReconstruction(test.Original,test.Error,true))));}
        finally{var array=(Artifact[])test.Input.Artifacts;for(var i=0;i<array.Length;i++)array[i]=originals[i];}
    }
    [Fact]
    public void WrongRestoredBytesPathAndMediaAreEachRejected()
    {
        var test=ReconstructionContractCases.Create().Single(c=>c.Error.Length==0);
        foreach(var wrong in new[]{test.Original with{RelativePath="wrong"},test.Original with{MediaType="wrong"},test.Original with{Bytes=Encoding.UTF8.GetBytes("wrong")}})
            Assert.NotNull(Record.Exception(()=>ReconstructionContractCases.AssertContract(test,new ScriptedReconstruction(wrong))));
    }
}
