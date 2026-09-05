using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Skillbooks.Stats
{
    /// <summary>
    /// Resolves a trait's SourceDomain (see DiscoveredStatTrait) into a player-facing "where did
    /// this come from" label for the book's tooltip -- mirrors core's SkillBookSourceMod, kept
    /// as a separate self-contained copy the same way the rest of Stats stays standalone-
    /// capable. See core's copy for why "game"/"survival"/"creative" are special-cased to
    /// "Vintage Story" rather than their own internal content-pack names.
    /// </summary>
    public static class StatBookSourceMod
    {
        private static readonly HashSet<string> VanillaDomains = new HashSet<string> { "game", "survival", "creative" };

        public static string Resolve(ICoreAPI api, string domain)
        {
            if (string.IsNullOrEmpty(domain)) { return null; }
            if (VanillaDomains.Contains(domain)) { return Lang.Get("skillbooksstats:source-vanilla"); }
            return api.ModLoader.GetMod(domain)?.Info?.Name ?? domain;
        }
    }
}
