using static AndroidMcp.BuildTool.Tests.Sources.Java.JavaContractCases;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

public static class JavaProducerTypeTextCases
{
    // Original skips only java.lang.<simple>, not java.lang subpackages.
    public static readonly JavaCase Case=new("JAVA-PRODUCER-java-lang-subpackage",
        "package a; import java.lang.annotation.Retention; class C { Retention f(Retention value){ return value; } }",
        [Object(("kind","package"),("canonical","a"),("name","a")),
         Object(("kind","class"),("canonical","a.C"),("name","C")),
         Object(("kind","method"),("canonical","a.C#f(java.lang.annotation.Retention)"),("name","f"),("return_type","java.lang.annotation.Retention"),
             ("parameters",Array(Object(("name","value"),("type","java.lang.annotation.Retention"),("direction",null),("annotations",Array())))))],
        Object(("package","a"),("imports",Object(("Retention","java.lang.annotation.Retention"))),("wildcard_imports",Array())));
}
