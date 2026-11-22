namespace Auxil.CodeGeneration.Common.Syntax.Shapes;

/// <summary>Shapes an object literal statement, writing it (members comma-separated, terminator on the closing brace).</summary>
[PublicAPI]
public delegate void ObjectLiteralShape(ShapeContext ctx, CodeObjectLiteral literal);
