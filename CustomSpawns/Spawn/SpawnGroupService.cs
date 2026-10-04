using CustomSpawns.Data.Adapter;
using CustomSpawns.Data.Dto;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace CustomSpawns.Spawn
{
    /**
     * Spawns every party of one spawn: its main party first, then the parties it spawns along with.
     * Only the main party is named after the spawn, carries its priority party behaviours and is led by
     * a generated lord; the parties spawned along with it escort it instead of having a behaviour of their own.
     */
    public class SpawnGroupService
    {
        private readonly Spawner _spawner;
        private readonly DynamicSpawnData _dynamicSpawnData;

        public SpawnGroupService(Spawner spawner, DynamicSpawnData dynamicSpawnData)
        {
            _spawner = spawner;
            _dynamicSpawnData = dynamicSpawnData;
        }

        public MobileParty? Spawn(SpawnDto spawn, Settlement spawnSettlement, Clan spawnClan)
        {
            MobileParty? mainParty = _spawner.SpawnParty(spawnSettlement, spawnClan, spawn.PartyTemplate,
                new TextObject(spawn.Name), spawn.PriorityPartyBehaviourTemplateId);
            if (mainParty == null)
            {
                return null;
            }

            _dynamicSpawnData.RegisterPrimaryParty(mainParty, spawn.PartyTemplate.StringId);
            foreach (AccompanyingParty accompanyingParty in spawn.SpawnAlongWith)
            {
                MobileParty? supportingParty = _spawner.SpawnParty(spawnSettlement, spawnClan,
                    accompanyingParty.templateObject, new TextObject(accompanyingParty.name));
                if (supportingParty != null)
                {
                    _dynamicSpawnData.RegisterSupportingParty(supportingParty, spawn.PartyTemplate.StringId, mainParty);
                }
            }

            return mainParty;
        }

        public MobileParty? SpawnInvasionReinforcement(SpawnDto spawn, PartyTemplateObject reinforcementTemplate,
            Settlement landingSettlement, Clan invasionClan, string reinforcementRuleId)
        {
            MobileParty? reinforcement = _spawner.SpawnParty(landingSettlement, invasionClan, reinforcementTemplate,
                new TextObject(spawn.Name), spawn.PriorityPartyBehaviourTemplateId);
            if (reinforcement != null)
            {
                _dynamicSpawnData.RegisterInvasionReinforcementParty(reinforcement, spawn.PartyTemplate.StringId,
                    reinforcementRuleId);
            }
            return reinforcement;
        }
    }
}
