namespace Auxil.CodeGeneration.Common.Syntax.Shapes;

/// <summary>Shapes a field declaration; returns the declaration text without modifiers or terminator.</summary>
[PublicAPI]
public delegate string FieldShape(
    ShapeContext ctx,
    string name,
    CodeType? type,
    bool nullable,
    string? defaultValue,
    bool isConst,
    string keyword,
    bool isInterfaceMember
);
