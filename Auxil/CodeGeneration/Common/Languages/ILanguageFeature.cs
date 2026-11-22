namespace Auxil.CodeGeneration.Common.Languages;

/// <summary>
/// Feature trait interfaces. A language struct implements the traits for
/// the features it supports; builder methods are gated on these traits,
/// so unsupported features are compile errors. The renderer derives its
/// capability list from the same traits.
/// </summary>
[PublicAPI]
public interface ILanguageFeature;
