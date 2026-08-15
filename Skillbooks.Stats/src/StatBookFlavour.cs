using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace Skillbooks.Stats
{
    /// <summary>
    /// Self-contained three-tier flavour resolver for standalone mode: mod-supplied
    /// (assets/&lt;domain&gt;/config/skillbooksstats/&lt;code&gt;.json), curated
    /// (assets/skillbooksstats/config/flavour-curated.json), then a procedural fallback.
    /// Zero reference to any Skillbooks.* type -- runs whether or not core is present (see
    /// StatBookRegistry.ResolveFlavour for why the mod-supplied tier specifically is checked
    /// in both cases). The mod-supplied path lives under config/, not a bare top-level
    /// folder -- AssetManager only scans the fixed set of AssetCategory folder names, so
    /// anything outside that set is never indexed at all, regardless of correct placement.
    /// </summary>
    public static class StatBookFlavour
    {
        public class FlavourText
        {
            public string Title;
            public string Blurb;
        }

        private static Dictionary<string, FlavourText> curatedCache;

        public static (string title, string blurb) Resolve(ICoreServerAPI api, string traitCode, string traitModDomain)
        {
            FlavourText fallback = ProceduralFallback(traitCode);

            FlavourText tier1 = TryLoadModSupplied(api, traitModDomain, traitCode);
            if (tier1 != null) { FlavourText filled = FillGaps(tier1, fallback); return (filled.Title, filled.Blurb); }

            FlavourText tier2 = TryLoadCurated(api, traitCode);
            if (tier2 != null) { FlavourText filled = FillGaps(tier2, fallback); return (filled.Title, filled.Blurb); }

            return (fallback.Title, fallback.Blurb);
        }

        /// <summary>
        /// Public so StatBookRegistry can check this tier on its own, ahead of core's
        /// resolver when core is present -- see StatBookRegistry.RegisterBook.
        /// </summary>
        public static FlavourText TryLoadModSupplied(ICoreServerAPI api, string traitModDomain, string traitCode)
        {
            if (string.IsNullOrEmpty(traitModDomain)) { return null; }

            AssetLocation loc = new AssetLocation(traitModDomain, "config/skillbooksstats/" + traitCode + ".json");
            IAsset asset = api.Assets.TryGet(loc);
            return asset?.ToObject<FlavourText>();
        }

        private static FlavourText TryLoadCurated(ICoreServerAPI api, string traitCode)
        {
            if (curatedCache == null)
            {
                AssetLocation loc = new AssetLocation("skillbooksstats", "config/flavour-curated.json");
                IAsset asset = api.Assets.TryGet(loc);
                curatedCache = asset?.ToObject<Dictionary<string, FlavourText>>() ?? new Dictionary<string, FlavourText>();
            }

            return curatedCache.TryGetValue(traitCode, out FlavourText text) ? text : null;
        }

        private static FlavourText FillGaps(FlavourText text, FlavourText fallback)
        {
            return new FlavourText
            {
                Title = string.IsNullOrEmpty(text.Title) ? fallback.Title : text.Title,
                Blurb = string.IsNullOrEmpty(text.Blurb) ? fallback.Blurb : text.Blurb,
            };
        }

        private static FlavourText ProceduralFallback(string traitCode)
        {
            string traitName = Lang.Get("trait-" + traitCode);
            string traitDesc = Lang.GetIfExists("traitdesc-" + traitCode);

            return new FlavourText
            {
                Title = Lang.Get("skillbooksstats:fallback-title", traitName),
                Blurb = traitDesc != null
                    ? Lang.Get("skillbooksstats:fallback-blurb-withdesc", traitDesc)
                    : Lang.Get("skillbooksstats:fallback-blurb-nodesc"),
            };
        }
    }
}
