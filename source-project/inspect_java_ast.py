from tree_sitter import Language,Parser
import tree_sitter_java
p=Parser(Language(tree_sitter_java.language()))
b=b'package a; import java.util.List; public class X<T> { public static final String A="p", B="q"; public X(){} @Deprecated public List<String> f(final String s,int... xs){return null;} public @interface A {String value() default "";} }'
root=p.parse(b).root_node
print(root)
def visit(n,d=0):
 if d>3:return
 print(' '*d,n.type,repr(b[n.start_byte:n.end_byte].decode()[:90]),[(name,n.child_by_field_name(name).type if n.child_by_field_name(name) else None) for name in ('name','type','body','parameters','declarator','value')])
 for c in n.named_children:visit(c,d+1)
visit(root)
