using System.Collections.Generic;
using Newtonsoft.Json.Linq;
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

        public static void Generate(ICoreServerAPI api, Dictionary<string, DiscoveredStatTrait> statTraits, bool coreEnabled)
        {
            int i = 0;
            int registered = 0;
            foreach (KeyValuePair<string, DiscoveredStatTrait> entry in statTraits)
            {
                try
                {
                    RegisterBook(api, entry.Key, entry.Value, TintPool[i % TintPool.Length], coreEnabled);
                    registered++;
                }
                catch (System.Exception ex)
                {
                    api.Logger.Error($"[Skillbooks: Stats] Failed to register stat book for trait '{entry.Key}': {ex.Message}");
                }
                i++;
            }
            api.Logger.Event($"[Skillbooks: Stats] Registered {registered} of {statTraits.Count} stat book item(s)");
        }

        private static void RegisterBook(ICoreServerAPI api, string traitCode, DiscoveredStatTrait discovered, string tint, bool coreEnabled)
        {
            (string title, string blurb) = coreEnabled
                ? ResolveFlavourViaCore(api, traitCode, discovered)
                : StatBookFlavour.Resolve(api, traitCode, discovered.SourceDomain);

            Item item = api.ClassRegistry.CreateItem("ItemStatBook");
            item.Code = new AssetLocation("skillbooksstats", "statbook-" + traitCode);
            item.MaxStackSize = 16;
            item.Shape = new CompositeShape { Base = SharedShape };
            item.Textures["cover"] = new CompositeTexture(new AssetLocation("game", "item/lore/" + tint));
            item.CreativeInventoryTabs = new[] { "skillbooksstats" };

            item.Attributes = new JsonObject(new JObject
            {
                ["skillbooksstats:traitCode"] = traitCode,
                ["skillbooksstats:title"] = title,
                ["skillbooksstats:blurb"] = blurb,
                ["handbook"] = new JObject { ["exclude"] = true },
            });

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

            api.RegisterItem(item);
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
