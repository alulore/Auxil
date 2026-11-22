namespace Auxil.CodeGeneration.Common.Syntax.Shapes;

/// <summary>Which declaration kind a signature belongs to.</summary>
[PublicAPI]
public enum SignatureKind
{
    /// <summary>A method (class member).</summary>
    Method,

    /// <summary>A constructor.</summary>
    Constructor,

    /// <summary>A top-level function.</summary>
    Function,
}
