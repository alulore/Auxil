namespace Auxil.CodeGeneration.Common.Model.Statements;

/// <summary>
/// An object literal statement (TypeScript object literal). The prefix
/// is leading text written before the opening brace (e.g. "return" for
/// "return { ... };"); members are comma-separated and the closing brace
/// carries the statement terminator. Members can be raw expressions,
/// nested object literals, or function values.
/// </summary>
[PublicAPI]
public sealed class CodeObjectLiteral : CodeStatement
{
    /// <summary>Leading text before the opening brace, if any (e.g. "return").</summary>
    public string? Prefix { get; set; }

    /// <summary>The literal members in declaration order.</summary>
    public List<CodeObjectMember> Members { get; } = [];
}
