using System.Text;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

// The pinned grammar recovers one method whose modifiers are "@A( public".
// Original modifiers calls parsers.annotations and propagates its ValueError;
// the managed equivalent is FormatException, caught by the existing dispatcher.
public static class JavaAnnotationFailureCases
{
    public const string Text="class C { @A( public int f(){} }";
    public static SourceDocument Input()=>new("MalformedAnnotation.java",Encoding.UTF8.GetBytes(Text),new("fixture","pinned","java","37.2"));
    public static void AssertContract(ISourceExtractor provider)
    {
        var input=Input();var before=input.Bytes.ToArray();
        var error=Assert.Throws<FormatException>(()=>provider.Extract(input));
        Assert.Equal("Unclosed annotation",error.Message);Assert.Equal(before,input.Bytes.ToArray());
    }
}
