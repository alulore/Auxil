namespace Auxil.CodeGeneration.Common.Syntax.Shapes;

/// <summary>Shapes a do/while statement, writing it.</summary>
[PublicAPI]
public delegate void DoWhileShape(ShapeContext ctx, string condition, List<CodeStatement> body);
