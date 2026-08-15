using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Skillbooks.Stats.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Skillbooks.Stats
{
    /// <summary>
    /// Mirrors core's own DiscoveredTrait, but a separate local type -- this class must stay
    /// self-contained and safe to use even when core isn't installed.
    /// </summary>
    public class DiscoveredStatTrait
    {
        public Trait Trait;
        public string SourceDomain;
    }

    /// <summary>
    /// Discovers traits with a non-empty Attributes block (stat modifiers like Fleetfooted's
    /// walkspeed bonus), as opposed to core's recipe-gated crafting traits. Fully
    /// self-contained -- doesn't reference core at all. excludeCodes lets the caller pass in
    /// core's already-discovered codes (when core is present), since a modded trait can
    /// carry both a crafting gate and stat Attributes at once.
    /// </summary>
    public static class StatTraitDiscovery
    {
        public static Dictionary<string, DiscoveredStatTrait> Run(ICoreServerAPI api, StatBooksConfig config, IReadOnlySet<string> excludeCodes)
        {
            Dictionary<string, DiscoveredStatTrait> allTraits = LoadAllTraits(api);

            Dictionary<string, DiscoveredStatTrait> statTraits = new Dictionary<string, DiscoveredStatTrait>();
            int filteredOut = 0;
            int claimedByCore = 0;
            int negativeExcluded = 0;
            foreach (KeyValuePair<string, DiscoveredStatTrait> entry in allTraits)
            {
                Trait trait = entry.Value.Trait;
                if (trait.Attributes == null || trait.Attributes.Count == 0) { continue; }

                if (excludeCodes.Contains(entry.Key))
                {
                    claimedByCore++;
                    continue;
                }
                if (trait.Type == EnumTraitType.Negative && !config.IncludeNegativeTraits)
                {
                    negativeExcluded++;
                    continue;
                }
                if (!config.IsTraitEnabled(entry.Key))
                {
                    filteredOut++;
                    continue;
                }
                statTraits[entry.Key] = entry.Value;
            }

            api.Logger.Event($"[Skillbooks: Stats] Discovered {statTraits.Count} stat trait(s) ({filteredOut} excluded by config, {claimedByCore} already a crafting trait book, {negativeExcluded} negative trait(s) excluded): {string.Join(", ", statTraits.Keys)}");
            return statTraits;
        }

        /// <summary>
        /// Mirrors core's own TraitDiscovery.LoadAllTraits, done independently here since
        /// CharacterSystem.TraitsByCode only populates later, at ModsAndConfigReady.
        /// </summary>
        private static Dictionary<string, DiscoveredStatTrait> LoadAllTraits(ICoreServerAPI api)
        {
            Dictionary<string, DiscoveredStatTrait> traits = new Dictionary<string, DiscoveredStatTrait>();
            Dictionary<AssetLocation, JToken> many = api.Assets.GetMany<JToken>(api.Logger, "config/traits", null);

            foreach (var (loc, token) in many)
            {
                if (token is JObject)
                {
                    AddTrait(traits, JsonUtil.ToObject<Trait>(token, loc.Domain, null), loc.Domain);
                }
                else if (token is JArray array)
                {
                    foreach (JToken entry in array)
                    {
                        AddTrait(traits, JsonUtil.ToObject<Trait>(entry, loc.Domain, null), loc.Domain);
                    }
                }
            }

            return traits;
        }

        private static void AddTrait(Dictionary<string, DiscoveredStatTrait> traits, Trait trait, string sourceDomain)
        {
            if (trait?.Code == null) { return; }
            traits[trait.Code] = new DiscoveredStatTrait { Trait = trait, SourceDomain = sourceDomain };
        }
    }
}
