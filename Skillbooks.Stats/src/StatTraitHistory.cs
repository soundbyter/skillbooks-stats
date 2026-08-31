using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Server;

namespace Skillbooks.Stats
{
    /// <summary>
    /// Persists every stat-trait code ever discovered in this savegame, so StatBookRegistry
    /// can keep registering an illegible item for a trait whose providing mod has since been
    /// removed instead of the code just disappearing. Stored via ISaveGame.GetData/StoreData,
    /// under its own key so it doesn't collide with core's own TraitHistory when both mods
    /// share a savegame.
    /// </summary>
    public static class StatTraitHistory
    {
        private const string DataKey = "skillbooksstats:knownTraitCodes";

        public static HashSet<string> LoadAndUpdate(ICoreServerAPI api, IEnumerable<string> currentTraitCodes)
        {
            string[] stored = api.WorldManager.SaveGame.GetData(DataKey, System.Array.Empty<string>());
            HashSet<string> known = new HashSet<string>(stored);
            foreach (string code in currentTraitCodes) { known.Add(code); }

            api.WorldManager.SaveGame.StoreData(DataKey, known.ToArray());
            return known;
        }
    }
}
