namespace Auxil.CodeGeneration.Common.Model;

/// <summary>
/// Canonical built-in type names understood by the type maps.
/// </summary>
[PublicAPI]
public static class CodeTypes
{
    /// <summary>The string type.</summary>
    public static CodeType String => new("string");

    /// <summary>The int type.</summary>
    public static CodeType Int => new("int");

    /// <summary>The float type.</summary>
    public static CodeType Float => new("float");

    /// <summary>The double type.</summary>
    public static CodeType Double => new("double");

    /// <summary>The bool type.</summary>
    public static CodeType Bool => new("bool");

    /// <summary>The void type.</summary>
    public static CodeType Void => new("void");

    /// <summary>The object type.</summary>
    public static CodeType Object => new("object");

    /// <summary>The array type.</summary>
    public static CodeType Array => new("array");

    /// <summary>The dictionary/map type.</summary>
    public static CodeType Dictionary => new("dictionary");

    /// <summary>The null type.</summary>
    public static CodeType Null => new("null");

    /// <summary>The dynamic/untyped type.</summary>
    public static CodeType Dynamic => new("dynamic");
}
