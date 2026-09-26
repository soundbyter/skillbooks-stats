using System.Collections.Generic;
using Vintagestory.API.Server;

namespace Skillbooks.Stats.Config
{
    /// <summary>
    /// Mirrors the parts of core's SkillBooksConfig that Stats also needs.
    /// </summary>
    public class StatBooksConfig
    {
        /// <summary>
        /// Bumped whenever a release needs to change an existing config file on load (see
        /// Migrate). Defaults to 0 so a file written before this field existed reads as
        /// version 0; freshly created configs are stamped with CurrentConfigVersion instead.
        /// </summary>
        public int ConfigVersion = 0;

        /// <summary>
        /// Ships with DefaultExcludedTraits. Newtonsoft replaces arrays wholesale on load, so
        /// a saved list -- even an empty one -- wins over this default; older files get the
        /// defaults added once through Migrate instead, and anything removed afterwards
        /// stays removed.
        /// </summary>
        public string[] TraitBlacklist = (string[])DefaultExcludedTraits.Clone();
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

        private const int CurrentConfigVersion = 1;

        /// <summary>
        /// Traits that exist in some mod's traits.json but no player is meant to have.
        /// "test" is Aldi's Classes' leftover dev trait (+10000% to several stats, granted
        /// by no class).
        /// </summary>
        private static readonly string[] DefaultExcludedTraits = { "test" };

        public static StatBooksConfig Load(ICoreServerAPI api)
        {
            StatBooksConfig config = api.LoadModConfig<StatBooksConfig>(FileName);
            if (config == null)
            {
                config = new StatBooksConfig { ConfigVersion = CurrentConfigVersion };
            }
            else
            {
                Migrate(config);
            }
            api.StoreModConfig(config, FileName);
            return config;
        }

        private static void Migrate(StatBooksConfig config)
        {
            if (config.ConfigVersion < 1)
            {
                // Pre-versioning files saved TraitBlacklist, usually as [], which would
                // otherwise override the new default exclusions forever.
                List<string> blacklist = new List<string>(config.TraitBlacklist ?? System.Array.Empty<string>());
                foreach (string traitCode in DefaultExcludedTraits)
                {
                    if (!blacklist.Contains(traitCode)) { blacklist.Add(traitCode); }
                }
                config.TraitBlacklist = blacklist.ToArray();
            }
            config.ConfigVersion = CurrentConfigVersion;
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
