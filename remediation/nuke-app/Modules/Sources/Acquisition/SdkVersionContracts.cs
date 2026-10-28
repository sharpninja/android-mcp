namespace AndroidMcp.BuildTool.Modules.Sources.Acquisition;
public interface ISdkArchiveVersionValidator
{
    long ResolveSize(ReadOnlyMemory<byte> repositoryXml,string platformVersion,string? packagePath=null,long? expectedSize=null);
}
public enum AcquisitionCompatibilityProfile { CanonicalRestore, GenericRehydrate }
