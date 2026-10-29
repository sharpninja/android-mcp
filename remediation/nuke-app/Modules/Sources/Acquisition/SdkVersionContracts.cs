namespace AndroidMcp.BuildTool.Modules.Sources.Acquisition;
public interface ISdkArchiveVersionValidator
{
    long ResolveSize(ReadOnlyMemory<byte> repositoryXml,string platformVersion,string? packagePath=null,long? expectedSize=null);
}
public sealed record SdkArchivePackageChoice(bool Present,bool IsString,string? Value);
public interface ISdkArchivePackageVersionValidator:ISdkArchiveVersionValidator
{
    long ResolvePackageSize(ReadOnlyMemory<byte> repositoryXml,string platformVersion,SdkArchivePackageChoice package,long? expectedSize=null);
}
public enum AcquisitionCompatibilityProfile { CanonicalRestore, GenericRehydrate }
