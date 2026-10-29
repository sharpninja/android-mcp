using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Review;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Review;
public sealed class ReviewRequirementNormalizationNativeTests
{
    [Theory,MemberData(nameof(ReviewRequirementNormalizationCases.Data),MemberType=typeof(ReviewRequirementNormalizationCases))]
    public async Task Same_documented_payload_cases_bind_to_native_normalizer_and_shared_TRX_authority(string name)
    {
        var store=new ReviewMemoryStore();var head=new string('a',40);const string fr="FR-BUILD-001",tr="TR-BUILD-TOOL-001",test="TEST-BUILD-001";
        var condition=name=="unsupported-coverage-statement"?"Generic test without criterion IDs":"Cover FR-BUILD-001 ac-1 in Fixture.Coverage";
        JsonArray Criteria()=>new(JsonSerializer.SerializeToNode(new{id="ac-1",text="Complete exact evidence",isSatisfied=false}));
        var functional=JsonSerializer.SerializeToNode(new{id=fr,body="Functional body",title="FR",status="pending",notes="retain-fr",custom=new{value=7}})!.AsObject();functional["acceptanceCriteria"]=Criteria();
        var technical=JsonSerializer.SerializeToNode(new{id=tr,body="Technical body",title="TR",status="pending"})!.AsObject();technical["acceptanceCriteria"]=Criteria();
        var testing=JsonSerializer.SerializeToNode(new{id=test,condition,title="TEST",status="pending"})!.AsObject();testing["acceptanceCriteria"]=Criteria();
        var mapping=JsonSerializer.SerializeToNode(new{frId=fr,trIds=new[]{tr},testIds=new[]{test},notes="retain-mapping"})!.AsObject();
        if(name=="wrong-exact-id")functional["id"]="FR-BUILD-002";
        if(name=="foreign-test-id")testing["id"]="TEST-OTHER-001";
        if(name=="missing-acceptance-criterion")functional["acceptanceCriteria"]=new JsonArray();
        if(name=="mapping-without-tests")mapping.Remove("testIds");
        StoredArtifact Put(string path,JsonNode body)
        {var bytes=Encoding.UTF8.GetBytes(body.ToJsonString());store.Values[path]=bytes;return new(path,ReviewFixture.Hash(bytes),bytes.Length,"application/json");}
        var fpin=Put("fr.json",name=="unknown-response-envelope"?JsonSerializer.SerializeToNode(new{result=new{item=functional}})!:functional);
        var trpin=Put("tr.json",technical);var tpin=Put("test.json",testing);var mpin=Put("mapping.json",mapping);
        const string xml="<TestRun xmlns='http://microsoft.com/schemas/VisualStudio/TeamTest/2010'><ResultSummary outcome='Completed'><Counters total='1' executed='1' passed='1' failed='0' notExecuted='0'/></ResultSummary><Results><UnitTestResult testId='t1' executionId='e1' outcome='Passed'/></Results><TestDefinitions><UnitTest id='t1'><TestMethod className='Fixture.Coverage' name='NamedCriterion'/></UnitTest></TestDefinitions></TestRun>";
        var trxBytes=Encoding.UTF8.GetBytes(xml);store.Values["full.trx"]=trxBytes;var trxPin=new StoredArtifact("full.trx",ReviewFixture.Hash(trxBytes),trxBytes.Length,"application/xml");
        var proof=JsonSerializer.SerializeToNode(new{head=name=="wrong-coverage-head"?new string('b',40):head,
            criteria=new[]{new{frId=fr,acId="ac-1",testId=test,testCondition=condition,testRecord=tpin,trx=trxPin,
                classes=new[]{name=="missing-tested-class"?"Absent.Class":"Fixture.Coverage"}}}})!;
        var coveragePin=Put("coverage.json",proof);
        if(name=="changed-record-pin")fpin=fpin with{Sha256=new string('0',64)};
        var records=new List<ReviewRequirementRecordPin>{new(ReviewRequirementRecordKind.Functional,fr,fpin),new(ReviewRequirementRecordKind.Technical,tr,trpin),
            new(ReviewRequirementRecordKind.Testing,name=="foreign-test-id"?"TEST-OTHER-001":test,tpin),new(ReviewRequirementRecordKind.Mapping,fr,mpin)};
        if(name=="duplicate-record")records.Add(records[0]);
        var request=new ReviewRequirementNormalizationRequest(head,"/workspace","mcpserver-codex-plugin","1.121.0",[fr],[tr],[test],records,name=="missing-explicit-coverage"?null:coveragePin);
        RequirementNormObservation observation;
        try
        {
            var result=await new ReviewRequirementRecordNormalizer(new PinnedArtifactReader(store),new CanonicalJson(),new TrxResultAdmission()).NormalizeAsync(request);
            var root=JsonNode.Parse(result.Json)!;
            var fs=root["functional"]!.AsArray();var ts=root["technical"]!.AsArray();var tests=root["testing"]!.AsArray();var maps=root["mappings"]!.AsArray();
            Assert.Equal("retain-fr",fs[0]!["notes"]!.GetValue<string>());Assert.Equal(7,fs[0]!["custom"]!["value"]!.GetValue<int>());
            Assert.Equal("retain-mapping",maps[0]!["notes"]!.GetValue<string>());Assert.Equal(condition,tests[0]!["condition"]!.GetValue<string>());
            observation=new("normalized",fs.Count+ts.Count+tests.Count,maps.Count,maps[0]!["criteria"]!.AsArray().Count,
                fs[0]!["status"]!.GetValue<string>()+":"+fs[0]!["acceptanceCriteria"]![0]!["isSatisfied"]!.ToJsonString(),result.Inputs.Count,root["source"] is null,0);
        }
        catch(ReviewException error){observation=new(error.Code,0,0,0,"",0,true,0);}
        catch(PinnedArtifactException error){observation=new(error.Code,0,0,0,"",0,true,0);}
        ReviewRequirementNormalizationCases.AssertContract(name,observation);
    }
}
