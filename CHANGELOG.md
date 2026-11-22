# Changelog

All notable changes to Auxil are documented here.

## 0.3.0 - 2026-11-22

### Breaking changes

- `FunctionStatementBuilder<TLanguage>.Async()` is now `AsyncModifier()`.
- The `DeclarationScope` extension `Async()` is now `AsyncModifier()`.
- `Auxil.Common.Cli.Cli` is now `CliCommand`.
- `Auxil.CodeGeneration.Common.Syntax.Shapes.Shapes` is now `SyntaxShapes`; the `Shapes` members on `ILanguage`, `LanguageProfile`, `RenderContext` and `ShapeContext` keep their names and now have the type `SyntaxShapes`.
- `CliCommand.OutputReceived` and `CliCommand.ErrorOutputReceived` are now `EventHandler<CliOutputEventArgs>` (was `EventHandler<string>`); read the line from `CliOutputEventArgs.Line`.
- `CliCommand.ExecuteAsync(CancellationToken, TimeSpan?)` is now `ExecuteAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)`.

## 0.2.0 - 2026-11-21

### Changed

- The repository moved to `alulore/Auxil`, and packages are published to the alulore GitHub Packages feed.

### Added

- Pathfinding: grid path finding under `Auxil.Pathfinding` — 2D maps and stacked maps, a
  general-purpose A* with terrain costs, bitboard A*, JPS, JPS+, HPA*, flow fields for crowds,
  route smoothing, nearest-target search, incremental search, and a worker pool.

## 0.1.0 - Initial standalone release

This is the first standalone release of Auxil. The project, package, and
public namespaces use the `Auxil` name and target .NET 10 with C# 14.
