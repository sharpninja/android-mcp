using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using static AndroidMcp.BuildTool.Modules.Review.ReviewNormalization;
namespace AndroidMcp.BuildTool.Modules.Review;

public enum ReviewRequirementRecordKind{Functional,Technical,Testing,Mapping}
public sealed record ReviewRequirementRecordPin(ReviewRequirementRecordKind Kind,string Id,StoredArtifact Record);
public sealed record ReviewRequirementNormalizationRequest(string Head,string Workspace,string Plugin,string PluginVersion,
    IReadOnlyList<string> FunctionalIds,IReadOnlyList<string> TechnicalIds,IReadOnlyList<string> TestingIds,
    IReadOnlyList<ReviewRequirementRecordPin> Records,StoredArtifact? CriterionCoverage);

/// <summary>Documented direct FrEntry/TrEntry/TestEntry/FrTrMapping web-JSON records only.
/// This codec does not decode unknown transport envelopes or mint official-plugin authority.</summary>
public sealed class ReviewRequirementRecordNormalizer(IPinnedArtifactReader pins,ICanonicalJson canonical,ITrxResultAdmission trx)
{
    public async Task<ReviewNormalizationProjection> NormalizeAsync(ReviewRequirementNormalizationRequest request,CancellationToken token=default)
    {
        const string incomplete="REQUIREMENTS_INCOMPLETE";const string coverage="CRITERION_COVERAGE_UNAVAILABLE";
        var expected=request.FunctionalIds.Concat(request.TechnicalIds).Concat(request.TestingIds).ToArray();
        Need(expected.Length>0&&expected.Distinct(StringComparer.Ordinal).Count()==expected.Length,incomplete);
        Need(request.Records.Count==expected.Length+request.FunctionalIds.Count,incomplete);
        var raw=new Dictionary<string,JsonObject>(StringComparer.Ordinal);var mappings=new Dictionary<string,JsonObject>(StringComparer.Ordinal);
        var captures=new Dictionary<string,StoredArtifact>(StringComparer.Ordinal);
        var records=new Dictionary<string,StoredArtifact>(StringComparer.Ordinal);
        foreach(var item in request.Records)
        {
            token.ThrowIfCancellationRequested();
            var body=await ReadAsync(pins,canonical,item.Record,token);
            var idKey=item.Kind==ReviewRequirementRecordKind.Mapping?"frId":"id";
            Need(body.ContainsKey(idKey),"OFFICIAL_RESPONSE_SCHEMA_UNAVAILABLE");
            Need(Text(body,idKey)==item.Id,incomplete);
            var scope=item.Kind switch{ReviewRequirementRecordKind.Functional=>request.FunctionalIds,ReviewRequirementRecordKind.Technical=>request.TechnicalIds,
                ReviewRequirementRecordKind.Testing=>request.TestingIds,_=>request.FunctionalIds};
            Need(scope.Contains(item.Id,StringComparer.Ordinal),incomplete);
            if(item.Kind==ReviewRequirementRecordKind.Mapping)
            {
                Need(mappings.TryAdd(item.Id,body),incomplete);
                var trIds=Array(body,"trIds","REQUIREMENT_MAPPING_UNAVAILABLE").Select(x=>x?.GetValue<string>()??"").ToArray();
                var testIds=Array(body,"testIds","REQUIREMENT_MAPPING_UNAVAILABLE").Select(x=>x?.GetValue<string>()??"").ToArray();
                Need(trIds.Length>0&&testIds.Length>0&&trIds.All(x=>request.TechnicalIds.Contains(x,StringComparer.Ordinal))&&
                    testIds.All(x=>request.TestingIds.Contains(x,StringComparer.Ordinal)),"REQUIREMENT_MAPPING_UNAVAILABLE");
            }
            else
            {
                Need(raw.TryAdd(item.Id,body),incomplete);records.Add(item.Id,item.Record);
                Need(!string.IsNullOrWhiteSpace(Text(body,item.Kind==ReviewRequirementRecordKind.Testing?"condition":"body")),"OFFICIAL_RESPONSE_SCHEMA_UNAVAILABLE");
                var criteria=Array(body,"acceptanceCriteria",incomplete);
                Need(criteria.Count>0&&criteria.All(x=>Text(x,"id")!=""&&Text(x,"text")!=""),incomplete);
                Need(criteria.Select(x=>Text(x,"id")).Distinct(StringComparer.Ordinal).Count()==criteria.Count,incomplete);
            }
            captures[item.Record.RelativePath]=item.Record;
        }
        Need(raw.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(expected)&&mappings.Count==request.FunctionalIds.Count,incomplete);
        Need(request.CriterionCoverage is not null,coverage);
        var proof=await ReadAsync(pins,canonical,request.CriterionCoverage!,token);captures[request.CriterionCoverage!.RelativePath]=request.CriterionCoverage;
        Need(Text(proof,"head")==request.Head,"HEAD_MISMATCH");
        var rows=Array(proof,"criteria",coverage);Need(rows.Count>0,coverage);
        var grouped=new Dictionary<string,List<(string Ac,string Test)>>(StringComparer.Ordinal);
        foreach(var row in rows)
        {
            var fr=Text(row,"frId");var ac=Text(row,"acId");var test=Text(row,"testId");
            Need(request.FunctionalIds.Contains(fr,StringComparer.Ordinal)&&request.TestingIds.Contains(test,StringComparer.Ordinal),coverage);
            Need(Array(raw[fr],"acceptanceCriteria",incomplete).Any(x=>Text(x,"id")==ac),coverage);
            Need(Array(mappings[fr],"testIds",coverage).Any(x=>x?.GetValue<string>()==test),coverage);
            var condition=Text(raw[test],"condition");
            Need(Text(row,"testCondition")==condition&&condition.Contains(fr,StringComparison.Ordinal)&&condition.Contains(ac,StringComparison.Ordinal),coverage);
            Need(row?["testRecord"] is JsonObject&&row["trx"] is JsonObject,coverage);
            var recordPin=JsonSerializer.Deserialize<StoredArtifact>(row!["testRecord"]!.ToJsonString());
            Need(recordPin==records[test],coverage);
            var trxPin=JsonSerializer.Deserialize<StoredArtifact>(row["trx"]!.ToJsonString())!;
            var classes=Array(row,"classes",coverage).Select(x=>x?.GetValue<string>()??"").ToArray();Need(classes.Length>0,coverage);
            var bytes=await ReadBytesAsync(pins,trxPin,token);var admitted=trx.Evaluate(new(bytes,classes));Need(admitted.Admitted,coverage);
            captures[trxPin.RelativePath]=trxPin;
            if(!grouped.TryGetValue(fr,out var claims))grouped[fr]=claims=[];claims.Add((ac,test));
        }
        foreach(var fr in request.FunctionalIds)
        {
            Need(grouped.TryGetValue(fr,out var claims),coverage);
            var actual=claims!.Select(x=>x.Ac).ToHashSet(StringComparer.Ordinal);
            Need(actual.SetEquals(Array(raw[fr],"acceptanceCriteria",incomplete).Select(x=>Text(x,"id"))),coverage);
            Need(claims.Distinct().Count()==claims.Count,coverage);
            mappings[fr]["criteria"]=new JsonArray(claims.GroupBy(x=>x.Ac,StringComparer.Ordinal).Select(group=>JsonSerializer.SerializeToNode(new{acId=group.Key,testIds=group.Select(x=>x.Test).Order(StringComparer.Ordinal).ToArray()})).ToArray());
        }
        var output=JsonSerializer.SerializeToElement(new{head=request.Head,workspace=request.Workspace,plugin=request.Plugin,pluginVersion=request.PluginVersion,
            functional=request.FunctionalIds.Select(x=>raw[x]),technical=request.TechnicalIds.Select(x=>raw[x]),testing=request.TestingIds.Select(x=>raw[x]),
            mappings=request.FunctionalIds.Select(x=>mappings[x]),rawRecordPins=captures.Values.OrderBy(x=>x.RelativePath,StringComparer.Ordinal)});
        return new(canonical.Serialize(output),captures.Values.OrderBy(x=>x.RelativePath,StringComparer.Ordinal).ToArray());
    }
}
