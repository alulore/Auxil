namespace Auxil.CodeGeneration.Common.Model.Statements;

/// <summary>
/// A single match arm: a pattern and its body.
/// </summary>
[PublicAPI]
public sealed class CodeMatchArm
{
    /// <summary>The pattern, as a raw expression; "_" maps to the default arm.</summary>
    public required string Pattern { get; init; }

    /// <summary>The arm body statements.</summary>
    public List<CodeStatement> Body { get; } = [];
}
