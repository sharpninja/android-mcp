using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;

public sealed class NativeSdkLegacyPlatform(Func<SdkLegacyPlatformValue,JsonElement?,JsonElement?,string>? interpret=null):ISdkLegacyPlatformProbe
{
    public SdkLegacyPlatformObservation Run(SdkLegacyPlatformCase c)
    {
        using var row=c.Row is null?null:JsonDocument.Parse(c.Row);
        using var transport=c.Transport is null?null:JsonDocument.Parse(c.Transport);
        var beforeRow=row?.RootElement.GetRawText();var beforeTransport=transport?.RootElement.GetRawText();
        var canonical=new RecordingCanonical();string? text=null;string? error=null;
        try{var service=new SdkLegacyPlatformValue(canonical);text=interpret is null?service.Resolve(row?.RootElement,transport?.RootElement):interpret(service,row?.RootElement,transport?.RootElement);}
        catch(TransportException e){error=e.Code;}
        return new(text,error,canonical.Calls,beforeRow==row?.RootElement.GetRawText()&&beforeTransport==transport?.RootElement.GetRawText());
    }
    private sealed class RecordingCanonical:ICanonicalJson
    {
        public int Calls{get;private set;}
        public byte[] Serialize(JsonElement value){Assert.Equal(JsonValueKind.Number,value.ValueKind);Calls++;return new CanonicalJson().Serialize(value);}
        public byte[] Serialize(ReadOnlyMemory<byte> rawUtf8)=>throw new InvalidOperationException("No SDK raw JSON parse authority.");
        public string Hash(ReadOnlyMemory<byte> rawUtf8)=>throw new InvalidOperationException("No SDK hash substitution.");
        public string Hash(JsonElement value)=>throw new InvalidOperationException("No SDK hash substitution.");
    }
}
[Trait("Gate","Real")]
public sealed class SdkLegacyPlatformRealTests
{
    [Theory,MemberData(nameof(SdkLegacyPlatformCases.All),MemberType=typeof(SdkLegacyPlatformCases))]
    public void SameCasesUseSharedCanonicalNumericAuthority(SdkLegacyPlatformCase c)=>SdkLegacyPlatformCases.AssertCase(c,new NativeSdkLegacyPlatform());
}
