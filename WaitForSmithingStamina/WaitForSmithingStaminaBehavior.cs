using Helpers;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Overlay;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace WaitForSmithingStamina
{
    public class WaitForSmithingStaminaBehavior : CampaignBehaviorBase
    {
        private float waitProgressHours;
        private float waitTargetHours;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(AddGameMenus));
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData<float>("_smithingStaminaWaitProgressHours", ref waitProgressHours);
            dataStore.SyncData<float>("_smithingStaminaWaitTargetHours", ref waitTargetHours);
        }

        protected void AddGameMenus(CampaignGameStarter campaignGameStarter)
        {
            campaignGameStarter.AddGameMenuOption("town", "town_do_wait_smithing", "Wait to regain smithing stamina.", new GameMenuOption.OnConditionDelegate(GameMenuWaitForSmithingStaminaCondition), x => GameMenu.SwitchToMenu("town_wait_smithing"), index: 11);
            campaignGameStarter.AddWaitGameMenu("town_wait_smithing", "{=ydbVysqv}You are waiting to regain your stamina in {CURRENT_SETTLEMENT}.", new OnInitDelegate(GameMenuSettlementWaitOnInit), new OnConditionDelegate(GameMenuTownWaitOnCondition), new OnConsequenceDelegate(GameMenuTownWaitOnConsequence), new OnTickDelegate(GameMenuTownWaitMenuOnTick), GameMenu.MenuAndOptionType.WaitMenuShowOnlyProgressOption, GameOverlays.MenuOverlayType.SettlementWithBoth, targetWaitHours: waitProgressHours);
            campaignGameStarter.AddGameMenuOption("town_wait_smithing", "wait_leave", "{=UqDNAZqM}Stop waiting", new GameMenuOption.OnConditionDelegate(BackOnCondition), args =>
            {
                PlayerEncounter.Current.IsPlayerWaiting = false;
                SwitchToMenuIfThereIsAnInterrupt();
            }, true);
        }
        
        private bool GameMenuWaitForSmithingStaminaCondition(MenuCallbackArgs args)
        {
            if (!Settlement.CurrentSettlement.IsTown)
                return false;

            bool canPlayerDo = Campaign.Current.Models.SettlementAccessModel.CanMainHeroDoSettlementAction(Settlement.CurrentSettlement, SettlementAccessModel.SettlementAction.WaitInSettlement, out bool disableOption, out TextObject disabledText);
            args.optionLeaveType = GameMenuOption.LeaveType.Wait;
            if (MenuHelper.SetOptionProperties(args, canPlayerDo, disableOption, disabledText))
                return !HasPartyFullSmithingStamina();
            return false;
        }

        private void GameMenuSettlementWaitOnInit(MenuCallbackArgs args)
        {
            waitProgressHours = 0.0f;
            CalculateWaitTime();
            PlayerEncounter.Current.IsPlayerWaiting = true;
        }

        private static bool GameMenuTownWaitOnCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Wait;
            MBTextManager.SetTextVariable("CURRENT_SETTLEMENT", Settlement.CurrentSettlement.EncyclopediaLinkWithName, false);
            return true;
        }

        private static void GameMenuTownWaitOnConsequence(MenuCallbackArgs args)
        {
            PlayerEncounter.Current.IsPlayerWaiting = false;
            // It is essential to exit to last before entering the smithy, otherwise the smithy can no longer be exited.
            GameMenu.ExitToLast();
            CraftingHelper.OpenCrafting(CraftingTemplate.All.First<CraftingTemplate>());
        }


        private void GameMenuTownWaitMenuOnTick(MenuCallbackArgs args, CampaignTime timeDifferenceSinceLastTick)
        {
            SwitchToMenuIfThereIsAnInterrupt();
            waitProgressHours += (float)timeDifferenceSinceLastTick.ToHours;
            if (waitTargetHours.ApproximatelyEqualsTo(0.0f))
                args.MenuContext.GameMenu.SetProgressOfWaitingInMenu(1.0f);
            args.MenuContext.GameMenu.SetProgressOfWaitingInMenu(waitProgressHours / waitTargetHours);
        }

        private void SwitchToMenuIfThereIsAnInterrupt()
        {
            string genericStateMenu = Campaign.Current.Models.EncounterGameMenuModel.GetGenericStateMenu();
            if (!(genericStateMenu != "town_wait_menus"))
                return;
            if (!string.IsNullOrEmpty(genericStateMenu))
                GameMenu.SwitchToMenu(genericStateMenu);
            else
                GameMenu.ExitToLast();
        }

        private static bool BackOnCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Leave;
            return true;
        }

        private bool HasPartyFullSmithingStamina()
        {
            CraftingCampaignBehavior campaignBehavior = Campaign.Current.GetCampaignBehavior<CraftingCampaignBehavior>();
            return CraftingHelper.GetAvailableHeroesForCrafting().All(hero => campaignBehavior.GetHeroCraftingStamina(hero) == campaignBehavior.GetMaxHeroCraftingStamina(hero));
        }

        private void CalculateWaitTime()
        {
            waitTargetHours = CraftingHelper.GetAvailableHeroesForCrafting().Max(hero => GetHoursUntilRecovery(hero));
        }

        int GetHoursUntilRecovery(Hero hero)
        {
            CraftingCampaignBehavior campaignBehavior = Campaign.Current.GetCampaignBehavior<CraftingCampaignBehavior>();
            int missingStamina = campaignBehavior.GetMaxHeroCraftingStamina(hero) - campaignBehavior.GetHeroCraftingStamina(hero);
            return (int)Math.Ceiling((double)missingStamina / (double)GetStaminaHourlyRecoveryRate(hero));
        }

        // taken from CraftingCampaignBehavior.
        private int GetStaminaHourlyRecoveryRate(Hero hero)
        {
            int hourlyRecoveryRate = 5 + MathF.Round((float)hero.GetSkillValue(DefaultSkills.Crafting) * 0.025f);
            if (hero.GetPerkValue(DefaultPerks.Athletics.Stamina))
                hourlyRecoveryRate += MathF.Round((float)hourlyRecoveryRate * DefaultPerks.Athletics.Stamina.PrimaryBonus);
            return hourlyRecoveryRate;
        }
    }
}
