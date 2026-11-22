namespace Auxil.CodeGeneration.Common.Model.Statements;

/// <summary>
/// A match (GDScript) / switch (TypeScript, C#) statement. Patterns are
/// raw expressions; the "_" pattern maps to the default arm.
/// </summary>
[PublicAPI]
public sealed class CodeMatch : CodeStatement
{
    /// <summary>The matched expression, as a raw expression.</summary>
    public required string Expression { get; init; }

    /// <summary>The arms in order.</summary>
    public List<CodeMatchArm> Arms { get; } = [];
}
