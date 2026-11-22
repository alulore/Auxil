namespace Auxil.CodeGeneration.Common.Syntax.Shapes;

/// <summary>Shapes a while statement, writing it.</summary>
[PublicAPI]
public delegate void WhileShape(ShapeContext ctx, string condition, List<CodeStatement> body);
