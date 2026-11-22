namespace Auxil.CodeGeneration.Common.Syntax.Shapes;

/// <summary>Shapes a raw block statement, writing it.</summary>
[PublicAPI]
public delegate void RawBlockShape(
    ShapeContext ctx,
    string header,
    List<CodeStatement> body,
    string? closeSuffix
);
