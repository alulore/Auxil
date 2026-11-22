namespace Auxil.CodeGeneration.Common.Syntax.Shapes;

/// <summary>Shapes an if/else-if/else statement, writing it.</summary>
[PublicAPI]
public delegate void IfShape(ShapeContext ctx, CodeIf statement);
