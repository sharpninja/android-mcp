namespace AndroidMcp.BuildTool.Sources;

/// <summary>Independent lexical no-drop counts; no compiler, type, availability, or wire interpretation.</summary>
public sealed record AidlLexicalInventory(
    int Declarations,
    int Statements,
    bool Reconciled,
    IReadOnlyList<string> Issues,
    int ExpectedRecords);

public interface IAidlLexicalInventorySourceFacts
{
    /// <summary>Counts the supplied source exactly; null is a caller precondition error.</summary>
    AidlLexicalInventory Inventory(string text);
}
