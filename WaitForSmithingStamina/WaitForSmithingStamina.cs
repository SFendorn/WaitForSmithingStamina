using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace WaitForSmithingStamina
{
    public class WaitForSmithingStamina : MBSubModuleBase
    {
        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            if (!(game.GameType is Campaign))
                return;
            AddBehaviors((CampaignGameStarter) gameStarterObject);
        }

        private void AddBehaviors(CampaignGameStarter campaignGameStarter)
        {
            campaignGameStarter.AddBehavior(new WaitForSmithingStaminaBehavior());
        }
    }
}
