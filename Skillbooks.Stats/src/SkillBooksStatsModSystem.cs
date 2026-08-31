using System.Collections.Generic;
using Skillbooks.Stats.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Skillbooks.Stats
{
    public class SkillBooksStatsModSystem : ModSystem
    {
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
            bool coreEnabled = sapi.ModLoader.IsModEnabled("skillbooks");

            HashSet<string> excludeCodes = coreEnabled ? GetCoreCraftingTraitCodes(sapi) : new HashSet<string>();

            Dictionary<string, DiscoveredStatTrait> statTraits = StatTraitDiscovery.Run(sapi, config, excludeCodes);
            StatBookRegistry.Generate(sapi, statTraits, coreEnabled);
            StatBookMarketPatcher.RegisterLootHook(sapi, statTraits, config);
            StatBookMarketPatcher.RegisterTraderHook(sapi, statTraits, config);
            StatBookCharSelPatcher.Register(sapi, config);

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
        /// </summary>
        private static HashSet<string> GetCoreCraftingTraitCodes(ICoreServerAPI sapi)
        {
            Skillbooks.SkillBooksModSystem core = sapi.ModLoader.GetModSystem<Skillbooks.SkillBooksModSystem>();
            return new HashSet<string>(core.CraftingTraits.Keys);
        }
    }
}
