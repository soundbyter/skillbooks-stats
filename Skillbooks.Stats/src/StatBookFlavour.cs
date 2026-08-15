using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace Skillbooks.Stats
{
    /// <summary>
    /// Self-contained three-tier flavour resolver for standalone mode: mod-supplied
    /// (assets/&lt;domain&gt;/skillbooksstats/&lt;code&gt;.json), curated
    /// (assets/skillbooksstats/config/flavour-curated.json), then a procedural fallback.
    /// Zero reference to any Skillbooks.* type -- only runs when core is absent; when it's
    /// present, StatBookRegistry calls into core's resolver instead.
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

        private static FlavourText TryLoadModSupplied(ICoreServerAPI api, string traitModDomain, string traitCode)
        {
            if (string.IsNullOrEmpty(traitModDomain)) { return null; }

            AssetLocation loc = new AssetLocation(traitModDomain, "skillbooksstats/" + traitCode + ".json");
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
