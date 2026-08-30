using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Skillbooks.Stats
{
    /// <summary>
    /// Hold right-click ~2s to permanently grant the stat trait named in this item's own
    /// "skillbooksstats:traitCode" attribute. A self-contained sibling of core's
    /// ItemSkillBook rather than a shared class -- reusing core's class directly would leave
    /// Stats with no working item in standalone mode, since core isn't even loadable then.
    /// Always forces a stat recompute after granting: a trait's Attributes only take effect
    /// through CharacterSystem.applyTraitAttributes, which nothing calls automatically when
    /// extraTraits changes.
    /// </summary>
    public class ItemStatBook : Item
    {
        private const float SecondsToRead = 2f;

        private IProgressBar progressBar;

        public string TraitCode => Attributes?["skillbooksstats:traitCode"].AsString();

        public override string GetHeldItemName(ItemStack itemStack)
        {
            return Attributes?["skillbooksstats:title"].AsString() ?? base.GetHeldItemName(itemStack);
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            string blurb = Attributes?["skillbooksstats:blurb"].AsString();
            if (!string.IsNullOrEmpty(blurb))
            {
                dsc.AppendLine(blurb);
            }
            AppendTraitSummary(dsc);
        }

        /// <summary>
        /// The flavour text alone never says which trait this actually is or what it does --
        /// this appends that mechanical info below it.
        /// </summary>
        private void AppendTraitSummary(StringBuilder dsc)
        {
            string traitCode = TraitCode;
            if (string.IsNullOrEmpty(traitCode)) { return; }

            dsc.AppendLine();
            dsc.AppendLine(Lang.Get("skillbooksstats:tooltip-grants", Lang.Get("trait-" + traitCode)));

            string traitDesc = Lang.GetIfExists("traitdesc-" + traitCode);
            if (!string.IsNullOrEmpty(traitDesc))
            {
                dsc.AppendLine(traitDesc);
            }

            string attrText = TraitAttributeFormatter.Format(Attributes?["skillbooksstats:attributes"]);
            if (!string.IsNullOrEmpty(attrText))
            {
                dsc.AppendLine(attrText);
            }
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (byEntity.Controls.ShiftKey) { return; }
            handHandling = EnumHandHandling.PreventDefault;

            if (api is ICoreClientAPI capi)
            {
                ModSystemProgressBar progressBarSystem = capi.ModLoader.GetModSystem<ModSystemProgressBar>();
                progressBarSystem.RemoveProgressbar(progressBar);
                progressBar = progressBarSystem.AddProgressbar();
            }
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (progressBar != null)
            {
                progressBar.Progress = secondsUsed / SecondsToRead;
            }
            return secondsUsed < SecondsToRead;
        }

        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            RemoveProgressBar();
            return true;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            RemoveProgressBar();

            if (secondsUsed < SecondsToRead - 0.1f) { return; }
            if (byEntity.World.Side != EnumAppSide.Server) { return; }

            string traitCode = TraitCode;
            if (string.IsNullOrEmpty(traitCode)) { return; }

            IPlayer player = (byEntity as EntityPlayer)?.Player;
            if (player == null) { return; }

            ICoreAPI resolvedApi = api;

            string[] extraTraits = byEntity.WatchedAttributes.GetStringArray("extraTraits", System.Array.Empty<string>());
            if (extraTraits.Contains(traitCode))
            {
                (player as IServerPlayer)?.SendMessage(GlobalConstants.CurrentChatGroup, Lang.Get("skillbooksstats:msg-alreadyknown"), EnumChatType.Notification);
                return;
            }

            byEntity.WatchedAttributes.SetStringArray("extraTraits", extraTraits.Append(traitCode).ToArray());
            byEntity.WatchedAttributes.MarkPathDirty("extraTraits");
            RecordLearnedTrait(byEntity, traitCode);
            RefreshTraitStats(resolvedApi, byEntity);

            slot.TakeOut(1);
            slot.MarkDirty();

            (player as IServerPlayer)?.SendMessage(GlobalConstants.CurrentChatGroup, Lang.Get("skillbooksstats:msg-traitlearned", Lang.Get("trait-" + traitCode)), EnumChatType.Notification);
        }

        /// <summary>
        /// extraTraits is a generic vanilla extension point (CharacterSystem only ever reads
        /// it) that any mod can add codes to -- race selection and other mods use it too, so
        /// it can't double as "traits granted specifically by a stat book" for display
        /// purposes. Tracked separately here, under an unprefixed key shared with core the
        /// same way extraTraits itself is shared, so a "Learned Traits" tab can show only
        /// what was actually read from a book.
        /// </summary>
        private static void RecordLearnedTrait(EntityAgent byEntity, string traitCode)
        {
            string[] learned = byEntity.WatchedAttributes.GetStringArray("skillbooksLearnedTraits", System.Array.Empty<string>());
            if (learned.Contains(traitCode)) { return; }

            byEntity.WatchedAttributes.SetStringArray("skillbooksLearnedTraits", learned.Append(traitCode).ToArray());
            byEntity.WatchedAttributes.MarkPathDirty("skillbooksLearnedTraits");
        }

        /// <summary>
        /// Internal rather than private: StatBookCharSelPatcher reuses this after restoring
        /// extraTraits post-charsel, same need for the same reason.
        /// </summary>
        internal static void RefreshTraitStats(ICoreAPI api, EntityAgent byEntity)
        {
            if (byEntity is not EntityPlayer entityPlayer) { return; }
            string currentClassCode = byEntity.WatchedAttributes.GetString("characterClass");
            if (string.IsNullOrEmpty(currentClassCode)) { return; }

            try
            {
                CharacterSystem characterSystem = api.ModLoader.GetModSystem<CharacterSystem>();
                characterSystem?.setCharacterClass(entityPlayer, currentClassCode, initializeGear: false);
            }
            catch (System.Exception ex)
            {
                api.Logger.Warning($"[Skillbooks: Stats] Failed to refresh trait stats after granting a trait: {ex.Message}");
            }
        }

        private void RemoveProgressBar()
        {
            if (api is ICoreClientAPI capi && progressBar != null)
            {
                capi.ModLoader.GetModSystem<ModSystemProgressBar>().RemoveProgressbar(progressBar);
                progressBar = null;
            }
        }
    }
}
