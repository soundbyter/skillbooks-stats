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

## Installation

Grab the latest release from the [Releases page](https://github.com/soundbyter/skillbooks-stats/releases)
and drop the zip into your Vintage Story `Mods` folder (or extract it there). No other mod
required — install [Skillbooks core](https://github.com/soundbyter/skillbooks-core) alongside
it too if you also want crafting-trait books.

## Configuration

A config file is generated on first run at `ModConfig/skillbooksstats.json`. See the file
itself for the full set of options.

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
