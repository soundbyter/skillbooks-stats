# Contributing to Skillbooks: Stats

## Setup

1. Own a Vintage Story install and set the `VINTAGE_STORY` environment variable to it, same as
   any Vintage Story mod build.
2. This repo consumes `Skillbooks.Core` as a NuGet package from GitHub Packages (needed at
   *compile* time only, to call `GetModSystem<SkillBooksModSystem>()` — the mod still runs
   fine at runtime with core absent). `nuget.config` at the repo root points at the feed;
   GitHub Packages requires a token to *read* packages too, not just publish them. If the
   package isn't public, set a token with `read:packages` scope before restoring:
   ```
   $env:GITHUB_TOKEN = gh auth token   # or any PAT with read:packages
   ```
3. `dotnet build Skillbooks.Stats/Skillbooks.Stats.csproj -c Release`. Output lands in
   `Skillbooks.Stats/bin/Release/Mods/mod` — copy that folder's *contents* into your `Mods`
   directory to test, or zip them for a release.

Test both modes if your change could plausibly affect either: with Skillbooks core installed
alongside (the "reuse core's resolver" path) and with it removed entirely from the mods
folder (the standalone path). A change that only breaks one of the two is easy to miss if you
only ever test with both installed.

## The one rule that actually matters here: JIT safety

Any code that references a `Skillbooks.*` type (from core) **must** live in its own method,
never inlined into a method that also has to run when core is absent. This isn't a style
preference — the .NET JIT resolves every type referenced anywhere in a method the moment that
method is compiled, not just the ones on the branch actually taken. A reference to a
`Skillbooks.*` type sitting behind a runtime `if (coreEnabled)` check inside a shared method
still crashes mod loading *entirely* when core's assembly isn't present — confirmed the hard
way during initial development (`Could not load file or assembly 'Skillbooks'...`), which took
the "no core installed, run standalone" fallback down with it.

The fix is always the same shape: isolate the core-touching call into its own private method,
only ever invoked from behind an `IsModEnabled("skillbooks")` check (a plain string comparison,
no type dependency, safe regardless of whether core is loaded). See
`StatBookRegistry.ResolveFlavourViaCore` and `SkillBooksStatsModSystem.GetCoreCraftingTraitCodes`
for the existing pattern — follow it for anything new that touches core's types.

## Code style

Same conventions as [core](https://github.com/soundbyter/skillbooks-core/blob/main/CONTRIBUTING.md#code-style):
comments only where the *why* is genuinely non-obvious, decompile-verify claims about engine
behavior rather than assuming, no speculative abstractions.

## Mod-supplied flavour overrides

`StatBookFlavour`'s tier 1 (`assets/<moddomain>/skillbooksstats/<traitcode>.json`) is public
surface other mod authors rely on directly, documented in the [README](README.md#for-mod-authors-supplying-your-own-flavour-text)
-- including the gotcha that this path is only consulted in standalone mode; with core present,
core's own tier 1 path is what's actually checked instead (see `StatBookRegistry.RegisterBook`).
Changing either lookup's JSON shape or path is a breaking change under the versioning policy
below, not a routine refactor.

## Versioning

Same [semver](https://semver.org/) policy as core. The one thing specific to this repo:
`Skillbooks.Stats.csproj`'s `<PackageReference Include="Skillbooks.Core" Version="...">` pin
has to be bumped to match whenever core publishes a new package version — nothing fails loudly
if it's left stale, it'll just quietly keep compiling against an old core API surface. If your
PR needs a newer core version, say so explicitly; that's a maintainer action (publishing the
package) that has to happen before the PR can build against it.

## Pull requests

Fork, branch, make your change, open a PR against `main` describing what changed, why, and
which mode(s) you tested it in (core present / standalone / both).
