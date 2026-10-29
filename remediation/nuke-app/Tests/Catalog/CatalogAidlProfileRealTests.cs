using System.Text.Json;
using AndroidMcp.BuildTool.Catalog;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed class NativeCatalogAidlProfile:ICatalogAidlProfileProvider
{
    private sealed class ObservedProfile(ISourcesExtractionDispatcher inner,string expected):ISourcesExtractionDispatcher
    {
        public IReadOnlyList<string> SupportedKinds=>inner.SupportedKinds;
        public int AidlCalls {get;private set;}
        public bool SelectedProfile {get;private set;}=true;
        public ExtractionResult Extract(SourceDocument source,string? kind,string? scope=null)
        {
            var result=inner.Extract(source,kind,scope);
            if(kind=="aidl"){AidlCalls++;SelectedProfile &= result.Metadata?["source_identity"]?["profile_id"]?.GetValue<string>()==expected;}
            return result;
        }
    }
    public Task<CatalogAidlProfileReply> BuildAsync(CatalogAidlProfileCall call)
    {
        var profile=call.Profile=="release-011" ? SourceExtractionProfile.RELEASE011 : SourceExtractionProfile.CurrentCandidate;
        using var runtime=new SourcesRuntimeFactory().Create(profile);var observed=new ObservedProfile(runtime.Dispatcher,profile.Id);
        using var producer=new NativeCatalogBuildProvider{SourceValues=values=>values[2]=("aidl","IFoo.aidl",call.Raw),SourceDispatcher=observed,SourceProfile=profile};
        var built=producer.Build(new("aidl-profile-caller"));
        if(built.Outcome!="ok")return Task.FromResult(new CatalogAidlProfileReply(built.Outcome,false,null,false,false,false,false,observed.SelectedProfile,observed.AidlCalls==1,built.OriginalInputsRetained));
        var entries=File.ReadAllLines(Path.Combine(producer.EvidenceRoot,"out/entries.jsonl")).Select(raw=>JsonDocument.Parse(raw).RootElement.Clone()).ToArray();
        var method=entries.Single(entry=>entry.GetProperty("canonical_signature").GetString()=="aidl:a.I#ping()");
        var root=entries.Single(entry=>entry.GetProperty("canonical_signature").GetString()=="aidl:a.I");var extensions=method.GetProperty("extensions");
        var hasInherited=extensions.TryGetProperty("interface_oneway",out var inherited);
        var contract=new CatalogContracts(new CanonicalJson()).Project(method);
        return Task.FromResult(new CatalogAidlProfileReply("ok",hasInherited,hasInherited ? inherited.GetBoolean() : null,extensions.GetProperty("oneway").GetBoolean(),contract.GetProperty("oneway").GetBoolean(),root.GetProperty("extensions").GetProperty("oneway").GetBoolean(),
            entries.Count(entry=>entry.GetProperty("canonical_signature").GetString()!.StartsWith("aidl:",StringComparison.Ordinal))==2,observed.SelectedProfile,observed.AidlCalls==1,built.OriginalInputsRetained));
    }
}
[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogAidlProfileRealTests
{
    [Theory,MemberData(nameof(CatalogAidlProfileCases.All),MemberType=typeof(CatalogAidlProfileCases))]
    public Task ExistingFactoryBuildAndContractUseUnchangedProfileCallerCases(CatalogAidlProfileCase test)=>CatalogAidlProfileCases.AssertCase(test,new NativeCatalogAidlProfile());
}
