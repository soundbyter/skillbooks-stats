using System.Collections.Generic;
using Vintagestory.API.Server;

namespace Skillbooks.Stats.Config
{
    /// <summary>
    /// Mirrors the parts of core's SkillBooksConfig that Stats also needs.
    /// </summary>
    public class StatBooksConfig
    {
        public string[] TraitBlacklist = System.Array.Empty<string>();
        public string[] TraitAllowlist = System.Array.Empty<string>();

        /// <summary>
        /// Default false: a Negative-typed trait (a pure downside, e.g. Weak) doesn't get a
        /// book unless a server owner opts in -- reading one is framed as a reward, and a
        /// curse in that pool would be a mismatch. Mixed traits (tradeoffs, not pure
        /// downsides) stay included either way.
        /// </summary>
        public bool IncludeNegativeTraits = false;

        public double LootSpawnChance = 0.001;
        public string[] LootTargetBlockCodes = { "game:lootvessel-*" };

        public bool TraderEnabled = true;
        public string[] TraderOffers = { "treasurehunter" };
        public double TraderPriceMultiplier = 1.0;

        /// <summary>
        /// No public event fires on trader restock, so StatBookMarketPatcher polls each
        /// trader's "lastRefreshTotalDays" and rolls this chance itself on each advance.
        /// </summary>
        public double TraderSpawnChance = 0.005;

        /// <summary>
        /// Base price in rusty gears, before TraderPriceMultiplier and a small +/-2 random
        /// variance. Matches core's own TraderBasePrice for consistency between the two mods.
        /// </summary>
        public int TraderBasePrice = 24;

        public bool SalvageEnabled = true;
        public int SalvageLeatherAmount = 2;

        /// <summary>If true, only illegible/orphaned books can be salvaged.</summary>
        public bool SalvageIllegibleOnly = false;

        public bool RerollEnabled = true;

        /// <summary>If true, only illegible/orphaned books can be rerolled.</summary>
        public bool RerollIllegibleOnly = false;

        /// <summary>
        /// Stat books are shown in the handbook (alongside other items) by default. Set true
        /// to hide them again, keeping which traits have books a surprise until found in-world.
        /// </summary>
        public bool HideFromHandbook = false;

        /// <summary>
        /// Player-authored flavour text, keyed by trait code. Takes priority over everything
        /// else -- a mod-supplied override (see StatBookFlavour), the curated list, and the
        /// procedural fallback. Either field can be left null/omitted and falls back to
        /// whatever the next tier provides. Ignored (deferring to core's own skillbooks.json
        /// FlavourOverrides instead) when core is also installed -- see
        /// StatBookRegistry.ResolveFlavourWithOverride -- so there's one config file to manage
        /// overrides in rather than two that could quietly drift apart. Only takes effect in
        /// standalone mode.
        /// </summary>
        public Dictionary<string, FlavourOverride> FlavourOverrides = new Dictionary<string, FlavourOverride>();

        public class FlavourOverride
        {
            public string Title;
            public string Blurb;
        }

        /// <summary>
        /// The admin "charsel" command effectively starts a new character, which resets
        /// extraTraits down to whatever the freshly (re)selected class provides on its own --
        /// silently dropping any trait bonuses previously earned by reading a book. Default
        /// true: those bonuses are meant to be a permanent character upgrade, and losing them
        /// to an admin-gated command feels like an accidental side effect rather than intent.
        /// Set false to let charsel wipe them like a true fresh start. Ignored (deferring to
        /// core's own setting) when core is also installed -- see StatBookCharSelPatcher.
        /// </summary>
        public bool KeepTraitsOnCharSel = true;

        private const string FileName = "skillbooksstats.json";

        public static StatBooksConfig Load(ICoreServerAPI api)
        {
            StatBooksConfig config = api.LoadModConfig<StatBooksConfig>(FileName) ?? new StatBooksConfig();
            api.StoreModConfig(config, FileName);
            return config;
        }

        public bool IsTraitEnabled(string traitCode)
        {
            if (TraitAllowlist.Length > 0)
            {
                return System.Array.IndexOf(TraitAllowlist, traitCode) >= 0;
            }
            return System.Array.IndexOf(TraitBlacklist, traitCode) < 0;
        }
    }
}
