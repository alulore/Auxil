namespace Auxil.CodeGeneration.Common.Syntax.Shapes;

/// <summary>Shapes a match/switch statement, writing it.</summary>
[PublicAPI]
public delegate void MatchShape(ShapeContext ctx, CodeMatch statement);
