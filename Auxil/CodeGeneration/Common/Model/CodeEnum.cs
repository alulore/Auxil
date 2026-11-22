namespace Auxil.CodeGeneration.Common.Model;

/// <summary>
/// An enum declaration.
/// </summary>
[PublicAPI]
public sealed class CodeEnum : CodeMember
{
    /// <summary>The enum name.</summary>
    public required string Name { get; init; }

    /// <summary>The enum members in declaration order.</summary>
    public List<CodeEnumMember> Members { get; } = [];
}
