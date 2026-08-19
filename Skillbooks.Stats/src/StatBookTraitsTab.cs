using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace Skillbooks.Stats
{
    /// <summary>
    /// Adds a genuine "Learned Traits" tab to the vanilla character dialog, alongside
    /// "Character" and "Traits" -- same mechanism as core's copy of this class (see there
    /// for why: charDlg.Tabs/.RenderTabHandlers are plain mutable lists, the same way
    /// CharacterSystem itself adds the "Traits" tab). Self-contained sibling rather than a
    /// shared class, matching how the rest of Stats stays standalone-capable. Skipped
    /// entirely when core is loaded -- core's own tab already covers traits from both mods,
    /// since they share the same "skillbooksLearnedTraits" watched attribute (see
    /// ItemStatBook.RecordLearnedTrait -- not "extraTraits" itself, which is a generic
    /// vanilla extension point other mods write to as well, e.g. race selection).
    ///
    /// Coexistence with other tab-adding mods: see core's SkillBookTraitsTab class remarks
    /// -- safe against any mod that computes its own DataInt from live list state, not
    /// defensible against one that hardcodes a value not matching its own eventual position
    /// (DataInt is a literal RenderTabHandlers index, not just an identifier to keep unique).
    /// </summary>
    public class StatBookTraitsTab : ModSystem
    {
        private const string TextKey = "skillbookslearnedtraits-text";
        private const string ScrollbarKey = "skillbookslearnedtraits-scroll";
        private const float ViewportHeight = 200f;

        private ICoreClientAPI capi;
        private GuiDialogCharacterBase dlg;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Client;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            if (api.ModLoader.IsModEnabled("skillbooks")) { return; }

            capi = api;

            // Deferred to LevelFinalize -- see core's SkillBookTraitsTab.StartClientSide for
            // why: GuiTab clicks resolve via GuiTab.DataInt, not list position, and
            // CharacterSystem hardcodes DataInt=1 for its own "Traits" tab. Registering here
            // directly can race CharacterSystem's own StartClientSide and collide with it.
            api.Event.LevelFinalize += RegisterTab;
        }

        private void RegisterTab()
        {
            dlg = capi.Gui.LoadedGuis.Find(d => d is GuiDialogCharacterBase) as GuiDialogCharacterBase;
            if (dlg == null) { return; }

            // DataInt must equal the index our handler will occupy in RenderTabHandlers --
            // see core's SkillBookTraitsTab.RegisterTab for the full explanation.
            dlg.Tabs.Add(new GuiTab
            {
                Name = Lang.Get("skillbooksstats:learnedtraits-title"),
                DataInt = dlg.Tabs.Count,
            });
            dlg.RenderTabHandlers.Add(ComposeTab);
        }

        private void ComposeTab(GuiComposer compo)
        {
            ElementBounds textBounds = ElementBounds.Fixed(0, 25, 365, ViewportHeight);
            ElementBounds clippingBounds = textBounds.ForkBoundingParent();
            ElementBounds insetBounds = textBounds.FlatCopy().FixedGrow(6).WithFixedOffset(-3, -3);
            ElementBounds scrollbarBounds = insetBounds.CopyOffsetedSibling(textBounds.fixedWidth + 7).WithFixedWidth(20);

            RichTextComponentBase[] comps = VtmlUtil.Richtextify(capi, BuildText(), CairoFont.WhiteDetailText().WithLineHeightMultiplier(1.15));

            compo
                .BeginClip(clippingBounds)
                    .AddInset(insetBounds, 3)
                    .AddRichtext(comps, textBounds, TextKey)
                .EndClip()
                .AddVerticalScrollbar(OnNewScrollbarValue, scrollbarBounds, ScrollbarKey)
            ;

            // Deferred one tick -- the caller invokes this handler before its own Compose()
            // call, so the richtext element's real height isn't known yet here.
            capi.Event.EnqueueMainThreadTask(FixScrollbarHeight, "skillbookslearnedtraits-fixscroll");
        }

        private void FixScrollbarHeight()
        {
            GuiComposer compo = dlg?.Composers["playercharacter"];
            GuiElementRichtext richtext = compo?.GetRichtext(TextKey);
            GuiElementScrollbar scrollbar = compo?.GetScrollbar(ScrollbarKey);
            if (richtext == null || scrollbar == null) { return; }

            scrollbar.SetHeights(ViewportHeight, (float)richtext.Bounds.fixedHeight);
        }

        private void OnNewScrollbarValue(float value)
        {
            GuiElementRichtext richtext = dlg.Composers["playercharacter"]?.GetRichtext(TextKey);
            if (richtext == null) { return; }
            richtext.Bounds.fixedY = 3 - value;
            richtext.Bounds.CalcWorldBounds();
        }

        /// <summary>
        /// Stat bonuses need each trait's raw Attributes, which skillbooksLearnedTraits
        /// doesn't carry on its own -- reloaded from config/traits.json the same way
        /// StatTraitDiscovery discovers traits server-side, since IAssetManager.GetMany is
        /// equally available client-side.
        /// </summary>
        private string BuildText()
        {
            string[] learnedCodes = capi.World.Player.Entity.WatchedAttributes.GetStringArray("skillbooksLearnedTraits", Array.Empty<string>());
            if (learnedCodes.Length == 0) { return Lang.Get("skillbooksstats:learnedtraits-empty"); }

            Dictionary<string, Dictionary<string, double>> attributesByCode = LoadTraitAttributes(capi);

            StringBuilder text = new StringBuilder();
            foreach (string code in learnedCodes)
            {
                text.AppendLine(Lang.Get("trait-" + code));

                string traitDesc = Lang.GetIfExists("traitdesc-" + code);
                if (!string.IsNullOrEmpty(traitDesc))
                {
                    text.AppendLine(traitDesc);
                }

                if (attributesByCode.TryGetValue(code, out Dictionary<string, double> attributes))
                {
                    string attrText = TraitAttributeFormatter.Format(new JsonObject(JObject.FromObject(attributes)));
                    if (!string.IsNullOrEmpty(attrText))
                    {
                        text.AppendLine(attrText);
                    }
                }

                text.AppendLine();
            }

            return text.ToString().TrimEnd();
        }

        private static Dictionary<string, Dictionary<string, double>> LoadTraitAttributes(ICoreClientAPI capi)
        {
            Dictionary<string, Dictionary<string, double>> attributesByCode = new Dictionary<string, Dictionary<string, double>>();
            Dictionary<AssetLocation, JToken> many = capi.Assets.GetMany<JToken>(capi.Logger, "config/traits", null);

            foreach (var (loc, token) in many)
            {
                if (token is JObject) { AddTrait(attributesByCode, JsonUtil.ToObject<Trait>(token, loc.Domain, null)); }
                else if (token is JArray array)
                {
                    foreach (JToken entry in array) { AddTrait(attributesByCode, JsonUtil.ToObject<Trait>(entry, loc.Domain, null)); }
                }
            }

            return attributesByCode;
        }

        private static void AddTrait(Dictionary<string, Dictionary<string, double>> attributesByCode, Trait trait)
        {
            if (trait?.Code == null || trait.Attributes is not { Count: > 0 }) { return; }
            attributesByCode[trait.Code] = trait.Attributes;
        }
    }
}
