namespace Auxil.CodeGeneration.Common.Model.Statements;

/// <summary>
/// A catch block of a CodeTry.
/// </summary>
[PublicAPI]
public sealed class CodeCatch
{
    /// <summary>The exception type, or null for an untyped catch.</summary>
    public CodeType? Type { get; init; }

    /// <summary>The exception variable name, or null for a bare catch.</summary>
    public string? Variable { get; init; }

    /// <summary>The catch-block statements.</summary>
    public List<CodeStatement> Body { get; } = [];
}
