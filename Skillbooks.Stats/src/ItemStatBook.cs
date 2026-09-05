using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Skillbooks.Stats
{
    /// <summary>
    /// Hold right-click ~2s to permanently grant the stat trait named in this item's own
    /// "skillbooksstats:traitCode" attribute. Holding a temporal gear in the offhand while
    /// reading switches the same interaction into a reroll instead (fixed 1-gear cost, since
    /// the offhand only ever holds one item) -- mirrors core's ItemSkillBook. A self-contained
    /// sibling rather than a shared class -- reusing core's class directly would leave Stats
    /// with no working item in standalone mode, since core isn't even loadable then. Always
    /// forces a stat recompute after granting: a trait's Attributes only take effect through
    /// CharacterSystem.applyTraitAttributes, which nothing calls automatically when
    /// extraTraits changes.
    /// </summary>
    public class ItemStatBook : Item
    {
        /// <summary>
        /// Namespaced class-registration key, same reasoning as core's ItemSkillBook.ClassName
        /// -- ClassRegistry.RegisterItemClass/CreateItem share one flat global
        /// Dictionary&lt;string, Type&gt; with no collision protection (confirmed via decompile:
        /// RegisterItemClass is just `ItemClassToTypeMapping[itemClass] = item;`). Not a
        /// confirmed collision like core's "ItemSkillBook" was against XLib, but "ItemStatBook"
        /// is just as generic a name, so it gets the same defensive treatment.
        /// </summary>
        public const string ClassName = "SkillbooksStatsItemStatBook";

        private const float SecondsToRead = 2f;
        private const float SecondsToReroll = 3.5f;

        private static readonly AssetLocation TemporalGearCode = new AssetLocation("game", "gear-temporal");

        private IProgressBar progressBar;

        public string TraitCode => Attributes?["skillbooksstats:traitCode"].AsString();

        /// <summary>
        /// Set when this book's trait code is no longer discovered (its providing mod was
        /// removed). Shows fixed flavour text, grants nothing, and isn't consumed on read --
        /// but can still be rerolled into a fresh valid book.
        /// </summary>
        public bool IsIllegible => Attributes?["skillbooksstats:illegible"].AsBool(false) ?? false;

        public override string GetHeldItemName(ItemStack itemStack)
        {
            if (IsIllegible) { return Lang.Get("skillbooksstats:item-illegible-statbook"); }
            return Attributes?["skillbooksstats:title"].AsString() ?? base.GetHeldItemName(itemStack);
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            if (IsIllegible)
            {
                dsc.AppendLine(Lang.Get("skillbooksstats:illegible-blurb"));
            }
            else
            {
                string blurb = Attributes?["skillbooksstats:blurb"].AsString();
                if (!string.IsNullOrEmpty(blurb))
                {
                    dsc.AppendLine(blurb);
                }
                AppendTraitSummary(dsc);
            }

            // The handbook's search index is built from an item's name plus its full
            // GetDescription() output (see core's ItemSkillBook.GetHeldItemInfo for the
            // decompile-confirmed mechanism) -- this line's only purpose is making these books
            // findable by searching "skillbooks" there.
            dsc.AppendLine(Lang.Get("skillbooksstats:tooltip-series"));
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

            string sourceMod = Attributes?["skillbooksstats:sourceMod"].AsString();
            if (!string.IsNullOrEmpty(sourceMod))
            {
                dsc.AppendLine(Lang.Get("skillbooksstats:tooltip-source", sourceMod));
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
            bool rerolling = HasOffhandGear(byEntity);
            float duration = rerolling ? SecondsToReroll : SecondsToRead;

            if (progressBar != null)
            {
                progressBar.Progress = secondsUsed / duration;
            }

            if (rerolling && byEntity.World is IClientWorldAccessor clientWorld)
            {
                clientWorld.AddCameraShake(0.035f);
            }

            return secondsUsed < duration;
        }

        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            RemoveProgressBar();
            return true;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            RemoveProgressBar();

            float completionDuration = HasOffhandGear(byEntity) ? SecondsToReroll : SecondsToRead;
            if (secondsUsed < completionDuration - 0.1f) { return; }
            if (byEntity.World.Side != EnumAppSide.Server) { return; }

            string traitCode = TraitCode;
            if (string.IsNullOrEmpty(traitCode)) { return; }

            IPlayer player = (byEntity as EntityPlayer)?.Player;
            if (player == null) { return; }

            ICoreAPI resolvedApi = api;
            SkillBooksStatsModSystem modSystem = resolvedApi.ModLoader.GetModSystem<SkillBooksStatsModSystem>();
            ItemSlot offhandSlot = byEntity.LeftHandItemSlot;
            bool illegible = IsIllegible;

            bool rerollAllowed = modSystem.Config.RerollEnabled && HasOffhandGear(byEntity)
                && (illegible || !modSystem.Config.RerollIllegibleOnly);
            if (rerollAllowed)
            {
                Reroll(resolvedApi, modSystem, traitCode, slot, offhandSlot, player);
                return;
            }

            if (illegible)
            {
                (player as IServerPlayer)?.SendMessage(GlobalConstants.CurrentChatGroup, Lang.Get("skillbooksstats:msg-illegible"), EnumChatType.Notification);
                return;
            }

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
        /// Strips every trait ever granted by a stat book (skillbooksLearnedTraits) back out
        /// of extraTraits and clears the history itself, so a fresh book grants the same trait
        /// again rather than being silently rejected as already-known. Used by
        /// "/skillbooksstats cleartraits" -- only registered standalone (see
        /// StatBookCommands), since core's own identical command already covers this shared,
        /// unprefixed watched attribute when core is installed. Returns how many trait codes
        /// were cleared, so callers can report it back to whoever ran the command. Mirrors
        /// core's ItemSkillBook.ClearLearnedTraits.
        /// </summary>
        internal static int ClearLearnedTraits(ICoreAPI api, EntityAgent byEntity)
        {
            string[] learned = byEntity.WatchedAttributes.GetStringArray("skillbooksLearnedTraits", System.Array.Empty<string>());
            if (learned.Length == 0) { return 0; }

            HashSet<string> learnedSet = new HashSet<string>(learned);
            string[] active = byEntity.WatchedAttributes.GetStringArray("extraTraits", System.Array.Empty<string>());
            string[] remaining = active.Where(code => !learnedSet.Contains(code)).ToArray();

            byEntity.WatchedAttributes.SetStringArray("extraTraits", remaining);
            byEntity.WatchedAttributes.MarkPathDirty("extraTraits");
            byEntity.WatchedAttributes.SetStringArray("skillbooksLearnedTraits", System.Array.Empty<string>());
            byEntity.WatchedAttributes.MarkPathDirty("skillbooksLearnedTraits");

            RefreshTraitStats(api, byEntity);

            return learned.Length;
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

        private static void Reroll(ICoreAPI api, SkillBooksStatsModSystem modSystem, string currentTraitCode, ItemSlot bookSlot, ItemSlot offhandSlot, IPlayer player)
        {
            List<Item> candidates = new List<Item>();
            foreach (string candidateTraitCode in modSystem.StatTraits.Keys)
            {
                if (candidateTraitCode == currentTraitCode) { continue; }
                Item bookItem = api.World.GetItem(new AssetLocation("skillbooksstats", "statbook-" + candidateTraitCode));
                if (bookItem != null) { candidates.Add(bookItem); }
            }
            if (candidates.Count == 0)
            {
                // No other trait to reroll into -- fall back to allowing the same one.
                Item ownBook = api.World.GetItem(new AssetLocation("skillbooksstats", "statbook-" + currentTraitCode));
                if (ownBook != null) { candidates.Add(ownBook); }
            }
            if (candidates.Count == 0) { return; }

            Item chosen = candidates[api.World.Rand.Next(candidates.Count)];

            // Give the new book *before* clearing bookSlot -- see core's ItemSkillBook.Reroll
            // for the full reasoning (log-evidence-based: a still-held interaction appears to
            // carry its already-elapsed timer onto whatever refills the just-emptied active
            // slot, instantly completing a read on the new book instead of leaving it unread).
            ItemStack resultStack = new ItemStack(chosen);
            bool given = player.InventoryManager.TryGiveItemstack(resultStack);

            offhandSlot.TakeOut(1);
            offhandSlot.MarkDirty();
            bookSlot.TakeOut(1);
            bookSlot.MarkDirty();

            if (!given)
            {
                api.World.SpawnItemEntity(resultStack, GetEntityPosXyz(player.Entity));
            }

            (player as IServerPlayer)?.SendMessage(GlobalConstants.CurrentChatGroup, Lang.Get("skillbooksstats:msg-rerolled"), EnumChatType.Notification);
        }

        /// <summary>
        /// Entity.Pos is a public field on 1.21's VintagestoryAPI but a (private-set) property
        /// on 1.22's -- confirmed via decompiling both, same issue already fixed in core's
        /// ItemSkillBook.Reroll. Identical C# source compiles to binary-incompatible IL either
        /// way, so this can't be a plain `entity.Pos.XYZ`. EntityPos.XYZ itself is a stable
        /// computed property on both versions, so only the Pos lookup itself needs reflection.
        /// </summary>
        private static Vec3d GetEntityPosXyz(Entity entity)
        {
            Type type = typeof(Entity);
            object pos = type.GetProperty("Pos")?.GetValue(entity) ?? type.GetField("Pos")?.GetValue(entity);
            return (pos as EntityPos)?.XYZ;
        }

        private static bool HasOffhandGear(EntityAgent byEntity)
        {
            return byEntity.LeftHandItemSlot?.Itemstack?.Collectible?.Code?.Equals(TemporalGearCode) == true;
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
