using System.Collections.Generic;
using Skillbooks.Stats.Config;
using Skillbooks.Stats.Recipes;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Skillbooks.Stats
{
    public class SkillBooksStatsModSystem : ModSystem
    {
        public Dictionary<string, DiscoveredStatTrait> StatTraits { get; private set; } = new Dictionary<string, DiscoveredStatTrait>();

        public StatBooksConfig Config { get; private set; }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterItemClass(ItemStatBook.ClassName, typeof(ItemStatBook));
        }

        public override void AssetsFinalize(ICoreAPI api)
        {
            base.AssetsFinalize(api);
            if (api is not ICoreServerAPI sapi) { return; }

            StatBooksConfig config = StatBooksConfig.Load(sapi);
            Config = config;
            bool coreEnabled = sapi.ModLoader.IsModEnabled("skillbooks");

            HashSet<string> excludeCodes = coreEnabled ? GetCoreCraftingTraitCodes(sapi) : new HashSet<string>();

            Dictionary<string, DiscoveredStatTrait> statTraits = StatTraitDiscovery.Run(sapi, config, excludeCodes);
            StatTraits = statTraits;
            HashSet<string> knownTraitCodes = StatTraitHistory.LoadAndUpdate(sapi, statTraits.Keys);
            StatBookRegistry.Generate(sapi, statTraits, knownTraitCodes, config, coreEnabled);
            StatBookMarketPatcher.RegisterLootHook(sapi, statTraits, config);
            StatBookMarketPatcher.RegisterTraderHook(sapi, statTraits, config);
            StatBookCharSelPatcher.Register(sapi, config);
            StatSalvageRecipe.Register(sapi, config);
            if (!coreEnabled) { StatBookCommands.Register(sapi); }

            if (coreEnabled)
            {
                sapi.Logger.Notification("[Skillbooks: Stats] Skillbooks core detected. Reusing its flavour resolver for stat books.");
            }
            else
            {
                sapi.Logger.Notification("[Skillbooks: Stats] Skillbooks core not installed. Running standalone.");
            }
        }

        /// <summary>
        /// Isolated in its own method, only called after IsModEnabled has already confirmed
        /// core is present -- see StatBookRegistry.ResolveFlavourViaCore for why referencing
        /// core's types must stay isolated like this rather than being inlined here.
        ///
        /// Runs core's own discovery logic directly rather than reading core.CraftingTraits
        /// off its live ModSystem instance -- that property is only populated inside core's
        /// own AssetsFinalize, and mod load order does not guarantee that runs before Stats'
        /// (confirmed via a real crash report showing "skillbooksstats" loading before
        /// "skillbooks" with a large enough mod list). Unlike Config (a plain property with no
        /// default, so reading it early is a NullReferenceException), CraftingTraits defaults
        /// to an empty dictionary -- so this wouldn't crash, but would silently exclude
        /// nothing, letting a trait core already covers as a crafting-trait book also get a
        /// redundant stat book. TraitDiscovery.Run is a pure, side-effect-free scan of already-
        /// loaded recipes and traits.json; safe to run again here even though core will also
        /// run it again itself moments later.
        /// </summary>
        private static HashSet<string> GetCoreCraftingTraitCodes(ICoreServerAPI sapi)
        {
            Skillbooks.Config.SkillBooksConfig coreConfig = Skillbooks.Config.SkillBooksConfig.Load(sapi);
            return new HashSet<string>(Skillbooks.TraitDiscovery.Run(sapi, coreConfig).Keys);
        }
    }
}
