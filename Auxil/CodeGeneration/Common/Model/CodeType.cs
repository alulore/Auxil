namespace Auxil.CodeGeneration.Common.Model;

/// <summary>
/// A type reference with a canonical name.
/// Language renderers map the canonical name through the language's
/// type map (e.g. "string" -> String in GDScript, string in TypeScript).
/// Unknown names (custom classes, engine types) pass through unchanged.
/// </summary>
[PublicAPI]
public sealed record CodeType(string Name)
{
    /// <summary>Creates a type reference from a canonical type name.</summary>
    /// <param name="name">The canonical type name.</param>
    public static CodeType Of(string name)
    {
        return new CodeType(name);
    }

    /// <summary>The canonical type name.</summary>
    public override string ToString()
    {
        return Name;
    }
}
