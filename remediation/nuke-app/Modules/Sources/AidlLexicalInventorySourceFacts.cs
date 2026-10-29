namespace AndroidMcp.BuildTool.Sources;

/// <summary>The independent legacy no-drop inventory, composed from the shared lexical primitives.</summary>
public sealed class AidlLexicalInventorySourceFacts : IAidlLexicalInventorySourceFacts
{
    public AidlLexicalInventory Inventory(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var declarations = 0;
        var statements = 0;
        var reconciled = true;
        var issues = new List<string>();

        void Visit(string fragment)
        {
            foreach (var span in SourceSyntax.DeclarationSpans(fragment))
            {
                var raw = fragment[span.Start..span.End];
                var code = SourceSyntax.Mask(raw, strings: true);
                var header = SourceSyntax.Rx(@"\b(interface|parcelable|union|enum)\s+[\w.]+").Match(code);
                if (!header.Success) continue;
                declarations++;
                var open = code.IndexOf('{', header.Index + header.Length);
                if (open < 0) continue;
                var close = SourceSyntax.ClosingDelimiter(raw, open, '{', '}');
                if (close < 0)
                {
                    reconciled = false;
                    issues.Add("Unbalanced AIDL declaration body");
                    continue;
                }

                var body = SourceSyntax.Mask(raw[(open + 1)..close], strings: false);
                var nested = SourceSyntax.DeclarationSpans(body);
                var remaining = SourceSyntax.MaskSpans(body, nested);
                // Shift/comparison angle characters never open member-delimiter nesting.
                statements += SourceSyntax.Chunks(remaining, header.Groups[1].Value == "enum" ? ',' : ';').Count();
                Visit(body);
            }
        }

        Visit(text);
        return new(declarations, statements, reconciled, Array.AsReadOnly(issues.ToArray()),
            declarations == 0 ? 1 : declarations + statements);
    }
}
