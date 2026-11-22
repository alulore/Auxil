namespace Auxil.CodeGeneration.Common.Syntax.Shapes;

/// <summary>Shapes a class declaration header; returns the header text without modifiers.</summary>
[PublicAPI]
public delegate string ClassHeaderShape(
    ShapeContext ctx,
    string name,
    List<CodeGenericParameter> generics,
    string? extendsType,
    List<string> implementsTypes
);
