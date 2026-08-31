using System;
using System.Reflection;
using Newtonsoft.Json.Linq;
using Skillbooks.Stats.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Skillbooks.Stats.Recipes
{
    /// <summary>
    /// Stat book + knife -> leather. Mirrors core's Recipes.SalvageRecipe exactly. Kept
    /// intentionally simple for reliability. Still built in code rather than shipped as
    /// static JSON so the leather amount stays config-driven and the recipe can be skipped
    /// entirely when disabled.
    /// </summary>
    public static class StatSalvageRecipe
    {
        public static void Register(ICoreServerAPI api, StatBooksConfig config)
        {
            if (!config.SalvageEnabled) { return; }

            GridRecipe recipe = Build(api, config);
            if (recipe == null) { return; }

            api.RegisterCraftingRecipe(recipe);

            // Wildcard ingredient matching doesn't consult item Attributes, so
            // SalvageIllegibleOnly can't be expressed declaratively. MatchesGridRecipe is the
            // veto hook for that; recipe identity is checked by reference, which stays stable
            // server-side, so this only ever targets this one recipe. (Grid-specific event
            // used deliberately rather than the newer generic MatchesRecipe: this recipe is
            // always a GridRecipe, and MatchesGridRecipe is confirmed present as far back as
            // 1.21, unlike the generic event.)
            if (config.SalvageIllegibleOnly)
            {
                api.Event.MatchesGridRecipe += (player, matchedRecipe, ingredients, gridWidth) =>
                {
                    if (!ReferenceEquals(matchedRecipe, recipe)) { return true; }
                    foreach (ItemSlot slot in ingredients)
                    {
                        if (slot?.Itemstack?.Collectible is Skillbooks.Stats.ItemStatBook book)
                        {
                            return book.IsIllegible;
                        }
                    }
                    return true;
                };
            }

            api.Logger.Notification("[Skillbooks: Stats] Salvage recipe registered.");
        }

        private static GridRecipe Build(ICoreServerAPI api, StatBooksConfig config)
        {
            JObject json = new JObject
            {
                ["ingredientPattern"] = "BK",
                ["width"] = 2,
                ["height"] = 1,
                ["shapeless"] = true,
                ["ingredients"] = new JObject
                {
                    ["B"] = new JObject { ["type"] = "item", ["code"] = "skillbooksstats:statbook-*" },
                    ["K"] = new JObject
                    {
                        ["type"] = "item",
                        ["code"] = "game:knife-*",
                        ["isTool"] = true,
                        ["toolDurabilityCost"] = 2,
                    },
                },
                ["output"] = new JObject
                {
                    ["type"] = "item",
                    ["code"] = "game:leather-normal-plain",
                    ["quantity"] = config.SalvageLeatherAmount,
                },
            };

            GridRecipe recipe = JsonUtil.ToObject<GridRecipe>(json, "skillbooksstats", null);
            // Our JSON never sets a "name", and neither JsonUtil.ToObject nor
            // Resolve/ResolveIngredients assigns one on every game version -- an unset Name
            // crashes GridRecipe.ToBytes (Name.ToShortString()) the moment the server syncs
            // recipes to a joining client.
            SetNameIfUnset(recipe, new AssetLocation("skillbooksstats", "salvage-book"));
            if (!ResolveRecipe(recipe, api.World))
            {
                api.Logger.Error("[Skillbooks: Stats] Failed to resolve salvage recipe.");
                return null;
            }

            return recipe;
        }

        /// <summary>
        /// GridRecipe.Name is a field on 1.21's VintagestoryAPI but a property on 1.22's
        /// (confirmed via decompiling both, same issue already fixed in core's
        /// Recipes.SalvageRecipe) -- identical C# source compiles to binary-incompatible IL
        /// either way, so this can't be a plain `recipe.Name ??= ...`. Reflecting by member
        /// name resolves against whichever representation is actually loaded at runtime,
        /// regardless of which version this assembly was compiled against.
        /// </summary>
        private static void SetNameIfUnset(GridRecipe recipe, AssetLocation name)
        {
            Type type = typeof(GridRecipe);
            PropertyInfo property = type.GetProperty("Name");
            if (property != null)
            {
                if (property.GetValue(recipe) == null) { property.SetValue(recipe, name); }
                return;
            }
            FieldInfo field = type.GetField("Name");
            if (field != null && field.GetValue(recipe) == null) { field.SetValue(recipe, name); }
        }

        /// <summary>
        /// 1.22's GridRecipe only has Resolve(world, source); 1.21's only has
        /// ResolveIngredients(world) -- confirmed via decompiling both, not just a rename
        /// guess. Tries the 1.22 shape first, falls back to 1.21's.
        /// </summary>
        private static bool ResolveRecipe(GridRecipe recipe, IWorldAccessor world)
        {
            Type type = typeof(GridRecipe);
            MethodInfo resolve = type.GetMethod("Resolve", new[] { typeof(IWorldAccessor), typeof(string) });
            if (resolve != null)
            {
                return (bool)resolve.Invoke(recipe, new object[] { world, "skillbooksstats salvage recipe" });
            }
            MethodInfo resolveIngredients = type.GetMethod("ResolveIngredients", new[] { typeof(IWorldAccessor) });
            if (resolveIngredients != null)
            {
                return (bool)resolveIngredients.Invoke(recipe, new object[] { world });
            }
            throw new MissingMethodException("GridRecipe has neither Resolve(world, source) nor ResolveIngredients(world).");
        }
    }
}
