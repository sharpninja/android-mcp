using System.Text.Json.Nodes;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record ResolverInput(JsonObject[] Inventory,JsonObject Facts,string RegistryId,long CatalogGeneration=1,long OverlayGeneration=0,long Epoch=0,long Generation=1);
public sealed record ResolverDependency(string EntryId,string ContractHash);
public sealed record ResolverRow(string Id,string Kind,string State,string[] Reasons,string? ContractHash,string? SchemaHash,string? GraphDigest,
    ResolverDependency[] Dependencies,string CatalogState="active",long CatalogRevision=0,string? DocumentHash=null);
public sealed record ResolverReply(ResolverRow[] Rows,string RegistryId,long CatalogGeneration,long OverlayGeneration,long Epoch,long Generation,int InventoryCount);
public sealed record ResolverCase(string Id,string OriginalTest,ResolverInput Input,ResolverRow[] Expected);
public interface IResolverCaseProvider { ResolverReply Resolve(ResolverInput input); }

public static class ResolverCases
{
    public static string H(char value)=>new(value,64);
    public const string GraphAA="09c672efec325207a97a4920a3959119fb7c4581be3681e8dd3437751737d346";
    public const string GraphBB="1806646804e0085ed09dc67efd77ae40aa1476af2f4083649aeebeea26c89ee9";
    public const string GraphBA="a74918c1216ac41a5ff912994fd0221d12b32a1fb20681ef03999934f1e8a69e";
    public const string GraphCA="a1a1750dbbf2a1a23f6ce815851b7ea95ce9494c89a2343cc9b10a2f8b36cd31";
    public const string GraphAB="dc3ffedaad5ad188ec9a1825ebb28d3c99e015db980fb1af87a0cbc8e10a3d8a";
    public const string SchemaAA="b4bf8b0e5b3252ba32877d3bdf9a5e8de040c52b63b1046c4ddaecaf927ff33a";
    public const string SchemaBB="69c088ce5544130aa79a7612268a7b8f3022bcb559ec26430cd78585fb5e4b15";
    public const string SchemaBA="9a19f89a3a5e5737ea648155407f7e8356c1b53e7ec7ce76fd3a42162e7b2e58";
    public const string SchemaCA="9ff9adf70a3facd473cb88f547319356d8237ce7c61aaf51741939536ba1a6c4";
    public const string SchemaA_AB="00fa03e0f2e9030fad21866c4f4a968034f7d6b7d5dba92291ece58d2eb892ac";
    public const string SchemaB_AB="8ac06d6c8ecc33e9f26b331a089eb880fc569d43feeb848ca62d2014f37d7c84";
    public static string DocumentHash(char key,char schema='a')=>(key,schema) switch
    {
        ('a','a')=>"b6916106354d80c9a556be6523f2f211d4e4eac2e2d8c0313a50908cbba509e1",
        ('b','b')=>"4d781e51a03afebceca2f54e17dab31bb59be6393ff9b9c8c5383d435588478e",
        ('b','a')=>"8b65b01f9a6362dcc76451ba3461ee0691c8f331cd69cd2002a3456ae1b8a712",
        ('c','a')=>"2c70531850ba7755f8b833253dce04a52d12a67b5dcde269670d0e5e69ba8ada",
        _=>throw new InvalidOperationException("Missing independent fixture hash")
    };
    public static JsonObject Facts()=>JsonNode.Parse("""
    {"profile_id":"fixture","target_key":"platform:local","android_user_id":0,"backend_instance_id":"backend-fixture","device_instance_id":"boot-fixture","identity_verified":true,"capability_epoch":0,"observed_at":"2026-10-27T01:00:00Z","platform":{"major":37,"minor":1},"extensions":{"30":4},"modules":{"m":{"state":"present","version":"12","authoritative":true}},"interfaces":{"a.I":{"state":"reachable","version":2,"hash":"interface-hash-v2","authoritative":true}},"oem":"fixture-oem","build_fingerprint":"fixture-build","evidence_ids":["fixture-observation"]}
    """)!.AsObject();
    public static JsonObject Rule(JsonObject clause)=>new(){["alternatives"]=new JsonArray(clause),["evidence_ids"]=new JsonArray("fixture-source")};
    public static JsonObject Platform(int major,int minor,int? removedMajor=null,int removedMinor=0)=>Rule(new()
    { ["platform"]=new JsonObject{["introduced"]=new JsonArray(major,minor),["removed"]=removedMajor is null ? null : new JsonArray(removedMajor.Value,removedMinor)} });
    public static JsonObject Interface(int? minimum=2,params string[] hashes)=>Rule(new()
    { ["interface"]=new JsonObject{["descriptor"]="a.I",["minimum_version"]=minimum,["accepted_hashes"]=new JsonArray(hashes.Select(x=>(JsonNode?)JsonValue.Create(x)).ToArray())} });
    public static JsonObject Module(string name,string introduced,string? removed=null)=>Rule(new()
    { ["modules"]=new JsonObject{[name]=new JsonObject{["introduced"]=introduced,["removed"]=removed}} });
    public static JsonObject Entity(char key,string kind="binding",JsonObject? availability=null,char schema='a',params ResolverDependency[] dependencies)=>new()
    {
        ["entry_id"]=H(key),["entity_kind"]=kind,["catalog_document_sha256"]=DocumentHash(key,schema),
        ["variants"]=new JsonArray(new JsonObject{["schema_hash"]=H(schema),["availability"]=availability ?? Platform(37,0),
            ["dependencies"]=new JsonArray(dependencies.Select(x=>(JsonNode)new JsonObject{["entry_id"]=x.EntryId,["schema_hash"]=x.ContractHash}).ToArray())})
    };
    public static ResolverRow Available(char key,string kind="binding",char contract='a',ResolverDependency[]? dependencies=null,string? graph=null,string? schema=null)=>
        new(H(key),kind,"available",["APPLICABLE_EVIDENCE"],H(contract),schema ?? ((key,contract) switch {('a','a')=>SchemaAA,('b','b')=>SchemaBB,('b','a')=>SchemaBA,('c','a')=>SchemaCA,_=>throw new InvalidOperationException()}),
            graph ?? ((key,contract) switch {('a','a')=>GraphAA,('b','b')=>GraphBB,('b','a')=>GraphBA,('c','a')=>GraphCA,_=>throw new InvalidOperationException()}),dependencies ?? [],DocumentHash:DocumentHash(key,contract));
    public static ResolverRow Inactive(char key,string state,string reason,string kind="binding",char schema='a',bool chosen=false,params ResolverDependency[] dependencies)=>
        new(H(key),kind,state,[reason],chosen ? H(schema) : null,null,null,dependencies,DocumentHash:DocumentHash(key,schema));
    private static ResolverCase Case(string id,string original,JsonObject[] inventory,ResolverRow[] expected,Action<JsonObject>? changeFacts=null)
    { var facts=Facts();changeFacts?.Invoke(facts);return new(id,original,new(inventory,facts,H('f')),expected); }
    public static IEnumerable<object[]> All=>Generate().Select(x=>new object[]{x});
    public static IEnumerable<ResolverCase> Generate()
    {
        yield return Case("RES-01-major-minor-removed","test_major_minor_and_removed_apply_to_bindings_and_types",
            [Entity('a',availability:Platform(37,2)),Entity('b',"type",Platform(36,0,37,1))],
            [Inactive('a',"unavailable","PLATFORM_VERSION_EXCLUDED"),Inactive('b',"unavailable","PLATFORM_VERSION_EXCLUDED","type")]);
        yield return Case("RES-02-unknown-minor","test_minor_unknown_does_not_become_zero",
            [Entity('a',availability:Platform(37,2)),Entity('b',"type")],
            [Inactive('a',"unknown","PLATFORM_MINOR_UNKNOWN"),Available('b',"type")],f=>f["platform"]!["minor"]=null);
        JsonObject Extensions()=>new(){["alternatives"]=new JsonArray(new JsonObject{["platform"]=new JsonObject{["introduced"]=new JsonArray(38,0),["removed"]=null}},new JsonObject{["extensions"]=new JsonObject{["30"]=4}}),["evidence_ids"]=new JsonArray("fixture-source")};
        yield return Case("RES-03-extension-alternative","test_extension_alternative_is_not_android_minor",[Entity('a',availability:Extensions())],[Available('a')]);
        yield return Case("RES-04-missing-extension","test_extension_alternative_is_not_android_minor",[Entity('a',availability:Extensions())],[Inactive('a',"unknown","SDK_EXTENSION_UNKNOWN")],f=>f["extensions"]=new JsonObject());
        yield return Case("RES-05-independent-module-oem-build","test_modules_and_oem_builds_are_independent_axes",
            [Entity('a',availability:Module("m","13")),Entity('b',"type",Rule(new(){["oem"]=new JsonArray("other-oem")})),Entity('c',"type",Rule(new(){["build_fingerprints"]=new JsonArray("fixture-build")}))],
            [Inactive('a',"unavailable","MODULE_VERSION_EXCLUDED"),Inactive('b',"unavailable","BUILD_APPLICABILITY_EXCLUDED","type"),Available('c',"type")]);
        yield return Case("RES-06-proven-interface","test_interface_version_hash_and_denied_probe",[Entity('a',availability:Interface(2,"interface-hash-v2"))],[Available('a')]);
        foreach(var state in new[]{"denied","unreachable","unsupported"})
            yield return Case("RES-07-interface-"+state,"test_authoritative_absence_differs_from_unreachable",[Entity('a',availability:Interface(1))],[Inactive('a',"unknown","INTERFACE_OBSERVATION_UNAVAILABLE")],f=>f["interfaces"]!["a.I"]!["state"]=state);
        yield return Case("RES-08-interface-hash","test_interface_version_hash_and_denied_probe",[Entity('a',availability:Interface(2,"interface-hash-v2"))],[Inactive('a',"unknown","INTERFACE_HASH_UNRECOGNIZED")],f=>f["interfaces"]!["a.I"]!["hash"]="unrecognized");
        yield return Case("RES-09-interface-authoritative-absent","test_authoritative_absence_differs_from_unreachable",[Entity('a',availability:Interface(1))],[Inactive('a',"unavailable","INTERFACE_AUTHORITATIVELY_ABSENT")],f=>f["interfaces"]!["a.I"]!["state"]="absent");
        var dependency=new ResolverDependency(H('b'),H('b'));
        yield return Case("RES-10-dependency-unavailable","test_type_dependencies_and_missing_schema_are_consistent",
            [Entity('a',dependencies:[dependency]),Entity('b',"type",Platform(38,0),schema:'b')],
            [Inactive('a',"unavailable","TYPE_DEPENDENCY_UNAVAILABLE",chosen:true,dependencies:[dependency]),Inactive('b',"unavailable","PLATFORM_VERSION_EXCLUDED","type",'b')]);
        yield return Case("RES-11-dependency-missing","test_type_dependencies_and_missing_schema_are_consistent",[Entity('a',dependencies:[dependency])],
            [Inactive('a',"unknown","TYPE_DEPENDENCY_MISSING",chosen:true,dependencies:[dependency])]);
        var back=new ResolverDependency(H('a'),H('a'));
        yield return Case("RES-12-recursive-type-component","test_recursive_types_resolve_as_one_consistent_graph",
            [Entity('a',"type",dependencies:[dependency]),Entity('b',"type",schema:'b',dependencies:[back])],
            [Available('a',"type",dependencies:[dependency],graph:GraphAB,schema:SchemaA_AB),Available('b',"type",'b',[back],GraphAB,SchemaB_AB)]);
        var ambiguous=Entity('a');ambiguous["variants"]!.AsArray().Add(new JsonObject{["schema_hash"]=H('b'),["availability"]=Module("unknown","1"),["dependencies"]=new JsonArray()});
        yield return Case("RES-13-unknown-variant-ambiguous","test_available_variant_does_not_hide_ambiguous_unknown_variant",[ambiguous],[Inactive('a',"unknown","VARIANT_AMBIGUOUS")]);
        var mismatch=new ResolverDependency(H('b'),H('c'));
        yield return Case("RES-14-type-contract-mismatch","test_dependency_schema_mismatch_does_not_select_a_codec",
            [Entity('a',dependencies:[mismatch]),Entity('b',"type",schema:'b')],
            [Inactive('a',"unknown","TYPE_DEPENDENCY_SCHEMA_MISMATCH",chosen:true,dependencies:[mismatch]),Available('b',"type",'b')]);
        yield return Case("RES-15-unknown-source-lifecycle","test_unknown_lifecycle_is_retained_with_reason",
            [Entity('a',availability:new(){["unknown_reason"]="OEM lifecycle not acquired",["x-raw"]=new JsonObject{["vendor"]=1}})],
            [Inactive('a',"unknown","APPLICABILITY_UNKNOWN")]);
        yield return Case("RES-16-proven-transitive-type","test_availability_is_not_a_permission_grant",
            [Entity('a',dependencies:[dependency]),Entity('b',"type",schema:'b')],
            [Available('a',dependencies:[dependency],graph:GraphAB,schema:SchemaA_AB),Available('b',"type",'b')]);
        yield return Case("RES-17-unverified-identity","FR-BUILD-004/ac-1",[Entity('a')],[Inactive('a',"unknown","DEVICE_IDENTITY_UNVERIFIED")],f=>f["identity_verified"]=false);
        yield return Case("RES-18-other-node","FR-BUILD-004/ac-1",[Entity('a',"other")],[Inactive('a',"unknown","NON_EXECUTABLE_INVENTORY_NODE","other")]);
        yield return Case("RES-19-module-not-authoritative","FR-BUILD-004/ac-1",[Entity('a',availability:Module("m","1"))],[Inactive('a',"unknown","MODULE_PRESENCE_UNPROVEN")],f=>f["modules"]!["m"]!["authoritative"]=false);
        yield return Case("RES-20-module-absent","FR-BUILD-004/ac-1",[Entity('a',availability:Module("m","1"))],[Inactive('a',"unavailable","MODULE_ABSENT")],f=>f["modules"]!["m"]!["state"]="absent");
        yield return Case("RES-21-interface-not-authoritative","FR-BUILD-004/ac-1",[Entity('a',availability:Interface(1))],[Inactive('a',"unknown","INTERFACE_REACHABILITY_UNPROVEN")],f=>f["interfaces"]!["a.I"]!["authoritative"]=false);
        yield return Case("RES-22-missing-type-contract","FR-BUILD-004/ac-1",[Entity('a',dependencies:[dependency]),Entity('b',"type",new(){["unknown_reason"]="Unresolved type"},'b')],
            [Inactive('a',"unknown","TYPE_DEPENDENCY_UNKNOWN",chosen:true,dependencies:[dependency]),Inactive('b',"unknown","APPLICABILITY_UNKNOWN","type",'b')]);
        yield return Case("RES-23-not-type","FR-BUILD-004/ac-1",[Entity('a',dependencies:[dependency]),Entity('b',schema:'b')],
            [Inactive('a',"unknown","TYPE_DEPENDENCY_NOT_TYPE",chosen:true,dependencies:[dependency]),Available('b',contract:'b')]);
        yield return Case("RES-24-malformed-interval","FR-BUILD-004/ac-1",[Entity('a',availability:Platform(37,2,37,1))],[Inactive('a',"unknown","APPLICABILITY_MALFORMED")]);
        var suppressed=Entity('a');suppressed["catalog_state"]="suppressed";suppressed["catalog_revision"]=1;
        yield return Case("RES-25-exposure-separate-from-existence","test_suppression_retains_existence_but_denies_known_dispatch",[suppressed],[Available('a') with{CatalogState="suppressed",CatalogRevision=1}]);
    }
    public static ResolverReply Correct(ResolverCase test)=>new(test.Expected,test.Input.RegistryId,test.Input.CatalogGeneration,test.Input.OverlayGeneration,test.Input.Epoch,test.Input.Generation,test.Input.Inventory.Length);
    public static void AssertCase(ResolverCase test,IResolverCaseProvider provider)
    {
        var factsBefore=test.Input.Facts.ToJsonString();var inventoryBefore=test.Input.Inventory.Select(x=>x.ToJsonString()).ToArray();
        var actual=provider.Resolve(test.Input);
        Assert.Equal(test.Input.RegistryId,actual.RegistryId);Assert.Equal(test.Input.CatalogGeneration,actual.CatalogGeneration);Assert.Equal(test.Input.OverlayGeneration,actual.OverlayGeneration);
        Assert.Equal(test.Input.Epoch,actual.Epoch);Assert.Equal(test.Input.Generation,actual.Generation);Assert.Equal(test.Input.Inventory.Length,actual.InventoryCount);
        Assert.Equal(test.Expected.Select(x=>x.Id),actual.Rows.Select(x=>x.Id));
        foreach(var (expected,row) in test.Expected.Zip(actual.Rows))
        {
            Assert.Equal(expected.Kind,row.Kind);Assert.Equal(expected.State,row.State);Assert.Equal(expected.Reasons,row.Reasons);
            Assert.Equal(expected.ContractHash,row.ContractHash);Assert.Equal(expected.SchemaHash,row.SchemaHash);Assert.Equal(expected.GraphDigest,row.GraphDigest);
            Assert.Equal(expected.Dependencies,row.Dependencies);Assert.Equal(expected.CatalogState,row.CatalogState);Assert.Equal(expected.CatalogRevision,row.CatalogRevision);Assert.Equal(expected.DocumentHash,row.DocumentHash);
        }
        Assert.Equal(factsBefore,test.Input.Facts.ToJsonString());Assert.Equal(inventoryBefore,test.Input.Inventory.Select(x=>x.ToJsonString()).ToArray());
    }
}
public sealed class ScriptedResolver(ResolverCase expected,Func<ResolverReply,ResolverReply>? corrupt=null,bool mutateInput=false):IResolverCaseProvider
{
    public bool Called { get; private set; }
    public ResolverReply Resolve(ResolverInput input)
    {
        Assert.Same(expected.Input,input);Called=true;
        if(mutateInput) input.Facts["identity_verified"]=!input.Facts["identity_verified"]!.GetValue<bool>();
        var result=ResolverCases.Correct(expected);return corrupt is null ? result : corrupt(result);
    }
}
[Trait("Gate","Mock")]
public sealed class ResolverMockGateTests
{
    [Theory,MemberData(nameof(ResolverCases.All),MemberType=typeof(ResolverCases))]
    public void CorrectMocksPass(ResolverCase test)
    { var fake=new ScriptedResolver(test);ResolverCases.AssertCase(test,fake);Assert.True(fake.Called); }
    [Theory,MemberData(nameof(ResolverCases.All),MemberType=typeof(ResolverCases))]
    public void EveryHeaderAndMembershipMutationRejected(ResolverCase test)
    {
        Func<ResolverReply,ResolverReply>[] changes=[x=>x with{RegistryId="wrong"},x=>x with{CatalogGeneration=x.CatalogGeneration+1},x=>x with{OverlayGeneration=x.OverlayGeneration+1},x=>x with{Epoch=x.Epoch+1},x=>x with{Generation=x.Generation+1},x=>x with{InventoryCount=x.InventoryCount+1},x=>x with{Rows=[]}];
        foreach(var change in changes) Assert.NotNull(Record.Exception(()=>ResolverCases.AssertCase(test,new ScriptedResolver(test,change))));
    }
    [Theory,MemberData(nameof(ResolverCases.All),MemberType=typeof(ResolverCases))]
    public void EveryRowFieldMutationRejected(ResolverCase test)
    {
        Func<ResolverRow,ResolverRow>[] changes=[x=>x with{Id="wrong"},x=>x with{Kind="wrong"},x=>x with{State="wrong"},x=>x with{Reasons=["WRONG_REASON"]},x=>x with{ContractHash="wrong"},x=>x with{SchemaHash="wrong"},x=>x with{GraphDigest="wrong"},x=>x with{Dependencies=[new("wrong","wrong")]},x=>x with{CatalogState="wrong"},x=>x with{CatalogRevision=x.CatalogRevision+1},x=>x with{DocumentHash="wrong"}];
        foreach(var change in changes)
            Assert.NotNull(Record.Exception(()=>ResolverCases.AssertCase(test,new ScriptedResolver(test,x=>x with{Rows=x.Rows.Select((row,index)=>index==0 ? change(row) : row).ToArray()}))));
    }
    [Theory,MemberData(nameof(ResolverCases.All),MemberType=typeof(ResolverCases))]
    public void InputMutationRejected(ResolverCase test)
    {
        var isolated=test with{Input=test.Input with{Facts=(JsonObject)test.Input.Facts.DeepClone(),Inventory=test.Input.Inventory.Select(x=>(JsonObject)x.DeepClone()).ToArray()}};
        Assert.NotNull(Record.Exception(()=>ResolverCases.AssertCase(isolated,new ScriptedResolver(isolated,mutateInput:true))));
    }
}
