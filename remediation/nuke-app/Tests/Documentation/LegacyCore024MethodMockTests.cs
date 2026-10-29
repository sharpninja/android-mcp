using System.Text.Json.Nodes;
using System.Xml.Linq;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed record Core024Evidence(string Design,string Code,JsonObject Api,JsonObject Ui,JsonObject Pins,
    byte[] OriginalMetadata,byte[] PortableMetadata,JsonObject ScreenSchema,string PendingSvg);

// Eight exact source-method predicates. Minimal independent observations are
// supplied through this seam; current file reads are a later native binding.
public static class LegacyCore024Methods
{
    public static readonly string[] Names=["test_actual_controls_are_in_generated_design_table","test_delete_reason_and_metadata_etags",
        "test_pinned_portable_metadata_contract","test_inbound_arc_counts_dispatch_and_lifecycle_uses_port","test_screen_result_schema_has_one_field_spelling",
        "test_single_writer_credential_and_modifier_semantics","test_generic_example_uses_canonical_argument_names","test_pending_owner_state_is_paused_with_explicit_reason"];
    public static Core024Evidence Correct
    {
        get
        {
            var metadata="independent metadata fixture"u8.ToArray();
            var delete=new JsonObject{["requestBody"]=new JsonObject{["required"]=true,["content"]=new JsonObject{["application/json"]=new JsonObject{["schema"]=new JsonObject{["required"]=new JsonArray("reason")}}}}};
            JsonObject Meta()=>new(){["put"]=new JsonObject{["responses"]=new JsonObject{["201"]=new JsonObject{["headers"]=new JsonObject{["ETag"]=new JsonObject{["description"]="meta1 identity"}}}}}};
            var api=new JsonObject{["paths"]=new JsonObject{["/v1/catalog/entries/{entry_id}"]=new JsonObject{["delete"]=delete},
                ["/v1/catalog/callback-descriptors/{callback_id}"]=Meta(),["/v1/catalog/functional-groups/{group_id}"]=Meta()}};
            var design="<!-- GENERATED UI NAVIGATION BEGIN -->SCR01.C01 Start<!-- GENERATED UI NAVIGATION END -->\nNo modifier field exists on ARCv1 events\nplaceholder strings are not valid executable pins\n"+
                "{\n  \"jsonrpc\": \"2.0\",\n  \"id\": 42,\n  \"method\": \"tools/call\",\n  \"params\": {\"name\":\"binding.invoke\",\"arguments\":{\"bindingId\":\"x\",\"schemaHash\":\"x\",\"targetHandle\":null,\"arguments\":{}}}\n}\n\n";
            var code=string.Join('\n',new[]{"ArcRuntime ..> AdmissionGate : beginDispatch/endDispatch","interface LifecycleCommands","BridgeService ..|> LifecycleCommands",
                "ComposeManagementViewModel ..> SessionCommandFacade","KeystoreCredentialVault ..> CatalogRepository : credential verifier writes use the single serialized repository writer"});
            return new(design,code,api,new JsonObject{["screens"]=new JsonArray(new JsonObject{["controls"]=new JsonObject{["SCR01.C01"]="Start"}})},
                new JsonObject{["files"]=new JsonArray(new JsonObject{["file"]="SOURCE-METADATA-CONTRACT.md",["sha256"]=ContentHash.Sha256(metadata)})},metadata,metadata.ToArray(),
                new JsonObject{["required"]=new JsonArray("captureId","capturedAt","coordinateSpaceVersion","displayId")},
                "<svg><g data-health-snapshot='{&quot;service&quot;:&quot;Paused&quot;,&quot;pause_reason&quot;:&quot;OWNER_CHALLENGE&quot;}'/></svg>");
        }
    }
    public static void AssertOriginal(string method,Core024Evidence value)
    {
        switch(Array.IndexOf(Names,method)){
            case 0:
                var table=value.Design.Split("<!-- GENERATED UI NAVIGATION BEGIN -->",StringSplitOptions.None)[1].Split("<!-- GENERATED UI NAVIGATION END -->",StringSplitOptions.None)[0];
                foreach(var screen in value.Ui["screens"]!.AsArray())foreach(var control in screen!["controls"]!.AsObject())
                    Assert.Contains(control.Key+" "+control.Value!.GetValue<string>(),table,StringComparison.Ordinal);
                Assert.DoesNotContain("all 62 slides",value.Design,StringComparison.Ordinal);break;
            case 1:
                var body=value.Api["paths"]!["/v1/catalog/entries/{entry_id}"]!["delete"]!["requestBody"]!;
                Assert.True(body["required"]!.GetValue<bool>());Assert.Contains("reason",body["content"]!["application/json"]!["schema"]!["required"]!.AsArray().Select(item=>item!.GetValue<string>()));
                foreach(var path in new[]{"/v1/catalog/callback-descriptors/{callback_id}","/v1/catalog/functional-groups/{group_id}"})
                    Assert.Contains("meta1",value.Api["paths"]![path]!["put"]!["responses"]!["201"]!["headers"]!["ETag"]!["description"]!.GetValue<string>(),StringComparison.Ordinal);break;
            case 2:
                var pin=value.Pins["files"]!.AsArray().First(item=>item!["file"]!.GetValue<string>()=="SOURCE-METADATA-CONTRACT.md")!;
                Assert.Equal(pin["sha256"]!.GetValue<string>(),ContentHash.Sha256(value.OriginalMetadata));Assert.Equal(value.OriginalMetadata,value.PortableMetadata);break;
            case 3:
                foreach(var token in new[]{"ArcRuntime ..> AdmissionGate : beginDispatch/endDispatch","interface LifecycleCommands","BridgeService ..|> LifecycleCommands","ComposeManagementViewModel ..> SessionCommandFacade"})
                    Assert.Contains(token,value.Code,StringComparison.Ordinal);break;
            case 4:
                var required=value.ScreenSchema["required"]!.AsArray().Select(item=>item!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
                Assert.True(new[]{"captureId","capturedAt","coordinateSpaceVersion","displayId"}.All(required.Contains));
                Assert.DoesNotContain("captured_at, coordinate_version, capture_id",value.Design,StringComparison.Ordinal);break;
            case 5:
                Assert.Contains("KeystoreCredentialVault ..> CatalogRepository : credential verifier writes use the single serialized repository writer",value.Code,StringComparison.Ordinal);
                Assert.Contains("No modifier field exists on ARCv1 events",value.Design,StringComparison.Ordinal);break;
            case 6:
                var start=value.Design.IndexOf("{\n  \"jsonrpc\": \"2.0\",\n  \"id\": 42,\n  \"method\": \"tools/call\"",StringComparison.Ordinal);Assert.True(start>=0);
                var end=value.Design.IndexOf("\n\n",start,StringComparison.Ordinal);Assert.True(end>=0);
                var request=JsonNode.Parse(value.Design[start..end])!;Assert.Equal("binding.invoke",request["params"]!["name"]!.GetValue<string>());
                Assert.Equal(new[]{"arguments","bindingId","schemaHash","targetHandle"},request["params"]!["arguments"]!.AsObject().Select(item=>item.Key).Order(StringComparer.Ordinal));
                Assert.Contains("placeholder strings are not valid executable pins",value.Design,StringComparison.Ordinal);break;
            case 7:
                var health=JsonNode.Parse(XElement.Parse(value.PendingSvg).DescendantsAndSelf().First(element=>element.Attribute("data-health-snapshot") is not null).Attribute("data-health-snapshot")!.Value)!;
                Assert.Equal("Paused",health["service"]!.GetValue<string>());Assert.Equal("OWNER_CHALLENGE",health["pause_reason"]!.GetValue<string>());break;
            default:throw new InvalidOperationException("Unknown original core024 method");
        }
    }
    public static IEnumerable<object[]> Mutations
    {
        get
        {
            yield return new object[]{0,"control"};yield return new object[]{0,"stale-count"};
            foreach(var key in new[]{"required","reason","callback","group"})yield return new object[]{1,key};
            foreach(var key in new[]{"pin","original","portable"})yield return new object[]{2,key};
            foreach(var token in new[]{"ArcRuntime ..> AdmissionGate : beginDispatch/endDispatch","interface LifecycleCommands","BridgeService ..|> LifecycleCommands","ComposeManagementViewModel ..> SessionCommandFacade"})yield return new object[]{3,token};
            foreach(var propertyName in new[]{"captureId","capturedAt","coordinateSpaceVersion","displayId","old-spelling"})yield return new object[]{4,propertyName};
            yield return new object[]{5,"writer"};yield return new object[]{5,"modifier"};
            foreach(var key in new[]{"name","arguments","warning"})yield return new object[]{6,key};
            yield return new object[]{7,"service"};yield return new object[]{7,"reason"};
        }
    }
    public static Core024Evidence Wrong(int method,string key)
    {
        var value=Correct;
        switch(method){
            case 0:return value with{Design=key=="control"?value.Design.Replace("SCR01.C01 Start","SCR01.C01 WRONG",StringComparison.Ordinal):value.Design+"all 62 slides"};
            case 1:
                var paths=value.Api["paths"]!;
                if(key=="required")paths["/v1/catalog/entries/{entry_id}"]!["delete"]!["requestBody"]!["required"]=false;
                else if(key=="reason")paths["/v1/catalog/entries/{entry_id}"]!["delete"]!["requestBody"]!["content"]!["application/json"]!["schema"]!["required"]=new JsonArray("other");
                else paths[key=="callback"?"/v1/catalog/callback-descriptors/{callback_id}":"/v1/catalog/functional-groups/{group_id}"]!["put"]!["responses"]!["201"]!["headers"]!["ETag"]!["description"]="wrong";
                return value;
            case 2:
                if(key=="pin"){value.Pins["files"]![0]!["sha256"]=new string('0',64);return value;}
                return key=="original"?value with{OriginalMetadata="wrong"u8.ToArray()}:value with{PortableMetadata="wrong"u8.ToArray()};
            case 3:return value with{Code=value.Code.Replace(key,"REMOVED",StringComparison.Ordinal)};
            case 4:
                if(key=="old-spelling")return value with{Design=value.Design+"captured_at, coordinate_version, capture_id"};
                value.ScreenSchema["required"]=new JsonArray(value.ScreenSchema["required"]!.AsArray().Where(item=>item!.GetValue<string>()!=key).Select(item=>item!.DeepClone()).ToArray());return value;
            case 5:return key=="writer"?value with{Code=value.Code.Replace("KeystoreCredentialVault ..> CatalogRepository : credential verifier writes use the single serialized repository writer","wrong",StringComparison.Ordinal)}:
                value with{Design=value.Design.Replace("No modifier field exists on ARCv1 events","wrong",StringComparison.Ordinal)};
            case 6:
                if(key=="name")return value with{Design=value.Design.Replace("binding.invoke","wrong",StringComparison.Ordinal)};
                if(key=="arguments")return value with{Design=value.Design.Replace("targetHandle","target_handle",StringComparison.Ordinal)};
                return value with{Design=value.Design.Replace("placeholder strings are not valid executable pins","wrong",StringComparison.Ordinal)};
            case 7:return value with{PendingSvg=key=="service"?value.PendingSvg.Replace("Paused","Running",StringComparison.Ordinal):value.PendingSvg.Replace("OWNER_CHALLENGE","OTHER",StringComparison.Ordinal)};
            default:throw new InvalidOperationException("Unknown original mutation");
        }
    }
}

[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-005")]
public sealed class LegacyCore024MethodMockTests
{
    public static IEnumerable<object[]> Methods=>LegacyCore024Methods.Names.Select(method=>new object[]{method});
    [Theory,MemberData(nameof(Methods))]
    public void CorrectIndependentObservationsPassExactOriginalPredicates(string method)=>LegacyCore024Methods.AssertOriginal(method,LegacyCore024Methods.Correct);
    [Theory,MemberData(nameof(LegacyCore024Methods.Mutations),MemberType=typeof(LegacyCore024Methods))]
    public void EveryRequiredPredicateRejectsItsOwnWrongResponse(int method,string key)=>
        Assert.NotNull(Record.Exception(()=>LegacyCore024Methods.AssertOriginal(LegacyCore024Methods.Names[method],LegacyCore024Methods.Wrong(method,key))));
}
