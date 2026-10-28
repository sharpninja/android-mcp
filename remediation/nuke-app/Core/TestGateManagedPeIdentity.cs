using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace AndroidMcp.BuildTool.Core;

// One BCL PE/hash authority for collector and in-process origin observations.
public static class TestGateManagedPeIdentity
{
    public static TestGateBinaryIdentity Observe(string path,ReadOnlyMemory<byte> bytes)
    {
        try
        {
            using var input=new MemoryStream(bytes.ToArray(),false);using var pe=new PEReader(input);
            if(!pe.HasMetadata)throw new BadImageFormatException("Managed metadata is absent.");
            var metadata=pe.GetMetadataReader();var id=metadata.GetGuid(metadata.GetModuleDefinition().Mvid);
            if(id==Guid.Empty)throw new BadImageFormatException("Module identity is absent.");
            return new(path,ContentHash.Sha256(bytes.Span),id);
        }
        catch(Exception error)when(error is BadImageFormatException or InvalidOperationException or ArgumentException)
        {throw new TestGateFactsException("BINARY_METADATA_INVALID","Observed bytes are not supported managed PE metadata.");}
    }
}
