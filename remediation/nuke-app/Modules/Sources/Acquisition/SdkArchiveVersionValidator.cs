using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using AndroidMcp.BuildTool.Core;
namespace AndroidMcp.BuildTool.Modules.Sources.Acquisition;

public sealed class SdkArchiveVersionValidator:ISdkArchivePackageVersionValidator
{
    public long ResolveSize(ReadOnlyMemory<byte> repositoryXml,string platformVersion,string? packagePath=null,long? expectedSize=null)
        =>ResolveCore(repositoryXml,platformVersion,packagePath,packagePath is null,true,expectedSize);
    public long ResolvePackageSize(ReadOnlyMemory<byte> repositoryXml,string platformVersion,SdkArchivePackageChoice package,long? expectedSize=null)
        =>ResolveCore(repositoryXml,platformVersion,package.Value,!package.Present,package.IsString,expectedSize);
    private static long ResolveCore(ReadOnlyMemory<byte> repositoryXml,string platformVersion,string? packagePath,bool useDefault,bool stringChoice,long? expectedSize)
    {
        if(string.IsNullOrWhiteSpace(platformVersion))throw new TransportException("SDK_PLATFORM","Explicit platform API version is required.");
        var wanted=useDefault?"platforms;android-"+platformVersion:packagePath;XDocument document;
        try
        {
            using var stream=new MemoryStream(repositoryXml.ToArray(),false);
            using var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=8*1024*1024});
            document=XDocument.Load(reader);
        }
        catch(XmlException){throw new TransportException("SDK_XML","Invalid repository XML; DTD and external entities are prohibited.");}
        var packages=!useDefault&&(!stringChoice||wanted is null)?[]:
            document.Root?.Elements().Where(x=>x.Name.LocalName=="remotePackage"&&(string?)x.Attribute("path")==wanted).ToArray()??[];
        if(packages.Length!=1)throw new TransportException("SDK_PACKAGE","Exact SDK platform package absent or ambiguous.");
        var sizes=packages[0].Elements().Where(x=>x.Name.LocalName=="archives").SelectMany(x=>x.Elements().Where(y=>y.Name.LocalName=="archive")).SelectMany(x=>x.Elements().Where(y=>y.Name.LocalName=="complete")).SelectMany(x=>x.Elements().Where(y=>y.Name.LocalName=="size")).ToArray();
        if(sizes.Length!=1||!long.TryParse(sizes[0].Value,NumberStyles.None,CultureInfo.InvariantCulture,out var size)||size<=0||expectedSize is not null&&size!=expectedSize)throw new TransportException("SDK_SIZE","Exact SDK archive size absent, ambiguous or differs from pin.");
        return size;
    }
}
