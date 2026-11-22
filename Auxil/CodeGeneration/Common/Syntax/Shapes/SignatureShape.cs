namespace Auxil.CodeGeneration.Common.Syntax.Shapes;

/// <summary>Shapes a method/constructor/function signature; returns the signature text without modifiers or terminator.</summary>
[PublicAPI]
public delegate string SignatureShape(
    ShapeContext ctx,
    SignatureKind kind,
    string name,
    List<CodeParameter> parameters,
    List<CodeGenericParameter> generics,
    CodeType? returnType,
    string className,
    bool interfaceMode
);
