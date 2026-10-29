using System.Text;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation;

// The admitted renderer profile is the only version/hash authority. This keeps
// the original text comparison, including Python read_text universal newlines.
public sealed class DocumentationWrapperPropertiesValidator:IDocumentationWrapperPropertiesValidator
{
    public void Validate(DocumentationRendererProfile profile,Artifact properties)
    {
        try {
            var distribution=profile.Downloads.Single(pin=>pin.LocalPath==".tools/gradle-"+profile.GradleVersion+"-bin.zip");
            var expected="distributionBase=GRADLE_USER_HOME\ndistributionPath=wrapper/dists\ndistributionUrl=https\\://services.gradle.org/distributions/gradle-"+
                profile.GradleVersion+"-bin.zip\ndistributionSha256Sum="+distribution.Sha256+
                "\nnetworkTimeout=10000\nvalidateDistributionUrl=true\nzipStoreBase=GRADLE_USER_HOME\nzipStorePath=wrapper/dists\n";
            var observed=new UTF8Encoding(false,true).GetString(properties.Bytes.Span).Replace("\r\n","\n",StringComparison.Ordinal).Replace('\r','\n');
            if(properties.RelativePath=="gradle/wrapper/gradle-wrapper.properties"&&observed==expected)return;
        }
        catch(Exception error)when(error is DecoderFallbackException or InvalidOperationException){ }
        throw new ArchitectureAssetException("WRAPPER_PROPERTIES","Committed wrapper properties differ from pinned expected configuration");
    }
}
