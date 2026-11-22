namespace Auxil.CodeGeneration.Common.Syntax.Shapes;

/// <summary>Shapes an enum declaration, writing it; headerPrefix is the joined modifier prefix.</summary>
[PublicAPI]
public delegate void EnumShape(ShapeContext ctx, CodeEnum enm, string headerPrefix);
