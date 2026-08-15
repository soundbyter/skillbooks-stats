using System.Collections.Generic;
using Skillbooks.Stats.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
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
    /// event exists, so this polls each matching trader's "lastRefreshTotalDays" watched
    /// attribute and rolls its own chance whenever that value advances.
    /// </summary>
    public static class StatBookMarketPatcher
    {
        private const int PollIntervalMs = 60000;

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

        public static void RegisterTraderHook(ICoreServerAPI api, Dictionary<string, DiscoveredStatTrait> statTraits, StatBooksConfig config)
        {
            if (!config.TraderEnabled || statTraits.Count == 0 || config.TraderOffers.Length == 0) { return; }

            List<string> traitCodes = new List<string>(statTraits.Keys);
            Dictionary<long, double> lastKnownRefreshDay = new Dictionary<long, double>();

            api.Event.RegisterGameTickListener(_ =>
            {
                foreach (Entity entity in api.World.LoadedEntities.Values)
                {
                    if (entity is not EntityTradingHumanoid trader || trader.TradeProps == null) { continue; }
                    if (!MatchesConfiguredTrader(trader, config.TraderOffers)) { continue; }

                    double currentRefreshDay = trader.WatchedAttributes.GetDouble("lastRefreshTotalDays", double.MinValue);
                    bool seenBefore = lastKnownRefreshDay.TryGetValue(entity.EntityId, out double previousRefreshDay);
                    lastKnownRefreshDay[entity.EntityId] = currentRefreshDay;

                    // Only treat an *advance* as a real restock -- first-ever sighting of a
                    // trader just establishes a baseline, not a rotation to roll against.
                    if (!seenBefore || currentRefreshDay <= previousRefreshDay) { continue; }

                    if (api.World.Rand.NextDouble() >= config.TraderSpawnChance) { continue; }

                    TryInjectStatBook(api, trader, traitCodes, config);
                }
            }, PollIntervalMs);

            api.Logger.Notification($"[Skillbooks: Stats] Trader hook armed for trader type(s): {string.Join(", ", config.TraderOffers)} ({config.TraderSpawnChance:P2} chance per rotation)");
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
            });
            targetSlot.MarkDirty();
        }
    }
}
