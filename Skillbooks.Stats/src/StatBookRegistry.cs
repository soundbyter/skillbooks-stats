using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Skillbooks.Stats.Config;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Skillbooks.Stats
{
    /// <summary>
    /// Builds one ItemStatBook per discovered stat trait, same RegisterItem approach as
    /// core's SkillBookRegistry. Reuses core's flavour resolver when core is present (see
    /// CONTRIBUTING.md for why that call must stay isolated in its own method), falling back
    /// to StatBookFlavour's own self-contained resolver otherwise.
    ///
    /// Also registers a fallback "illegible" item for every trait in knownTraitCodes whose
    /// providing mod is no longer loaded -- otherwise an existing itemstack for it would
    /// collapse into the engine's generic "unknown item" placeholder. Same ItemStatBook class
    /// either way; the "skillbooksstats:illegible" attribute set here picks the mode. Mirrors
    /// core's SkillBookRegistry.
    /// </summary>
    public static class StatBookRegistry
    {
        private static readonly AssetLocation SharedShape = new AssetLocation("game", "block/clutter/bookshelves/small-normal");

        // A different vanilla "item/lore/" tint family than core's own TintPool ("aged-*"), so
        // stat books read as visually distinct from crafting-trait books at a glance.
        private static readonly string[] TintPool =
        {
            "normal-brickred", "normal-brown", "normal-burgundy", "normal-cherryred", "normal-darkbeige",
            "normal-darkgray", "normal-darkgreen", "normal-darkolive", "normal-gray", "normal-lightbrown",
            "normal-olive", "normal-orange", "normal-orangebrown", "normal-purple", "normal-purpleorange",
            "normal-slateblue", "normal-teal",
        };

        // Deliberately not in TintPool -- illegible books get their own distinct, worn look
        // rather than reusing a normal tint for aesthetics and for clarity at a glance.
        private const string IllegibleTexturePath = "item/lore/book-rotten1";

        public static void Generate(ICoreServerAPI api, Dictionary<string, DiscoveredStatTrait> statTraits, IEnumerable<string> knownTraitCodes, StatBooksConfig config, bool coreEnabled)
        {
            int i = 0;
            int registered = 0;
            foreach (KeyValuePair<string, DiscoveredStatTrait> entry in statTraits)
            {
                try
                {
                    RegisterBook(api, entry.Key, entry.Value, TintPool[i % TintPool.Length], config, coreEnabled);
                    registered++;
                }
                catch (System.Exception ex)
                {
                    api.Logger.Error($"[Skillbooks: Stats] Failed to register stat book for trait '{entry.Key}': {ex.Message}");
                }
                i++;
            }
            api.Logger.Event($"[Skillbooks: Stats] Registered {registered} of {statTraits.Count} stat book item(s)");

            int orphanedTotal = 0;
            int orphanedRegistered = 0;
            foreach (string traitCode in knownTraitCodes)
            {
                if (statTraits.ContainsKey(traitCode)) { continue; }
                // A blacklisted/not-allowlisted trait is a deliberate exclusion, not an orphan.
                if (!config.IsTraitEnabled(traitCode)) { continue; }
                orphanedTotal++;
                try
                {
                    RegisterIllegibleBook(api, traitCode, config);
                    orphanedRegistered++;
                }
                catch (System.Exception ex)
                {
                    api.Logger.Error($"[Skillbooks: Stats] Failed to register illegible stat book for orphaned trait '{traitCode}': {ex.Message}");
                }
            }
            if (orphanedTotal > 0)
            {
                api.Logger.Event($"[Skillbooks: Stats] Registered {orphanedRegistered} of {orphanedTotal} illegible stat book(s) for orphaned trait(s) (providing mod no longer loaded)");
            }
        }

        private static void RegisterBook(ICoreServerAPI api, string traitCode, DiscoveredStatTrait discovered, string tint, StatBooksConfig config, bool coreEnabled)
        {
            (string title, string blurb) = ResolveFlavourWithOverride(api, traitCode, discovered, config, coreEnabled);

            Item item = BuildBaseItem(api, traitCode, "item/lore/" + tint);
            JObject attributes = new JObject
            {
                ["skillbooksstats:traitCode"] = traitCode,
                ["skillbooksstats:title"] = title,
                ["skillbooksstats:blurb"] = blurb,
                ["skillbooksstats:attributes"] = discovered.Trait.Attributes is { Count: > 0 }
                    ? JObject.FromObject(discovered.Trait.Attributes)
                    : null,
                ["skillbooksstats:sourceMod"] = StatBookSourceMod.Resolve(api, discovered.SourceDomain),
            };
            if (config.HideFromHandbook) { attributes["handbook"] = new JObject { ["exclude"] = true }; }
            item.Attributes = new JsonObject(attributes);

            api.RegisterItem(item);
        }

        private static void RegisterIllegibleBook(ICoreServerAPI api, string traitCode, StatBooksConfig config)
        {
            Item item = BuildBaseItem(api, traitCode, IllegibleTexturePath);
            JObject attributes = new JObject
            {
                ["skillbooksstats:traitCode"] = traitCode,
                ["skillbooksstats:illegible"] = true,
            };
            if (config.HideFromHandbook) { attributes["handbook"] = new JObject { ["exclude"] = true }; }
            item.Attributes = new JsonObject(attributes);

            api.RegisterItem(item);
        }

        private static Item BuildBaseItem(ICoreServerAPI api, string traitCode, string texturePath)
        {
            Item item = api.ClassRegistry.CreateItem(ItemStatBook.ClassName);
            item.Code = new AssetLocation("skillbooksstats", "statbook-" + traitCode);
            item.MaxStackSize = 16;
            item.Shape = new CompositeShape { Base = SharedShape };
            item.Textures["cover"] = new CompositeTexture(new AssetLocation("game", texturePath));
            item.CreativeInventoryTabs = new[] { "skillbooksstats" };

            item.GuiTransform = new ModelTransform
            {
                Translation = new FastVec3f(0f, 0f, 0f),
                Rotation = new FastVec3f(-180f, 123f, 33f),
                Origin = new FastVec3f(0.48f, 0.21f, 0.5f),
                ScaleXYZ = new FastVec3f(-3.23f, 3.23f, 3.23f),
            };
            item.TpHandTransform = new ModelTransform
            {
                Translation = new FastVec3f(-0.79f, -0.36f, -0.73f),
                Rotation = new FastVec3f(0f, -84f, 7f),
                Origin = new FastVec3f(0.5f, 0.1f, 0.5f),
                Scale = 0.67f,
            };
            item.GroundTransform = new ModelTransform
            {
                Translation = new FastVec3f(0f, 0f, 0f),
                Rotation = new FastVec3f(0f, 0f, 90f),
                Origin = new FastVec3f(0.41f, 0f, 0.5f),
                Scale = 3.4f,
            };

            return item;
        }

        /// <summary>
        /// A FlavourOverrides entry overrides everything else, for that trait code -- checked
        /// here, above ResolveFlavour's own chain. With core installed, this latches onto
        /// core's own skillbooks.json FlavourOverrides instead of Stats' own
        /// skillbooksstats.json copy: one config file for an admin to manage overrides in
        /// regardless of which mod actually owns a given trait code, rather than two
        /// independent override lists that could quietly drift or duplicate each other.
        /// Stats' own FlavourOverrides only ever applies in standalone mode.
        /// </summary>
        private static (string title, string blurb) ResolveFlavourWithOverride(ICoreServerAPI api, string traitCode, DiscoveredStatTrait discovered, StatBooksConfig config, bool coreEnabled)
        {
            (string title, string blurb) resolved = ResolveFlavour(api, traitCode, discovered, coreEnabled);

            (string title, string blurb)? over = coreEnabled
                ? TryGetCoreFlavourOverride(api, traitCode)
                : TryGetOwnFlavourOverride(config, traitCode);
            if (over == null) { return resolved; }

            StatBookFlavour.FlavourText filled = StatBookFlavour.FillGaps(
                new StatBookFlavour.FlavourText { Title = over.Value.title, Blurb = over.Value.blurb },
                new StatBookFlavour.FlavourText { Title = resolved.title, Blurb = resolved.blurb });
            return (filled.Title, filled.Blurb);
        }

        private static (string title, string blurb)? TryGetOwnFlavourOverride(StatBooksConfig config, string traitCode)
        {
            if (!config.FlavourOverrides.TryGetValue(traitCode, out StatBooksConfig.FlavourOverride over)) { return null; }
            return (over.Title, over.Blurb);
        }

        /// <summary>
        /// Isolated in its own method, only called after the caller has already confirmed core
        /// is installed -- same JIT-safety reasoning as ResolveFlavourViaCore below: a
        /// Skillbooks.* type reference here would crash mod loading with core absent if this
        /// weren't split out from the method that also runs in standalone mode.
        ///
        /// Loads core's config directly (the same static loader core's own AssetsFinalize
        /// uses) rather than reading it off core's live SkillBooksModSystem instance --
        /// confirmed via a real crash report that mod load order isn't guaranteed to run
        /// core's AssetsFinalize before Stats' own: with a large enough mod list, the
        /// dependency-topology sort can place "skillbooksstats" before "skillbooks" even
        /// though nothing here declares a hard dependency forcing otherwise (deliberately not
        /// declared, since Stats needs to load and run fine with core entirely absent). When
        /// that happens, core.Config is still null (only ever set inside core's own
        /// AssetsFinalize, no default), and core.Config.FlavourOverrides threw
        /// NullReferenceException for every single trait. Loading the config file directly
        /// sidesteps that race entirely -- it's a cheap, side-effect-free file read no matter
        /// how many times it's called, and core will happily load the same file again itself.
        /// </summary>
        private static (string title, string blurb)? TryGetCoreFlavourOverride(ICoreServerAPI api, string traitCode)
        {
            Skillbooks.Config.SkillBooksConfig coreConfig = Skillbooks.Config.SkillBooksConfig.Load(api);
            if (!coreConfig.FlavourOverrides.TryGetValue(traitCode, out Skillbooks.Config.SkillBooksConfig.FlavourOverride over)) { return null; }
            return (over.Title, over.Blurb);
        }

        /// <summary>
        /// StatBookFlavour.Resolve (the standalone path) already checks Stats' own
        /// skillbooksstats/ override as its first tier. ResolveFlavourViaCore does not -- it
        /// hands off to core's resolver entirely, which only knows about core's own
        /// skillbooks/ path. So with core present, this checks skillbooksstats/ explicitly
        /// first and only falls through to core's resolver for whatever it doesn't cover,
        /// rather than skipping Stats' own override tier just because core happens to be
        /// installed.
        /// </summary>
        private static (string title, string blurb) ResolveFlavour(ICoreServerAPI api, string traitCode, DiscoveredStatTrait discovered, bool coreEnabled)
        {
            if (!coreEnabled)
            {
                return StatBookFlavour.Resolve(api, traitCode, discovered.SourceDomain);
            }

            (string title, string blurb) viaCore = ResolveFlavourViaCore(api, traitCode, discovered);

            StatBookFlavour.FlavourText ownOverride = StatBookFlavour.TryLoadModSupplied(api, discovered.SourceDomain, traitCode);
            if (ownOverride == null) { return viaCore; }

            return (
                string.IsNullOrEmpty(ownOverride.Title) ? viaCore.title : ownOverride.Title,
                string.IsNullOrEmpty(ownOverride.Blurb) ? viaCore.blurb : ownOverride.Blurb
            );
        }

        /// <summary>
        /// Isolated in its own method, only called after the caller has already confirmed core
        /// is installed -- referencing Skillbooks.* types here is what would crash mod loading
        /// if this ran with core absent.
        /// </summary>
        private static (string title, string blurb) ResolveFlavourViaCore(ICoreServerAPI api, string traitCode, DiscoveredStatTrait discovered)
        {
            Skillbooks.DiscoveredTrait coreDiscovered = new Skillbooks.DiscoveredTrait
            {
                Trait = discovered.Trait,
                SourceDomain = discovered.SourceDomain,
            };
            Skillbooks.SkillBookFlavour.FlavourText flavour = Skillbooks.SkillBookFlavour.Resolve(api, traitCode, coreDiscovered);
            return (flavour.Title, flavour.Blurb);
        }
    }
}
