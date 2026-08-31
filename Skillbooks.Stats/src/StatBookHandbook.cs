using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace Skillbooks.Stats
{
    /// <summary>
    /// Registers a single static handbook page describing the stat book mechanic as a
    /// concept, mirroring core's SkillBookHandbook -- also describes the reroll mechanic
    /// (holding a temporal gear in the offhand while reading) and salvaging (crafting with a
    /// knife).
    /// </summary>
    public class StatBookHandbook : ModSystem
    {
        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Client;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            api.ModLoader.GetModSystem<ModSystemSurvivalHandbook>().OnInitCustomPages += pages => AddPage(api, pages);
        }

        private void AddPage(ICoreClientAPI api, List<GuiHandbookPage> pages)
        {
            GuiHandbookTextPage page = new GuiHandbookTextPage
            {
                pageCode = "skillbooksstats-concept",
                Title = Lang.Get("skillbooksstats:handbook-title"),
                Text = Lang.Get("skillbooksstats:handbook-text"),
            };
            page.Init(api);
            pages.Add(page);
        }
    }
}
