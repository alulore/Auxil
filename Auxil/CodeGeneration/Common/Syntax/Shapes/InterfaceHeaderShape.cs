namespace Auxil.CodeGeneration.Common.Syntax.Shapes;

/// <summary>Shapes an interface declaration header; returns the header text without modifiers.</summary>
[PublicAPI]
public delegate string InterfaceHeaderShape(
    ShapeContext ctx,
    string name,
    List<CodeGenericParameter> generics,
    List<string> extendsTypes
);
