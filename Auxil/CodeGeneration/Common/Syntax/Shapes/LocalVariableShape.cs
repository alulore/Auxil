namespace Auxil.CodeGeneration.Common.Syntax.Shapes;

/// <summary>Shapes a local variable declaration; returns the declaration text or null to skip.</summary>
[PublicAPI]
public delegate string? LocalVariableShape(ShapeContext ctx, CodeLocalVariable variable);
