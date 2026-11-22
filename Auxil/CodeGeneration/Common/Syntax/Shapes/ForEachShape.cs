namespace Auxil.CodeGeneration.Common.Syntax.Shapes;

/// <summary>Shapes a foreach statement, writing it.</summary>
[PublicAPI]
public delegate void ForEachShape(
    ShapeContext ctx,
    string variable,
    string iterable,
    List<CodeStatement> body
);
