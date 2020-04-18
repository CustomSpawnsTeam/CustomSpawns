﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.SandBox.CampaignBehaviors;
using TaleWorlds.Localization;

namespace CustomSpawns.Diplomacy
{
    class ForcedWarPeaceBehaviour : CampaignBehaviorBase
    {
        public ForcedWarPeaceBehaviour()
        {

        }

        private Data.DiplomacyDataManager dataManager;

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickClanEvent.AddNonSerializedListener(this, DailyClanBehaviour);
        }

        public override void SyncData(IDataStore dataStore)
        {

        }

        private void DailyClanBehaviour(Clan c)
        {
            if (c == null)
                return;
            try
            {
                if (dataManager == null)
                {
                    GetData();
                }
                if (dataManager.Data.ContainsKey(c.StringId) && dataManager.Data[c.StringId].ForcedWarPeaceDataInstance != null)
                {
                    var forcedWarPeaceInstance = dataManager.Data[c.StringId].ForcedWarPeaceDataInstance;
                    foreach (Clan declared in Clan.All)
                    {
                        if (declared == null || declared == c || (declared.Kingdom == c.Kingdom && c.Kingdom != null))
                            continue;
                        if (forcedWarPeaceInstance.atWarClans.Contains(declared))
                        {
                            if (declared == null)
                                continue;
                            if (declared.Kingdom != null)
                            {//we deal with kingdom
                                if (!forcedWarPeaceInstance.exceptionKingdoms.Contains(declared.Kingdom))
                                {
                                    if (!FactionManager.IsAtWarAgainstFaction(c, declared.Kingdom))
                                        FactionManager.DeclareWar(c, declared.Kingdom);
                                }
                            }
                            else
                            {
                                if (!FactionManager.IsAtWarAgainstFaction(c, declared))
                                    FactionManager.DeclareWar(c, declared);
                            }
                        }
                        else
                        {
                            //what if clan left kingdom, and it was in but ?
                            if (declared.Kingdom == null && !FactionManager.IsNeutralWithFaction(c, declared))
                                FactionManager.SetNeutral(c, declared);
                        }
                    }

                    foreach (var k in forcedWarPeaceInstance.exceptionKingdoms)
                    {
                        if (k == null)
                            return;
                        if (!FactionManager.IsNeutralWithFaction(c, k))
                            FactionManager.SetNeutral(c, k);
                    }

                }
            }
            catch (Exception e)
            {
                ErrorHandler.HandleException(e, " daily clan behaviour processing of ForcedWarPeaceBehaviour.cs ");
            }
        }

        private void GetData()
        {
            dataManager = Data.DiplomacyDataManager.Instance;
        }
    }
}
