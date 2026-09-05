# Skillbooks: Stats

A [Vintage Story](https://www.vintagestory.at/) mod: lore-styled, single-use books that
permanently grant a stat-modifying trait when read — the addon companion to
[Skillbooks](https://github.com/soundbyter/skillbooks-core).

Discovers traits with a non-empty `Attributes` block (stat modifiers like Fleetfooted's
movement speed bonus) dynamically at server start, and generates one book per trait
automatically — including traits added by other mods.

**Works fully standalone.** Skillbooks core is not required. If it's also installed, Stats
detects it automatically at startup and reuses its trait registry and curated flavour text
instead of duplicating them, so the two mods never drift into inconsistent behavior — but
neither ever forces the other to exist.

## Features

- **Dynamic discovery**, same as core: no per-trait configuration, new mods' stat traits pick
  up books automatically.
- **Curated flavour text** for every supported trait, whether or not core is installed —
  Stats carries its own copy for standalone mode, and reuses core's when it's present.
- **Negative traits excluded by default** (configurable) — reading a book is framed as a
  reward, so a curse showing up in that same pool would be a mismatch most players won't
  expect, though the tooltip always shows the real trait before you commit to reading.
- **Vessel loot and trader offers**, mirroring core's mechanisms.
- **Source attribution.** Each book's tooltip names which mod its trait came from (or "Vintage
  Story" for a base-game trait), so a modded trait's origin is never a mystery.
- **Handbook entries.** Books show up in the handbook like any other item by default; hideable
  via config for anyone who wants "which traits have books" to stay a surprise.
- **Admin commands.** `/skillbooksstats cleartraits` lets a player give back what they've
  learned so it can be relearned, and admins can do the same for a specific player or the
  whole server. Only registered in standalone mode -- core's own identical
  `/skillbooks cleartraits` already covers stat book traits too once core is installed, since
  both mods share the same underlying trait-history attribute.

## Installation

Get it from the [Vintage Story ModDB](https://mods.vintagestory.at/show/mod/64135) (installable
directly through the in-game mod browser), or grab the zip from the
[Releases page](https://github.com/soundbyter/skillbooks-stats/releases) here and drop it into
your Vintage Story `Mods` folder. No other mod required — install
[Skillbooks core](https://mods.vintagestory.at/show/mod/64127) alongside it too if you also want
crafting-trait books.

## Configuration

A config file is generated on first run at `ModConfig/skillbooksstats.json`. See the file
itself for the full set of options, including `HideFromHandbook` if you'd rather books not
show up there.

## Commands

- `/skillbooksstats cleartraits` — clears the traits you've learned from stat books so they can
  be learned again. Requires at least the default **Creative Player** role's privilege level
  (or a custom role of equivalent level).
- `/skillbooksstats cleartraits player <name>` — same, for a named online player. Requires at
  least the default **Survival Moderator** role's privilege level.
- `/skillbooksstats cleartraits all` — clears learned stat book traits for every online player.
  Same requirement as `player`.

These check the caller's role's privilege *level*, not one specific privilege — any role at or
above the threshold qualifies, including custom roles. Only registered when core isn't
installed — with core present, use its `/skillbooks cleartraits` instead, which already covers
traits from both mods (they share the same trait-history attribute under the hood). Offline
players aren't reachable; they need to be online for their traits to be cleared.

## For server admins: overriding a book's flavour text

**With Skillbooks core installed**, add entries to core's own `FlavourOverrides` in
`skillbooks.json` — Stats latches onto core's copy instead of keeping its own, so there's one
config file to manage overrides in regardless of whether a given trait code's book actually
came from core or Stats. See [core's README](https://github.com/soundbyter/skillbooks-core#for-server-admins-overriding-a-books-flavour-text)
for the format.

**Standalone (core not installed)**, add entries to `FlavourOverrides` in
`skillbooksstats.json` instead, keyed by trait code:

```json
"FlavourOverrides": {
  "fleetfooted": {
    "title": "Your Own Title",
    "blurb": "Your own in-world description."
  }
}
```

Either way, this overrides everything else for that trait code — mod-supplied overrides, the
curated list, and the procedural fallback. Either field can be omitted and falls back to
whatever the next tier would have provided.

## For mod authors: supplying your own flavour text

If your mod adds a stat trait, ship a file at
`assets/<yourmoddomain>/config/skillbooksstats/<traitcode>.json` in your own mod (note the
`config/` — Vintage Story's asset system only scans a fixed set of known top-level folders per
domain, so it has to sit under a recognized one):

```json
{
  "title": "Your Book's Title",
  "blurb": "The in-world description shown when the book is read or inspected."
}
```

This is checked first regardless of whether Skillbooks core is also installed — one file
covers both modes, no need to duplicate it under core's own path. A server admin's own
`FlavourOverrides` (above) still wins over this.

## Building from source

Requires the .NET SDK matching Vintage Story's target framework, a local Vintage Story install
(for its API DLLs, via the `VINTAGE_STORY` environment variable), and read access to the
`Skillbooks.Core` NuGet package on GitHub Packages (needed at compile time only — Stats still
runs fine at runtime without core installed). See [CONTRIBUTING.md](CONTRIBUTING.md) for the
full setup, then:

```
dotnet build Skillbooks.Stats/Skillbooks.Stats.csproj -c Release
```

## License

[GPL-3.0](LICENSE).

## AI Disclaimer

AI assistance has been used in the creation of flavour text for some of the books, documentation within this repository, comment writing within the codebase and repetitive tasks during development. The generated output was personally checked, modified and thoroughly tested by me for quality and accuracy and is subject to change at any time.
AI usage in contributing, documentation and pull requests is welcome so long as it is used safely and you have properly tested its output.
