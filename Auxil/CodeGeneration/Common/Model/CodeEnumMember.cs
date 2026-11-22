namespace Auxil.CodeGeneration.Common.Model;

/// <summary>
/// A single enum member (name and optional explicit value).
/// </summary>
[PublicAPI]
public sealed class CodeEnumMember
{
    /// <summary>The member name.</summary>
    public required string Name { get; init; }

    /// <summary>An optional explicit value, as a raw expression.</summary>
    public string? Value { get; set; }
}
