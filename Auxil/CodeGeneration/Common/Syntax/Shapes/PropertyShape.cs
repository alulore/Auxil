namespace Auxil.CodeGeneration.Common.Syntax.Shapes;

/// <summary>Shapes a property declaration, writing it (including accessor bodies).</summary>
[PublicAPI]
public delegate void PropertyShape(
    ShapeContext ctx,
    CodeProperty property,
    string prefix,
    bool isTopLevel,
    bool isInterfaceMember
);
