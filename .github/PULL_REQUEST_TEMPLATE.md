## What changed and why

## Tested how

- [ ] Built cleanly (`dotnet build Skillbooks.Stats/Skillbooks.Stats.csproj -c Release`)
- [ ] Tested standalone (core absent from the mods folder)
- [ ] Tested with Skillbooks core installed
- [ ] N/A -- doesn't touch anything mode-dependent

## If this touches anything that references a `Skillbooks.*` (core) type

- [ ] That reference is isolated in its own method, only called after `IsModEnabled("skillbooks")` -- see [CONTRIBUTING.md](CONTRIBUTING.md#the-one-rule-that-actually-matters-here-jit-safety). A shared method with a core-type reference anywhere in it, even behind a runtime check, crashes mod loading with core absent.

## Public API surface

- [ ] This does not need a newer `Skillbooks.Core` package version
- [ ] This *does* -- flag it, publishing the package is a maintainer action that has to happen before this can build
