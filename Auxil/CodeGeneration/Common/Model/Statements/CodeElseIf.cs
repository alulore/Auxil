namespace Auxil.CodeGeneration.Common.Model.Statements;

/// <summary>
/// An elif/else-if branch of a CodeIf.
/// </summary>
[PublicAPI]
public sealed class CodeElseIf
{
    /// <summary>The branch condition, as a raw expression.</summary>
    public required string Condition { get; init; }

    /// <summary>The branch statements.</summary>
    public List<CodeStatement> Body { get; } = [];
}
