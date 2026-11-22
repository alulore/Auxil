namespace Auxil.CodeGeneration.Common.Syntax.Shapes;

/// <summary>Shapes a try/catch/finally statement, writing it.</summary>
[PublicAPI]
public delegate void TryShape(ShapeContext ctx, CodeTry statement);
