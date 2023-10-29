using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Data.Dao;
using CustomSpawns.Data.Model.Reward;
using CustomSpawns.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace CustomSpawns.RewardSystem
{
    public class SpawnRewardBehaviour : CampaignBehaviorBase
    {
        private const string PlayerPartyId = "player_party";
        private readonly Random _random;
        private readonly RewardDao _rewardDao;
        private readonly ModDebug _modDebug;

        public SpawnRewardBehaviour(RewardDao rewardDao, ModDebug modDebug)
        {
            _rewardDao = rewardDao;
            _modDebug = modDebug;
            _random = new Random();
        }
        
        public override void RegisterEvents()
        {
            CampaignEvents.OnPlayerBattleEndEvent.AddNonSerializedListener(this,
            mapEvent =>
            {
                if (mapEvent.GetLeaderParty(BattleSideEnum.Attacker).Id == PlayerPartyId &&
                    mapEvent.WinningSide == BattleSideEnum.Attacker)
                {
                    CalculateReward(mapEvent.DefenderSide.Parties, mapEvent.AttackerSide.Parties.Find(p => p.Party.Id == PlayerPartyId));
                }
                else if (mapEvent.GetLeaderParty(BattleSideEnum.Defender).Id == PlayerPartyId &&
                         mapEvent.WinningSide == BattleSideEnum.Defender)
                {
                    CalculateReward(mapEvent.AttackerSide.Parties, mapEvent.DefenderSide.Parties.Find(p => p.Party.Id == PlayerPartyId));
                }
            });
        }

        public override void SyncData(IDataStore dataStore){}

        private void CalculateReward(List<MapEventParty> defeatedParties, MapEventParty mapEventPlayerParty)
        {
            foreach (var party in defeatedParties)
            {
                var moneyAmount = 0;
                var renownAmount = 0;
                var influenceAmount = 0;
                var partyRewards = _rewardDao.FindAll();
                var partyReward = partyRewards.FirstOrDefault(el => party.Party.Id.Contains(el.PartyId));
                if (partyReward != null)
                {
                    foreach (Reward reward in partyReward.RewardsList)
                    {
                        switch (reward.Type)
                        {
                            case RewardType.Influence:
                                if (reward.RenownInfluenceMoneyAmount != null)
                                {
                                    influenceAmount = Convert.ToInt32(reward.RenownInfluenceMoneyAmount);
                                    mapEventPlayerParty.GainedInfluence += Convert.ToSingle(reward.RenownInfluenceMoneyAmount);
                                }
                                break;
                            case RewardType.Money:
                                if (reward.RenownInfluenceMoneyAmount != null)
                                {
                                    moneyAmount = Convert.ToInt32(reward.RenownInfluenceMoneyAmount);
                                    mapEventPlayerParty.PlunderedGold += Convert.ToInt32(reward.RenownInfluenceMoneyAmount);
                                }
                                break;
                            case RewardType.Item:
                                if (reward.ItemId != null)
                                {
                                    ItemObject? item = Items.All.Find(obj => obj.StringId == reward.ItemId);
                                    if (item != null && IsItemGiven(reward.Chance ?? 1f))
                                    {
                                        mapEventPlayerParty.RosterToReceiveLootItems.Add(new ItemRosterElement(item, 1));
                                        UX.ShowMessage($"You found {item.Name}", Colors.Green);   
                                    }
                                }
                                break;
                            case RewardType.Renown:
                                if (reward.RenownInfluenceMoneyAmount != null)
                                {
                                    renownAmount = Convert.ToInt32(reward.RenownInfluenceMoneyAmount);
                                    mapEventPlayerParty.GainedRenown += Convert.ToSingle(reward.RenownInfluenceMoneyAmount);
                                }
                                break;
                        }
                    }

                    InformationManager.DisplayMessage(
                        new InformationMessage(
                            $"{mapEventPlayerParty.Party?.LeaderHero?.Name.ToString() ?? Agent.Main.Name} defeated {party.Party.Name} gaining {moneyAmount} denars, {renownAmount} renown and {influenceAmount} influence", 
                            Colors.Green
                            )
                        );
                }
            }
        }

        private bool IsItemGiven(float probability)
        {
            var chance = Math.Min(Math.Max(0, probability), 1);
            var pseudoRandomValue = _random.Next() % 100;
            _modDebug.ShowMessage($"Random value: {pseudoRandomValue} | Chance: {chance}", DebugMessageType.Reward);
            return pseudoRandomValue <= chance * 100;
        }
    }
}