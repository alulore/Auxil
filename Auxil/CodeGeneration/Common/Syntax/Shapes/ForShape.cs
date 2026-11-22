namespace Auxil.CodeGeneration.Common.Syntax.Shapes;

/// <summary>Shapes a C-style for statement, writing it.</summary>
[PublicAPI]
public delegate void ForShape(
    ShapeContext ctx,
    string initializer,
    string condition,
    string increment,
    List<CodeStatement> body
);
