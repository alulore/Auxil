---
name: verify
description: >
  Use before calling a change to Auxil's public API done: call it from a throwaway
  consumer project the way a library user would, and report what you saw.
---

# Verify

Use this before calling a change to Auxil's public API or behaviour done. Tests prove the code; this proves a user can call it.

## Start

Make a throwaway console app in a temp folder, outside the repo, so the repo's build rules don't apply to it:

- `dotnet new console -n Consumer -o <temp> --framework net11.0`
- `dotnet add <temp> reference D:/Dev/Projects/Auxil/Auxil/Auxil.csproj` (use this repo's real path)

## Drive

Write `Program.cs` the way a user would: only `using Auxil...` namespaces and public members, with no `InternalsVisibleTo` and no reflection. Call the changed API with ordinary input and with the edge case the change is about, and print the results. Then run `dotnet run --project <temp>`.

- Async or lifetime APIs (FileWatcher, PathfinderPool): `await` them and `Dispose` them as a user would, and print what you observe.
- Code generation (CodeBuilder): print `Render()` and read the generated text.
- Packaging (csproj, package metadata, target frameworks): `dotnet pack Auxil -o <temp>/feed`, then reference the package from the consumer instead of the project, using a `nuget.config` with `<temp>/feed` as a source.

`dotnet run --project LibraryConsole` is a quick check that the existing samples still run. It is not a substitute for calling the changed API.

## Done looks like

- The consumer compiles against the public surface without warnings you would have to suppress.
- The printed output is what the change promises, for both the ordinary input and the edge case.
- XML docs on new public members read right in the consumer. If it's quick, hover them or read the generated `Auxil.xml`.

## Stop

Delete the temp folder.

## Report

Say what the consumer called, with which inputs, and what it printed. If you could not run it, say so plainly.
