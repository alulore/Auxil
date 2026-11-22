namespace Auxil.CodeGeneration.Common.Model.Statements;

/// <summary>
/// A member of an object literal: a name and exactly one value kind —
/// a raw expression, a nested object literal, or a function value.
/// </summary>
[PublicAPI]
public sealed class CodeObjectMember
{
    /// <summary>The member name, written verbatim.</summary>
    public required string Name { get; init; }

    /// <summary>The member value as a raw expression, if any.</summary>
    public string? Expression { get; set; }

    /// <summary>The member value as a nested object literal, if any.</summary>
    public CodeObjectLiteral? Object { get; set; }

    /// <summary>The member value as a function, if any.</summary>
    public CodeNestedFunction? Function { get; set; }
}
