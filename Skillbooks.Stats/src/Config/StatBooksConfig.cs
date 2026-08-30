using Vintagestory.API.Server;

namespace Skillbooks.Stats.Config
{
    /// <summary>
    /// Mirrors the parts of core's SkillBooksConfig that Stats also needs. Salvage and reroll
    /// are still core-only.
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
