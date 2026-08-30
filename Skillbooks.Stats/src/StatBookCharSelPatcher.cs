using System.Collections.Generic;
using Skillbooks.Stats.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace Skillbooks.Stats
{
    /// <summary>
    /// Optional persistence for skillbook-granted traits across the admin "charsel" command --
    /// same mechanism and reasoning as core's copy of this class (see there). Self-contained
    /// sibling rather than a shared class, matching how the rest of Stats stays standalone-
    /// capable. Skipped entirely when core is loaded: extraTraits/skillbooksLearnedTraits are
    /// shared, unprefixed watched attributes with no record of which mod granted which trait,
    /// so "restore" isn't something that can be split per-mod -- core's own copy, and its own
    /// config setting, already covers traits from both mods when both are installed.
    /// </summary>
    public static class StatBookCharSelPatcher
    {
        public static void Register(ICoreServerAPI api, StatBooksConfig config)
        {
            if (api.ModLoader.IsModEnabled("skillbooks")) { return; }
            if (!config.KeepTraitsOnCharSel) { return; }

            Dictionary<string, string> lastKnownClass = new Dictionary<string, string>();

            api.Event.RegisterGameTickListener(_ =>
            {
                foreach (IPlayer player in api.World.AllOnlinePlayers)
                {
                    EntityPlayer entity = player.Entity;
                    if (entity == null) { continue; }

                    string currentClass = entity.WatchedAttributes.GetString("characterClass");
                    if (string.IsNullOrEmpty(currentClass)) { continue; }

                    bool seenBefore = lastKnownClass.TryGetValue(player.PlayerUID, out string previousClass);
                    lastKnownClass[player.PlayerUID] = currentClass;

                    if (!seenBefore || currentClass == previousClass) { continue; }

                    RestoreLearnedTraits(api, entity);
                }
            }, 2000);

            api.Logger.Notification("[Skillbooks: Stats] Charsel trait persistence armed.");
        }

        private static void RestoreLearnedTraits(ICoreServerAPI api, EntityPlayer entity)
        {
            string[] learned = entity.WatchedAttributes.GetStringArray("skillbooksLearnedTraits", System.Array.Empty<string>());
            if (learned.Length == 0) { return; }

            string[] active = entity.WatchedAttributes.GetStringArray("extraTraits", System.Array.Empty<string>());
            HashSet<string> activeSet = new HashSet<string>(active);
            List<string> restored = new List<string>(active);

            bool changed = false;
            foreach (string code in learned)
            {
                if (activeSet.Add(code))
                {
                    restored.Add(code);
                    changed = true;
                }
            }
            if (!changed) { return; }

            entity.WatchedAttributes.SetStringArray("extraTraits", restored.ToArray());
            entity.WatchedAttributes.MarkPathDirty("extraTraits");
            ItemStatBook.RefreshTraitStats(api, entity);
        }
    }
}
