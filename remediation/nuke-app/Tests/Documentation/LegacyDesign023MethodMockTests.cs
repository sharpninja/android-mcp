using System.Text;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed record Design023Evidence(string Design,JsonObject Api,JsonObject Facts,string Contract,string Build,
    JsonObject Identities,string Code,string Dsl);

// One literal assertion body for each original method. The original standalone
// Gradle declaration input is retained separately from the migrated projection.
public static class LegacyDesign023Methods
{
    public static readonly string[] Names=["test_version_rejection_matches_pinned_arc_source","test_contract_version_has_one_current_value",
        "test_admin_scheme_is_issued_bearer_and_owner_proof_is_server_owned","test_import_stage_routes_define_sealed_hash_size_expiry",
        "test_stage_lifetime_is_capacity_aware_and_configurable","test_standalone_docs_tasks_verify_pins",
        "test_all_native_arc_identities_hash_exact_bytes_and_use_pinned_payloads","test_dispatch_permit_and_storage_ports_are_explicit_in_sources",
        "test_watch_contract_is_finite_and_blob_quota_accounted"];
    public static readonly string[] Operations=["screen.state","input.tap","settings.brightness.get","settings.brightness.set","sensors.list","sensors.read","contacts.list","document.read","device.battery.read"];
    public static readonly string[] NativeRequests=["ScreenStateRequest@1","TapRequest@1","EmptyRequest@1","BrightnessSetRequest@1","SensorListRequest@1","SensorReadRequest@1","ContactsListRequest@1","DocumentReadRequest@1","EmptyRequest@1"];
    public static readonly string[] NativeResults=["ScreenStateResult@1","TapResult@1","BrightnessState@1","BrightnessState@1","SensorListResult@1","SensorSamples@1","ContactsPage@1","DocumentChunk@1","BatteryState@1"];
    public static readonly string[] NativeHashes=["306bd0e60a7cd05beee5657eb64502c38810f6b3235b2299b3faaf8a2ee4e8f6","650fc4876a7a63061c520a619e6607d7d82ab6473da76484673b7ae83a61dad5","04e92ad4dd9eaa87ab20ed857dd4264828f74c51cecb3629bc5f3d85f3fe3035","c33c358f85b9d4801e2464ab9a34297cf52fc086e80fc9a5d214b165be35e90d","11a4c3c5707e9a2fc90bfef2a1ad29b1e2cf6ae866a5d4e920f882962f527c31","f21013fd37bc29dbd7c993df64c0f78d28eb6704d4a715be3ae48a4ff6ed599c","09631a0831ee8cec3b99f2277d9fb612397cc5b4f0726c9a2a70de50c9cc4bfc","26ad57ccde14d41c4b8aa45c4ebcbec714b8ff65c81753e34c7544e973104a69","5994c5e964d93780e07056406c8ec61488181cad243ab8123c3c9797464fb722"];
    public static readonly string[] ArcEnums=["GET_CAPABILITIES","GET_SNAPSHOT","WATCH_TREE","INVOKE_CLICK","INVOKE_FOCUS","SET_PROPERTY","WATCH_LOGS","CANCEL","WATCH_FRAMES","SEND_INPUT"];
    public static readonly string[] ArcRequests=["GetCapabilitiesRequest","GetSnapshotRequest","WatchTreeRequest","InvokeClickRequest","InvokeFocusRequest","SetPropertyRequest","WatchLogsRequest","opaque-bytes","WatchFramesRequest","SendInputRequest"];
    public static readonly string[] ArcResults=["GetCapabilitiesResponse","TreeSnapshot","TreeUpdate","CommandResult","CommandResult","CommandResult","LogEntry","BRIDGE_STATUS_UNSUPPORTED","FrameUpdate","CommandResult"];
    public static readonly string[] ArcHashes=["22c7c084aaf12a4af3a88d8813c3d1b0925805a5b689ccdd2c5c73f0cd923bc5","4f0ed2efeb8fca545e8daa2c6c69762a308b4f423a7909ba03cf92c4b2775eb4","b28a1a348142608fd8c16ccbecaa35dd71ccd6169878806d0cd72345c0c07c7e","ccefa04b6574ab85839d22dfc6e6817f1ceeaeb26b4b7cb33d30dd46e9fd7746","f265de257bcfcf2fc7eda416d78b7063307d717cd0301d678dda753388f3d280","19620734704e670e55b4217d5e3391e231b1705efdcc40a1089ca08651a70850","8ace547f02087e186ff9b097a648c7434a76e86e234c603e8426d899d27bcade","9242ceeae4f164a62f0291d34f0639dd801981ea0ec48690a73045a6ffddad6c","71e3f215ea2ee80599637887db894811db4c399150467170a6c2734b4e63dfbf","85fe7b92538e2d9e494dbc69288fa354b4cc37d6096cd206438b2b860da7f3f5"];
    public static readonly string[] WatchTokens=["finite subscription result","per-subscription16MiB","flush and seal blob, commit reference","ARC_V1_CANCEL_UNSUPPORTED"];
    public static Design023Evidence Correct
    {
        get
        {
            const string proto="d22f19ce8d170f92c4e2f27bc2b6e4705337aa3d5fa6aed86d8e7eba2f95e79e";
            JsonObject Op(bool write)=>new(){["security"]=new JsonArray(new JsonObject{["catalogAuth"]=new JsonArray()}),["responses"]=new JsonObject{["403"]=new JsonObject()},["x-owner-authorization"]=write?"prearmed-local-exact-command":"ignored"};
            var post=Op(true);post["requestBody"]=new JsonObject{["content"]=new JsonObject{["application/octet-stream"]=new JsonObject()}};
            var properties=new JsonObject();foreach(var member in new[]{"stage_handle","sha256","size_bytes","expires_at","state"})properties[member]=new JsonObject();
            var api=new JsonObject{["info"]=new JsonObject{["version"]="1.6.0"},["components"]=new JsonObject{
                ["securitySchemes"]=new JsonObject{["catalogAuth"]=new JsonObject{["type"]="http",["scheme"]="bearer"}},
                ["schemas"]=new JsonObject{["ImportStage"]=new JsonObject{["properties"]=properties}}},
                ["paths"]=new JsonObject{["/v1/catalog/import-stages"]=new JsonObject{["get"]=Op(false),["post"]=post},
                    ["/write"]=new JsonObject{["put"]=Op(true),["delete"]=Op(true),["patch"]=Op(true)}}};
            var records=new JsonArray();for(var i=0;i<Operations.Length;i++)records.Add(new JsonObject{
                ["operation"]=Operations[i],["canonical_signature"]=$"bridge-native-v1|operation={Operations[i]}|request={NativeRequests[i]}|result={NativeResults[i]}",["binding_id"]=NativeHashes[i]});
            var arcs=new JsonArray();for(var i=0;i<ArcEnums.Length;i++){
                var request=i==7?ArcRequests[i]:"avalonia.remotecontrol.v1."+ArcRequests[i];var result=i==7?ArcResults[i]:"avalonia.remotecontrol.v1."+ArcResults[i];
                arcs.Add(new JsonObject{["method"]=i+1,["request_type"]=request,["result_type"]=result,["binding_id"]=ArcHashes[i],
                    ["canonical_signature"]=$"bridge-arc-v1|package=avalonia.remotecontrol.v1|wire=1.0|method={i+1}:BRIDGE_METHOD_{ArcEnums[i]}|request={request}|result={result}"});}
            var messages=new JsonArray();foreach(var value in ArcRequests.Concat(ArcResults).Where(value=>value!="opaque-bytes"&&value!="BRIDGE_STATUS_UNSUPPORTED").Distinct(StringComparer.Ordinal))messages.Add(value);
            return new("Version mismatch, including a zero-length default envelope, returns UNSUPPORTED5\nimport_stage_ttl_seconds\nSTAGE_LIFETIME_INSUFFICIENT\n"+string.Join('\n',WatchTokens),api,
                new JsonObject{["version_mismatch"]=new JsonObject{["status_number"]=5,["status_name"]="UNSUPPORTED"},["proto_sha256"]=proto,["message_names"]=messages},
                "# Catalog administration contract 1.6.0\n",
                "val generateUiAssets by tasks.registering { verifyToolchain }\nval renderStoryboards by tasks.registering { verifyToolchain }\nval validateArchitecture by tasks.registering { outputs.file(\"receipt\") }\n",
                new JsonObject{["arc_proto_sha256"]=proto,["records"]=records,["arc_records"]=arcs},
                "InvocationPlanner ..> AdmissionGate : beginDispatch\nSqliteCatalogRepository ..> OwnerCapabilityVerifier\n",
                "beginDispatch: atomically recheck epochs/authority\nInvalidate affected MCP sessions/delivery\n");
        }
    }
    public static void AssertOriginal(string method,Design023Evidence e)
    {
        switch(Array.IndexOf(Names,method)){
            case 0:Assert.Equal(5,e.Facts["version_mismatch"]!["status_number"]!.GetValue<int>());Assert.Equal("UNSUPPORTED",e.Facts["version_mismatch"]!["status_name"]!.GetValue<string>());
                Assert.DoesNotContain("For invalid ARC envelope version or empty request ID, a bounded ERROR response",e.Design,StringComparison.Ordinal);Assert.Contains("Version mismatch, including a zero-length default envelope, returns UNSUPPORTED5",e.Design,StringComparison.Ordinal);break;
            case 1:Assert.Equal("1.6.0",e.Api["info"]!["version"]!.GetValue<string>());Assert.StartsWith("# Catalog administration contract 1.6.0\n",e.Contract,StringComparison.Ordinal);Assert.DoesNotContain("OpenAPI specification version is1.4.0",e.Contract,StringComparison.Ordinal);break;
            case 2:
                var scheme=e.Api["components"]!["securitySchemes"]!["catalogAuth"]!;Assert.Equal("http",scheme["type"]!.GetValue<string>());Assert.Equal("bearer",scheme["scheme"]!.GetValue<string>());
                foreach(var route in e.Api["paths"]!.AsObject())foreach(var operation in route.Value!.AsObject())if(new[]{"get","post","put","delete","patch"}.Contains(operation.Key,StringComparer.Ordinal)){
                    foreach(var security in operation.Value!["security"]?.AsArray()??new JsonArray())foreach(var scopes in security!.AsObject())Assert.True(scopes.Value is JsonArray {Count:0});
                    if(operation.Key!="get"){Assert.Equal("prearmed-local-exact-command",operation.Value!["x-owner-authorization"]!.GetValue<string>());Assert.True(operation.Value!["responses"]!.AsObject().ContainsKey("403"));}}
                break;
            case 3:
                var stage=e.Api["paths"]!["/v1/catalog/import-stages"]!.AsObject();Assert.True(new[]{"get","post"}.ToHashSet(StringComparer.Ordinal).SetEquals(stage.Select(value=>value.Key)));
                foreach(var field in new[]{"stage_handle","sha256","size_bytes","expires_at","state"})Assert.True(e.Api["components"]!["schemas"]!["ImportStage"]!["properties"]!.AsObject().ContainsKey(field));
                // Python dict.popitem selects its last insertion; it does not require a singleton content map.
                var content=stage["post"]!["requestBody"]!["content"]!.AsObject();var last=content.Last();content.Remove(last.Key);
                Assert.Equal("application/octet-stream",last.Key);Assert.True(stage["post"]!.AsObject().ContainsKey("x-owner-authorization"));break;
            case 4:Assert.DoesNotContain("min(created+15minutes, management-window end)",e.Design,StringComparison.Ordinal);Assert.DoesNotContain("15minutes after copy begins",e.Design,StringComparison.Ordinal);Assert.Contains("import_stage_ttl_seconds",e.Design,StringComparison.Ordinal);Assert.Contains("STAGE_LIFETIME_INSUFFICIENT",e.Design,StringComparison.Ordinal);break;
            case 5:
                string Body(string name)=>e.Build.Split("val "+name+" by ",2,StringSplitOptions.None)[1].Split("\nval ",2,StringSplitOptions.None)[0];
                foreach(var name in new[]{"generateUiAssets","renderStoryboards"})Assert.Contains("verifyToolchain",Body(name),StringComparison.Ordinal);Assert.Contains("outputs.file",Body("validateArchitecture"),StringComparison.Ordinal);break;
            case 6:
                Assert.Equal(e.Facts["proto_sha256"]!.GetValue<string>(),e.Identities["arc_proto_sha256"]!.GetValue<string>());
                Assert.True(Enumerable.Range(1,10).ToHashSet().SetEquals(e.Identities["arc_records"]!.AsArray().Select(value=>value!["method"]!.GetValue<int>())));
                Assert.True(Operations.ToHashSet(StringComparer.Ordinal).SetEquals(e.Identities["records"]!.AsArray().Select(value=>value!["operation"]!.GetValue<string>())));
                var ids=new HashSet<string>(StringComparer.Ordinal);var messages=e.Facts["message_names"]!.AsArray().Select(value=>value!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
                foreach(var record in e.Identities["records"]!.AsArray().Concat(e.Identities["arc_records"]!.AsArray())){
                    var id=record!["binding_id"]!.GetValue<string>();Assert.Equal(ContentHash.Sha256(Encoding.UTF8.GetBytes(record["canonical_signature"]!.GetValue<string>())),id);Assert.True(ids.Add(id));
                    if((record["method"]?.GetValue<int>()??8)!=8)foreach(var field in new[]{"request_type","result_type"})Assert.Contains(record[field]!.GetValue<string>().Split('.').Last(),messages);}
                break;
            case 7:foreach(var token in new[]{"InvocationPlanner ..> AdmissionGate : beginDispatch","SqliteCatalogRepository ..> OwnerCapabilityVerifier"})Assert.Contains(token,e.Code,StringComparison.Ordinal);Assert.DoesNotContain("SqliteCatalogRepository ..> OwnerAuthenticationService",e.Code,StringComparison.Ordinal);foreach(var token in new[]{"beginDispatch: atomically recheck epochs/authority","Invalidate affected MCP sessions/delivery"})Assert.Contains(token,e.Dsl,StringComparison.Ordinal);break;
            case 8:foreach(var token in WatchTokens)Assert.Contains(token,e.Design,StringComparison.Ordinal);break;
            default:throw new ArgumentOutOfRangeException(nameof(method));
        }
    }
    public static IEnumerable<object[]> Mutations
    {
        get {
            foreach(var value in new[]{"status_number","status_name","old-version-text","new-version-text"})yield return new object[]{0,value,0};
            foreach(var value in new[]{"api-version","contract-prefix","contract-old"})yield return new object[]{1,value,0};
            foreach(var value in new[]{"scheme-type","scheme-name"})yield return new object[]{2,value,0};
            for(var i=0;i<5;i++)yield return new object[]{2,"scope",i};
            for(var i=0;i<4;i++)foreach(var value in new[]{"owner","403"})yield return new object[]{2,value,i};
            foreach(var value in new[]{"route-set","stage_handle","sha256","size_bytes","expires_at","state","media","stage-owner"})yield return new object[]{3,value,0};
            foreach(var value in new[]{"min(created+15minutes, management-window end)","15minutes after copy begins","import_stage_ttl_seconds","STAGE_LIFETIME_INSUFFICIENT"})yield return new object[]{4,value,0};
            for(var i=0;i<3;i++)yield return new object[]{5,"task-body",i};
            foreach(var value in new[]{"proto","method-set","operation-set"})yield return new object[]{6,value,0};
            for(var i=0;i<19;i++)foreach(var value in new[]{"signature","hash"})yield return new object[]{6,value,i};
            for(var i=0;i<10;i++)if(i!=7)foreach(var value in new[]{"request_type","result_type"})yield return new object[]{6,value,i};
            yield return new object[]{6,"duplicate",0};
            foreach(var value in new[]{"InvocationPlanner ..> AdmissionGate : beginDispatch","SqliteCatalogRepository ..> OwnerCapabilityVerifier","SqliteCatalogRepository ..> OwnerAuthenticationService","beginDispatch: atomically recheck epochs/authority","Invalidate affected MCP sessions/delivery"})yield return new object[]{7,value,0};
            foreach(var token in WatchTokens)yield return new object[]{8,token,0};
        }
    }
    public static Design023Evidence Wrong(int method,string mutation,int index)
    {
        var e=Correct;var stage=e.Api["paths"]!["/v1/catalog/import-stages"]!;
        var operations=new[]{stage["get"]!,stage["post"]!,e.Api["paths"]!["/write"]!["put"]!,e.Api["paths"]!["/write"]!["delete"]!,e.Api["paths"]!["/write"]!["patch"]!};
        if(method==0){if(mutation.StartsWith("status_",StringComparison.Ordinal)){e.Facts["version_mismatch"]![mutation]=mutation=="status_number"?JsonValue.Create(6):JsonValue.Create("ERROR");return e;}return e with{Design=mutation=="old-version-text"?e.Design+"For invalid ARC envelope version or empty request ID, a bounded ERROR response":e.Design.Replace("Version mismatch, including a zero-length default envelope, returns UNSUPPORTED5","removed",StringComparison.Ordinal)};}
        if(method==1){if(mutation=="api-version"){e.Api["info"]!["version"]="1.4.0";return e;}return e with{Contract=mutation=="contract-prefix"?" "+e.Contract:e.Contract+"OpenAPI specification version is1.4.0"};}
        if(method==2){if(mutation.StartsWith("scheme-",StringComparison.Ordinal))e.Api["components"]!["securitySchemes"]!["catalogAuth"]![mutation=="scheme-type"?"type":"scheme"]="wrong";else if(mutation=="scope")operations[index]["security"]![0]!["catalogAuth"]!.AsArray().Add("scope");else if(mutation=="owner")operations[index+1]["x-owner-authorization"]="wrong";else operations[index+1]["responses"]!.AsObject().Remove("403");return e;}
        if(method==3){if(mutation=="route-set")stage.AsObject().Remove("get");else if(mutation=="media")stage["post"]!["requestBody"]!["content"]!.AsObject().Add("wrong",new JsonObject());else if(mutation=="stage-owner")stage["post"]!.AsObject().Remove("x-owner-authorization");else e.Api["components"]!["schemas"]!["ImportStage"]!["properties"]!.AsObject().Remove(mutation);return e;}
        if(method==4)return e with{Design=mutation.StartsWith("import_",StringComparison.Ordinal)||mutation=="STAGE_LIFETIME_INSUFFICIENT"?e.Design.Replace(mutation,"removed",StringComparison.Ordinal):e.Design+mutation};
        if(method==5){var tokens=new[]{"val generateUiAssets by tasks.registering { verifyToolchain }","val renderStoryboards by tasks.registering { verifyToolchain }","outputs.file"};return e with{Build=e.Build.Replace(tokens[index],index<2?tokens[index].Replace("verifyToolchain","wrong",StringComparison.Ordinal):"wrong",StringComparison.Ordinal)};}
        if(method==6){var records=e.Identities["records"]!.AsArray();var arcs=e.Identities["arc_records"]!.AsArray();
            if(mutation=="proto")e.Identities["arc_proto_sha256"]="wrong";else if(mutation=="method-set")arcs[0]!["method"]=11;else if(mutation=="operation-set")records[0]!["operation"]="wrong";
            else if(mutation=="duplicate")records.Add(records[0]!.DeepClone());else if(mutation is "request_type" or "result_type")arcs[index]![mutation]="unknown.message";
            else {var record=index<9?records[index]!:arcs[index-9]!;record[mutation=="signature"?"canonical_signature":"binding_id"]="wrong";}return e;}
        if(method==7)return mutation=="SqliteCatalogRepository ..> OwnerAuthenticationService"?e with{Code=e.Code+mutation}:mutation.StartsWith("beginDispatch",StringComparison.Ordinal)||mutation.StartsWith("Invalidate",StringComparison.Ordinal)?e with{Dsl=e.Dsl.Replace(mutation,"removed",StringComparison.Ordinal)}:e with{Code=e.Code.Replace(mutation,"removed",StringComparison.Ordinal)};
        return e with{Design=e.Design.Replace(mutation,"removed",StringComparison.Ordinal)};
    }
}
[Trait("Gate","Mock"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class LegacyDesign023MethodMockTests
{
    public static IEnumerable<object[]> Methods=>LegacyDesign023Methods.Names.Select(value=>new object[]{value});
    [Theory,MemberData(nameof(Methods))]
    public void CorrectIndependentMockExercisesOriginalMethod(string method)=>LegacyDesign023Methods.AssertOriginal(method,LegacyDesign023Methods.Correct);
    [Theory,MemberData(nameof(LegacyDesign023Methods.Mutations),MemberType=typeof(LegacyDesign023Methods))]
    public void EachIndividualOriginalPredicateRejectsWrongMock(int method,string mutation,int index)=>Assert.NotNull(Record.Exception(()=>LegacyDesign023Methods.AssertOriginal(LegacyDesign023Methods.Names[method],LegacyDesign023Methods.Wrong(method,mutation,index))));
    [Fact]
    public void OriginalImportContentPopitemAndOptionalSecurityAreNotStrengthened()
    {
        var e=LegacyDesign023Methods.Correct;var post=e.Api["paths"]!["/v1/catalog/import-stages"]!["post"]!;post["requestBody"]!["content"]=new JsonObject{["first-unchecked"]=new JsonObject(),["application/octet-stream"]=new JsonObject()};post.AsObject().Remove("security");
        LegacyDesign023Methods.AssertOriginal(LegacyDesign023Methods.Names[2],e);LegacyDesign023Methods.AssertOriginal(LegacyDesign023Methods.Names[3],e);
    }
    [Fact]
    public void OriginalCancelPayloadAndNativeMissingMethodAreNotAssignedInventedMessageRequirements()
    {var e=LegacyDesign023Methods.Correct;e.Identities["arc_records"]![7]!["request_type"]="unchecked";e.Identities["arc_records"]![7]!["result_type"]="unchecked";LegacyDesign023Methods.AssertOriginal(LegacyDesign023Methods.Names[6],e);}
    [Fact]
    public void OriginalPopitemRemovesItsLastEntryBeforeTheMediaAssertion()
    {
        var e=LegacyDesign023Methods.Correct;var content=e.Api["paths"]!["/v1/catalog/import-stages"]!["post"]!["requestBody"]!["content"]!.AsObject();
        LegacyDesign023Methods.AssertOriginal(LegacyDesign023Methods.Names[3],e);Assert.Empty(content);
        content.Add("wrong",new JsonObject());Assert.NotNull(Record.Exception(()=>LegacyDesign023Methods.AssertOriginal(LegacyDesign023Methods.Names[3],e)));Assert.Empty(content);
    }
}
