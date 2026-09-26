using System.Collections.Generic;
using Skillbooks.Stats.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Skillbooks.Stats
{
    /// <summary>
    /// Hooks stat books into vessel loot and trader offers, mirroring core's
    /// SkillBookLootPatcher. Loot side: loot-bearing blocks cache their drop list before
    /// AssetsFinalize runs, so this hooks DidBreakBlock instead. Trader side: no restock
    /// event exists, so this watches each matching trader's "lastRefreshTotalDays" watched
    /// attribute and rolls its own chance whenever that value changes.
    /// </summary>
    public static class StatBookMarketPatcher
    {
        private const int PollIntervalMs = 5000;

        /// <summary>
        /// WatchedAttributes key recording the "lastRefreshTotalDays" value this mod last
        /// rolled against. Stored on the entity so it survives restarts and chunk unloads;
        /// an in-memory baseline missed restocks that happen as a trader's chunk loads, which
        /// is how most players meet a trader. Separate from core's key so each mod rolls
        /// independently.
        /// </summary>
        private const string LastRolledRefreshKey = "skillbooksstats:lastRolledRefreshDay";

        /// <summary>
        /// Engine default for EntityTradingHumanoid.doubleRefreshIntervalDays (protected).
        /// Only used to tell whether a restock is imminent on first sighting.
        /// </summary>
        private const double TraderRefreshIntervalDays = 7.0;

        public static void RegisterLootHook(ICoreServerAPI api, Dictionary<string, DiscoveredStatTrait> statTraits, StatBooksConfig config)
        {
            if (statTraits.Count == 0) { return; }

            List<string> traitCodes = new List<string>(statTraits.Keys);
            AssetLocation[] targetPatterns = new AssetLocation[config.LootTargetBlockCodes.Length];
            for (int i = 0; i < targetPatterns.Length; i++)
            {
                targetPatterns[i] = AssetLocation.Create(config.LootTargetBlockCodes[i]);
            }

            api.Event.DidBreakBlock += (byPlayer, oldBlockId, blockSel) =>
            {
                Block oldBlock = api.World.GetBlock(oldBlockId);
                if (oldBlock?.Code == null) { return; }

                bool matches = false;
                foreach (AssetLocation pattern in targetPatterns)
                {
                    if (WildcardUtil.Match(pattern, oldBlock.Code)) { matches = true; break; }
                }
                if (!matches) { return; }

                // At most one book per break -- rolling every trait independently made
                // near-every vessel drop a stack.
                string traitCode = traitCodes[api.World.Rand.Next(traitCodes.Count)];
                if (api.World.Rand.NextDouble() >= config.LootSpawnChance) { return; }

                Item book = api.World.GetItem(new AssetLocation("skillbooksstats", "statbook-" + traitCode));
                if (book == null) { return; }

                api.World.SpawnItemEntity(new ItemStack(book), blockSel.Position);
            };

            api.Logger.Notification($"[Skillbooks: Stats] Loot hook armed for block pattern(s): {string.Join(", ", config.LootTargetBlockCodes)}");
        }

        /// <summary>
        /// Rolls TraderSpawnChance whenever a trader's refresh day differs from the one stored
        /// under LastRolledRefreshKey. A trader with no stored value is new to this mod and
        /// gets rolled once against its current stock. Checked on entity spawn/load (worldgen
        /// traders only fire OnEntityLoaded, and stock themselves before it) and on a short poll.
        /// </summary>
        public static void RegisterTraderHook(ICoreServerAPI api, Dictionary<string, DiscoveredStatTrait> statTraits, StatBooksConfig config)
        {
            if (!config.TraderEnabled || statTraits.Count == 0 || config.TraderOffers.Length == 0) { return; }

            List<string> traitCodes = new List<string>(statTraits.Keys);

            void Check(Entity entity)
            {
                if (entity is not EntityTradingHumanoid trader || trader.TradeProps == null) { return; }
                if (!MatchesConfiguredTrader(trader, config.TraderOffers)) { return; }
                CheckTrader(api, trader, traitCodes, config);
            }

            api.Event.OnEntitySpawn += Check;
            api.Event.OnEntityLoaded += Check;
            api.Event.RegisterGameTickListener(_ =>
            {
                foreach (Entity entity in api.World.LoadedEntities.Values) { Check(entity); }
            }, PollIntervalMs);

            api.Logger.Notification($"[Skillbooks: Stats] Trader hook armed for trader type(s): {string.Join(", ", config.TraderOffers)} ({config.TraderSpawnChance:P2} chance per rotation)");
        }

        private static void CheckTrader(ICoreServerAPI api, EntityTradingHumanoid trader, List<string> traitCodes, StatBooksConfig config)
        {
            ITreeAttribute attrs = trader.WatchedAttributes;
            // Not stocked yet -- the engine sets this alongside its first stock.
            if (!attrs.HasAttribute("lastRefreshTotalDays")) { return; }
            double currentRefreshDay = attrs.GetDouble("lastRefreshTotalDays");

            if (!attrs.HasAttribute(LastRolledRefreshKey))
            {
                attrs.SetDouble(LastRolledRefreshKey, currentRefreshDay);
                // Restock due on the trader's next tick -- skip rolling into stock that's about
                // to be replaced; the refresh day changing will trigger the roll instead.
                if (api.World.Calendar.TotalDays - currentRefreshDay > TraderRefreshIntervalDays) { return; }
            }
            else
            {
                // Any change counts, not just an advance -- a reimported trader resets to an
                // earlier day with fresh stock.
                if (currentRefreshDay == attrs.GetDouble(LastRolledRefreshKey)) { return; }
                attrs.SetDouble(LastRolledRefreshKey, currentRefreshDay);
            }

            ClearOwnBooks(trader);
            if (api.World.Rand.NextDouble() >= config.TraderSpawnChance) { return; }
            TryInjectStatBook(api, trader, traitCodes, config);
        }

        /// <summary>
        /// Removes this mod's book offers left over from the previous rotation. The engine's
        /// restock only rewrites the first Selling.MaxItems slots (at 50% each), and books
        /// usually land in the overflow slots past that, so without this an unsold book would
        /// sit there forever and fill the slot every later rotation needs.
        /// </summary>
        private static void ClearOwnBooks(EntityTradingHumanoid trader)
        {
            foreach (ItemSlotTrade slot in trader.Inventory.SellingSlots)
            {
                AssetLocation code = slot?.Itemstack?.Collectible?.Code;
                if (code == null || code.Domain != "skillbooksstats" || !code.Path.StartsWith("statbook-")) { continue; }

                // Not SetTradeItem(null) -- it dereferences its argument.
                slot.TradeItem = null;
                slot.Itemstack = null;
                slot.MarkDirty();
            }
        }

        private static bool MatchesConfiguredTrader(EntityTradingHumanoid trader, string[] traderOffers)
        {
            string path = trader.Code?.Path;
            if (string.IsNullOrEmpty(path)) { return false; }

            foreach (string traderCode in traderOffers)
            {
                if (path.Contains(traderCode)) { return true; }
            }
            return false;
        }

        private static void TryInjectStatBook(ICoreServerAPI api, EntityTradingHumanoid trader, List<string> traitCodes, StatBooksConfig config)
        {
            ItemSlotTrade[] sellingSlots = trader.Inventory.SellingSlots;
            ItemSlotTrade targetSlot = null;
            foreach (ItemSlotTrade slot in sellingSlots)
            {
                if (slot != null && (slot.TradeItem == null || slot.TradeItem.Stock <= 0))
                {
                    targetSlot = slot;
                    break;
                }
            }
            // No free slot this rotation -- skip rather than overwrite a real current offer.
            if (targetSlot == null) { return; }

            string traitCode = traitCodes[api.World.Rand.Next(traitCodes.Count)];
            Item book = api.World.GetItem(new AssetLocation("skillbooksstats", "statbook-" + traitCode));
            if (book == null) { return; }

            int price = System.Math.Max(1, (int)System.Math.Round((config.TraderBasePrice + api.World.Rand.Next(-2, 3)) * config.TraderPriceMultiplier));

            targetSlot.SetTradeItem(new ResolvedTradeItem
            {
                Stack = new ItemStack(book, 1),
                Price = price,
                Stock = 1,
                // Null by default, and ResolvedTradeItem.ToTreeAttributes dereferences it --
                // left unset, the trader can't be serialized, so it never reaches clients or
                // saves. Values match TradeItem's defaults.
                SupplyDemand = new SupplyDemandOpts { PriceChangePerDay = 0.1f, PriceChangePerPurchase = 0.1f },
            });
            targetSlot.MarkDirty();
        }
    }
}
