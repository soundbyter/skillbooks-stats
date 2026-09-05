using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace Skillbooks.Stats
{
    /// <summary>
    /// Trait-clearing, gated by role privilege level rather than a single named privilege --
    /// see core's SkillBookCommands for the full reasoning (decompile-confirmed default role
    /// levels, JIT/Incomplete-root explanation, etc.), mirrored exactly here:
    /// "/skillbooksstats cleartraits" (self) needs at least "crplayer"'s level (100);
    /// "player &lt;name&gt;" and "all" need at least "sumod"'s level (200); anything at or above
    /// either threshold, default or custom role, qualifies too. Skipped entirely when core is
    /// installed -- see SkillBooksStatsModSystem.AssetsFinalize -- since
    /// extraTraits/skillbooksLearnedTraits are shared, unprefixed watched attributes with no
    /// record of which mod granted which trait, so core's own identical
    /// "/skillbooks cleartraits" already covers books from both mods once core is present.
    /// Registering both would just be two commands doing the same thing. No reference to any
    /// Skillbooks.* type, so nothing here needs the JIT-safety isolation CONTRIBUTING.md calls
    /// out for core-touching code.
    /// </summary>
    public static class StatBookCommands
    {
        /// <summary>Default "crplayer" role's level -- see core's SkillBookCommands.</summary>
        private const int MinLevelClearSelf = 100;

        /// <summary>Default "sumod" role's level -- see core's SkillBookCommands.</summary>
        private const int MinLevelClearOthers = 200;

        public static void Register(ICoreServerAPI api)
        {
            CommandArgumentParsers parsers = api.ChatCommands.Parsers;

            api.ChatCommands.Create("skillbooksstats")
                .WithDescription("Skillbooks: Stats mod commands")
                .RequiresPrivilege(Privilege.chat)
                .BeginSubCommand("cleartraits")
                    .WithDescription("Clears traits you've learned from stat books, so they can be learned again. Requires at least the Creative Player role's privilege level (or a custom role of equivalent level).")
                    .RequiresPlayer()
                    .WithPreCondition(args => RequireMinLevel(api, args, MinLevelClearSelf))
                    .HandleWith(args => ClearOwn(api, args))
                    .BeginSubCommand("player")
                        .WithDescription("Clears traits an online player has learned from stat books. Requires at least the Survival Moderator role's privilege level.")
                        .WithPreCondition(args => RequireMinLevel(api, args, MinLevelClearOthers))
                        .WithArgs(parsers.Word("playername"))
                        .HandleWith(args => ClearOnePlayer(api, args))
                    .EndSubCommand()
                    .BeginSubCommand("all")
                        .WithDescription("Clears learned stat book traits for every online player. Requires at least the Survival Moderator role's privilege level.")
                        .WithPreCondition(args => RequireMinLevel(api, args, MinLevelClearOthers))
                        .HandleWith(args => ClearAll(api, args))
                    .EndSubCommand()
                .EndSubCommand()
            ;
        }

        private static TextCommandResult RequireMinLevel(ICoreServerAPI api, TextCommandCallingArgs args, int minLevel)
        {
            IPlayerRole role = args.Caller.GetRole(api);
            if (role != null && role.PrivilegeLevel >= minLevel) { return TextCommandResult.Success(); }
            return TextCommandResult.Error(Lang.Get("skillbooksstats:cmd-insufficient-role"));
        }

        private static TextCommandResult ClearOwn(ICoreServerAPI api, TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) { return TextCommandResult.Error(Lang.Get("skillbooksstats:cmd-no-entity")); }

            int cleared = ItemStatBook.ClearLearnedTraits(api, player.Entity);
            return TextCommandResult.Success(cleared > 0
                ? Lang.Get("skillbooksstats:cmd-cleared-self", cleared)
                : Lang.Get("skillbooksstats:cmd-cleared-none"));
        }

        private static TextCommandResult ClearOnePlayer(ICoreServerAPI api, TextCommandCallingArgs args)
        {
            string targetName = (string)args[0];
            IServerPlayer target = api.World.AllOnlinePlayers
                .OfType<IServerPlayer>()
                .FirstOrDefault(p => p.PlayerName.Equals(targetName, StringComparison.OrdinalIgnoreCase));

            if (target?.Entity == null)
            {
                return TextCommandResult.Error(Lang.Get("skillbooksstats:cmd-player-not-found", targetName));
            }

            int cleared = ItemStatBook.ClearLearnedTraits(api, target.Entity);
            return TextCommandResult.Success(cleared > 0
                ? Lang.Get("skillbooksstats:cmd-cleared-other", cleared, target.PlayerName)
                : Lang.Get("skillbooksstats:cmd-cleared-none-other", target.PlayerName));
        }

        private static TextCommandResult ClearAll(ICoreServerAPI api, TextCommandCallingArgs args)
        {
            int playersAffected = 0;
            int traitsCleared = 0;
            foreach (IPlayer player in api.World.AllOnlinePlayers)
            {
                if (player.Entity == null) { continue; }
                int cleared = ItemStatBook.ClearLearnedTraits(api, player.Entity);
                if (cleared == 0) { continue; }
                playersAffected++;
                traitsCleared += cleared;
            }

            return TextCommandResult.Success(Lang.Get("skillbooksstats:cmd-cleared-all", traitsCleared, playersAffected));
        }
    }
}
